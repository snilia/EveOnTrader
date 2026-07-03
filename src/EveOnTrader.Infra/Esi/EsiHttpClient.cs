using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace EveOnTrader.Infra.Esi;

// EsiHttpClient wraps ESI HTTP calls with shared retry/backoff behavior.
public class EsiHttpClient
{
    private const int MaxRequestAttempts = 5;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];

    private readonly HttpClient _http;
    private readonly ILogger<EsiHttpClient> _logger;

    // Creates shared ESI HTTP client around configured HttpClient from DI.
    public EsiHttpClient(HttpClient http, ILogger<EsiHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // Sends GET request to ESI and retries transient failures before giving up.
    public Task<HttpResponseMessage> GetAsync(
        string url,
        string operation,
        CancellationToken cancellationToken = default)
    {
        return SendWithRetryAsync(
            ct => _http.GetAsync(url, ct),
            operation,
            allowNonRetriableFailure: false,
            cancellationToken);
    }

    // Sends GET request to ESI and retries transient failures, but returns non-retriable failures like 404 to caller.
    public Task<HttpResponseMessage> TryGetAsync(
        string url,
        string operation,
        CancellationToken cancellationToken = default)
    {
        return SendWithRetryAsync(
            ct => _http.GetAsync(url, ct),
            operation,
            allowNonRetriableFailure: true,
            cancellationToken);
    }

    // Sends POST request with JSON body to ESI and retries transient failures before giving up.
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string url,
        T body,
        string operation,
        CancellationToken cancellationToken = default)
    {
        return SendWithRetryAsync(
            ct => _http.PostAsJsonAsync(url, body, ct),
            operation,
            allowNonRetriableFailure: false,
            cancellationToken);
    }

    // Core retry loop used by all ESI requests.
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> sendAsync,
        string operation,
        bool allowNonRetriableFailure,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxRequestAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var resp = await sendAsync(cancellationToken);

                if (!resp.IsSuccessStatusCode)
                {
                    if (IsRetriableStatusCode(resp.StatusCode) && attempt < MaxRequestAttempts)
                    {
                        var delay = GetRetryDelay(resp, attempt);

                        _logger.LogWarning(
                            "{Operation}: HTTP {StatusCode} {ReasonPhrase}. Retrying in {DelaySeconds:n0}s (attempt {Attempt}/{MaxAttempts})...",
                            operation,
                            (int)resp.StatusCode,
                            resp.ReasonPhrase,
                            delay.TotalSeconds,
                            attempt,
                            MaxRequestAttempts);

                        resp.Dispose();
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }

                    if (allowNonRetriableFailure && !IsRetriableStatusCode(resp.StatusCode))
                    {
                        return resp;
                    }

                    try
                    {
                        resp.EnsureSuccessStatusCode();
                    }
                    catch
                    {
                        resp.Dispose();
                        throw;
                    }
                }

                return resp;
            }
            catch (HttpRequestException ex) when (attempt < MaxRequestAttempts)
            {
                var delay = GetRetryDelay(attempt);

                _logger.LogWarning(
                    ex,
                    "{Operation}: request failed. Retrying in {DelaySeconds:n0}s (attempt {Attempt}/{MaxAttempts})...",
                    operation,
                    delay.TotalSeconds,
                    attempt,
                    MaxRequestAttempts);

                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < MaxRequestAttempts)
            {
                var delay = GetRetryDelay(attempt);

                _logger.LogWarning(
                    ex,
                    "{Operation}: request timed out. Retrying in {DelaySeconds:n0}s (attempt {Attempt}/{MaxAttempts})...",
                    operation,
                    delay.TotalSeconds,
                    attempt,
                    MaxRequestAttempts);

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Request retry loop ended unexpectedly for {operation}.");
    }

    // Returns true for HTTP statuses usually temporary enough to retry.
    private static bool IsRetriableStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode == 420 ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    // Returns backoff delay based on current retry attempt.
    private static TimeSpan GetRetryDelay(int attempt)
    {
        var index = Math.Min(attempt - 1, RetryDelays.Length - 1);
        return RetryDelays[index];
    }

    // Uses Retry-After when server provides it; otherwise normal backoff.
    private static TimeSpan GetRetryDelay(HttpResponseMessage resp, int attempt)
    {
        var retryAfter = resp.Headers.RetryAfter?.Delta;

        if (retryAfter is not null && retryAfter.Value > TimeSpan.Zero)
        {
            return retryAfter.Value;
        }

        return GetRetryDelay(attempt);
    }
}