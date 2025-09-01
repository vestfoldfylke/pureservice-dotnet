using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace pureservice_dotnet.Functions;

public class MetricsEndpoint
{
    private readonly ILogger<MetricsEndpoint> _logger;

    public MetricsEndpoint(ILogger<MetricsEndpoint> logger)
    {
        _logger = logger;
    }

    [Function("Metrics")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "metrics")]
        HttpRequest req)
    {
        _logger.LogDebug("Serving Prometheus metrics");
        
        var responseStream = new MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(responseStream);
        responseStream.Position = 0;
        
        var reader = new StreamReader(responseStream);
        var content = await reader.ReadToEndAsync();
        
        return new ContentResult
        {
            Content = content,
            ContentType = "text/plain",
            StatusCode = StatusCodes.Status200OK
        };
    }
}