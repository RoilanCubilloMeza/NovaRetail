using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: LoadProbe <url> [concurrency] [requests] [timeoutSeconds]");
    return 1;
}

var url = args[0];
var concurrency = args.Length > 1 && int.TryParse(args[1], out var parsedConcurrency) ? parsedConcurrency : 100;
var requests = args.Length > 2 && int.TryParse(args[2], out var parsedRequests) ? parsedRequests : concurrency;
var timeoutSeconds = args.Length > 3 && int.TryParse(args[3], out var parsedTimeout) ? parsedTimeout : 60;

if (concurrency <= 0 || requests <= 0 || timeoutSeconds <= 0)
{
    Console.Error.WriteLine("All numeric arguments must be greater than zero.");
    return 1;
}

using var handler = new SocketsHttpHandler
{
    MaxConnectionsPerServer = concurrency * 2,
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30)
};
using var client = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
};

var gate = new SemaphoreSlim(concurrency, concurrency);
var results = new ConcurrentBag<RequestResult>();
var startedAt = Stopwatch.StartNew();

var tasks = Enumerable.Range(0, requests).Select(async index =>
{
    await gate.WaitAsync().ConfigureAwait(false);
    try
    {
        var requestWatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
            results.Add(new RequestResult(index, (int)response.StatusCode, requestWatch.ElapsedMilliseconds, null));
        }
        catch (Exception ex)
        {
            results.Add(new RequestResult(index, 0, requestWatch.ElapsedMilliseconds, ex.GetBaseException().Message));
        }
    }
    finally
    {
        gate.Release();
    }
});

await Task.WhenAll(tasks).ConfigureAwait(false);
startedAt.Stop();

var completed = results.OrderBy(r => r.Index).ToArray();
var latencies = completed.Select(r => r.DurationMs).OrderBy(ms => ms).ToArray();
var okCount = completed.Count(r => r.StatusCode is >= 200 and < 300);
var errorCount = completed.Length - okCount;
var requestsPerSecond = completed.Length / Math.Max(0.001, startedAt.Elapsed.TotalSeconds);

var summary = new
{
    Url = url,
    Concurrency = concurrency,
    Requests = requests,
    ElapsedMs = startedAt.ElapsedMilliseconds,
    RequestsPerSecond = Math.Round(requestsPerSecond, 2),
    Ok = okCount,
    Errors = errorCount,
    AvgMs = Math.Round(latencies.DefaultIfEmpty().Average(), 2),
    P50Ms = Percentile(latencies, 0.50),
    P95Ms = Percentile(latencies, 0.95),
    P99Ms = Percentile(latencies, 0.99),
    MaxMs = latencies.LastOrDefault(),
    StatusCounts = completed.GroupBy(r => r.StatusCode).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
    SampleErrors = completed.Where(r => !string.IsNullOrWhiteSpace(r.Error)).Select(r => r.Error).Distinct().Take(10).ToArray()
};

Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(summary));
return 0;

static long Percentile(long[] values, double percentile)
{
    if (values.Length == 0)
        return 0;

    var index = (int)Math.Ceiling(values.Length * percentile) - 1;
    index = Math.Clamp(index, 0, values.Length - 1);
    return values[index];
}

internal sealed record RequestResult(int Index, int StatusCode, long DurationMs, string? Error);