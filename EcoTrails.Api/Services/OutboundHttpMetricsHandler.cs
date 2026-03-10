using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EcoTrails.Api.Services;

public sealed class OutboundHttpMetricsHandler : DelegatingHandler
{
    public const string MeterName = "EcoTrails.Api.OutboundHttp";

    private readonly Histogram<double> _durationMs;
    private readonly Counter<long> _requests;
    private readonly string _clientName;

    public OutboundHttpMetricsHandler(IMeterFactory meterFactory, string clientName)
    {
        var meter = meterFactory.Create(MeterName);
        _durationMs = meter.CreateHistogram<double>(
            name: "external.http.request.duration",
            unit: "ms",
            description: "Duration of outbound HTTP calls from EcoTrails.Api.");
        _requests = meter.CreateCounter<long>(
            name: "external.http.requests",
            unit: "{request}",
            description: "Number of outbound HTTP calls from EcoTrails.Api.");
        _clientName = clientName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = request.Method.Method;
        var host = request.RequestUri?.Host ?? "unknown";

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            Record(stopwatch.Elapsed.TotalMilliseconds, method, host, (int)response.StatusCode, response.IsSuccessStatusCode, null);
            return response;
        }
        catch (Exception exception)
        {
            Record(stopwatch.Elapsed.TotalMilliseconds, method, host, 0, false, exception.GetType().Name);
            throw;
        }
    }

    private void Record(double elapsedMs, string method, string host, int statusCode, bool success, string? exceptionType)
    {
        var tags = new TagList
        {
            { "client.name", _clientName },
            { "http.method", method },
            { "server.address", host },
            { "http.response.status_code", statusCode },
            { "success", success }
        };

        if (!string.IsNullOrWhiteSpace(exceptionType))
        {
            tags.Add("exception.type", exceptionType);
        }

        _durationMs.Record(elapsedMs, tags);
        _requests.Add(1, tags);
    }
}