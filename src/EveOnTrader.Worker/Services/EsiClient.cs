using System.Net;
using System.Net.Http.Json;

namespace EveOnTrader.Worker.Services;

// EsiClient is shared HTTP client for talking to ESI with retry/backoff logic in one place.
public class EsiClient
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

    // Creates the shared ESI client around one configured HttpClient from DI.
    public EsiClient(HttpClient http)
    {
        _http = http;
    }

    // Sends a GET request to ESI and retries transient failures before giving up.
    public Task<HttpResponseMessage> GetAsync(string url, string operation)
    {
        return SendWithRetryAsync(
            () => _http.GetAsync(url),
            operation,
            allowNonRetriableFailure: false);
    }

    // Sends a GET request to ESI and retries transient failures, but returns non-retriable failures like 404 to the caller.
    public Task<HttpResponseMessage> TryGetAsync(string url, string operation)
    {
        return SendWithRetryAsync(
            () => _http.GetAsync(url),
            operation,
            allowNonRetriableFailure: true);
    }

    // Sends a POST request with a JSON body to ESI and retries transient failures before giving up.
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T body, string operation)
    {
        return SendWithRetryAsync(
            () => _http.PostAsJsonAsync(url, body),
            operation,
            allowNonRetriableFailure: false);
    }

    // Core retry loop used by all ESI requests; retries transient HTTP and network failures with backoff.
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> sendAsync,
        string operation,
        bool allowNonRetriableFailure)
    {
        for (var attempt = 1; attempt <= MaxRequestAttempts; attempt++)
        {
            try
            {
                var resp = await sendAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    if (IsRetriableStatusCode(resp.StatusCode) && attempt < MaxRequestAttempts)
                    {
                        var delay = GetRetryDelay(resp, attempt);

                        Console.WriteLine(
                            $"{operation}: HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}. Retrying in {delay.TotalSeconds:n0}s (attempt {attempt}/{MaxRequestAttempts})...");

                        resp.Dispose();
                        await Task.Delay(delay);
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

                Console.WriteLine(
                    $"{operation}: request failed: {ex.Message}. Retrying in {delay.TotalSeconds:n0}s (attempt {attempt}/{MaxRequestAttempts})...");

                await Task.Delay(delay);
            }
            catch (TaskCanceledException ex) when (attempt < MaxRequestAttempts)
            {
                var delay = GetRetryDelay(attempt);

                Console.WriteLine(
                    $"{operation}: request timed out: {ex.Message}. Retrying in {delay.TotalSeconds:n0}s (attempt {attempt}/{MaxRequestAttempts})...");

                await Task.Delay(delay);
            }
        }

        throw new InvalidOperationException($"Request retry loop ended unexpectedly for {operation}.");
    }

    // Returns true for HTTP statuses that are usually temporary and worth retrying.
    private static bool IsRetriableStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode == 420 ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    // Returns a backoff delay based on the current retry attempt number.
    private static TimeSpan GetRetryDelay(int attempt)
    {
        var index = Math.Min(attempt - 1, RetryDelays.Length - 1);
        return RetryDelays[index];
    }

    // Uses Retry-After from the server when present; otherwise falls back to the normal backoff delay.
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
