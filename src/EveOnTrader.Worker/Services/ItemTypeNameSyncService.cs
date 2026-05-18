using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;


//ItemTypeNameSyncService is a worker-side sync service that finds item IDs missing from ItemTypeRefs, 
//resolves their names from ESI in batches, and saves them into the database.
public class ItemTypeNameSyncService
{
    private const string CompatibilityDate = "2025-12-16";
    private const int BatchSize = 500;
    private const int MaxRequestAttempts = 5;

    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30)
    ];

    private readonly AppDbContext _db;

    // Creates the sync service with database access for reading missing types and saving resolved names.
    public ItemTypeNameSyncService(AppDbContext db)
    {
        _db = db;
    }

    // Finds missing item type IDs from MarketOrders, resolves their names from ESI in batches, and stores them in ItemTypeRefs.
    public async Task<int> SyncItemTypeRefsAsync()
    {
        Console.WriteLine("Checking for missing item type names...");

        var marketTypeIds = await _db.MarketOrders
            .Select(x => x.TypeId)
            .Distinct()
            .ToListAsync();

        var existingTypeIds = await _db.ItemTypeRefs
            .Select(x => x.TypeId)
            .ToHashSetAsync();

        var missingTypeIds = marketTypeIds
            .Where(id => !existingTypeIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingTypeIds.Count == 0)
        {
            Console.WriteLine("No missing item type names.");
            return 0;
        }

        Console.WriteLine($"Found {missingTypeIds.Count:n0} missing item type IDs. Resolving names from ESI...");

        using var http = CreateHttpClient();

        var inserted = 0;

        foreach (var batch in Batch(missingTypeIds, BatchSize))
        {
            using var resp = await PostAsJsonWithRetryAsync(
                http,
                "universe/names/?datasource=tranquility",
                batch,
                $"item type names batch ({batch.Count:n0} ids)");

            var results = await resp.Content.ReadFromJsonAsync<List<UniverseNameResult>>()
                         ?? new List<UniverseNameResult>();

            var refsToInsert = results
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new ItemTypeRef
                {
                    TypeId = x.Id,
                    Name = x.Name
                })
                .ToList();

            if (refsToInsert.Count > 0)
            {
                _db.ItemTypeRefs.AddRange(refsToInsert);
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();

                inserted += refsToInsert.Count;
            }

            Console.WriteLine($"Resolved {inserted:n0}/{missingTypeIds.Count:n0} item type names...");
        }

        Console.WriteLine($"Inserted {inserted:n0} new ItemTypeRef rows.");
        return inserted;
    }

    // Sends a POST request with a JSON body and retries transient failures before giving up.
    private Task<HttpResponseMessage> PostAsJsonWithRetryAsync<T>(HttpClient http, string url, T body, string operation)
    {
        return SendWithRetryAsync(
            () => http.PostAsJsonAsync(url, body),
            operation);
    }

    // Core retry loop for ESI requests; retries transient network and HTTP failures with backoff.
    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> sendAsync,
        string operation)
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

    // Creates a configured HttpClient for talking to ESI with required headers.
    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://esi.evetech.net/latest/")
        };

        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Compatibility-Date",
            CompatibilityDate);

        http.DefaultRequestHeaders.UserAgent.ParseAdd("EveOnTrader.Worker/1.0");

        return http;
    }

    // Splits a list of IDs into fixed-size batches for batched ESI requests.
    private static IEnumerable<List<long>> Batch(List<long> source, int batchSize)
    {
        for (var i = 0; i < source.Count; i += batchSize)
        {
            yield return source.Skip(i).Take(batchSize).ToList();
        }
    }
}
