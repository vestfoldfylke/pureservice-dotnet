using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<UserFunctions> _logger;

    public UserFunctions(IMetricsService metricsService, ILogger<UserFunctions> logger)
    {
        _metricsService = metricsService;
        _logger = logger;
    }

    [Function("Synchronize")]
    [OpenApiOperation(operationId: "Synchronize")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key",
        In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "text/plain", typeof(string), Description = "Trigger finished")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "text/plain", typeof(string),
        Description = "Trigger failed")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "text/plain", typeof(string),
        Description = "Error occured")]
    public IActionResult Synchronize([HttpTrigger(
        AuthorizationLevel.Function,
        "post",
        Route = "User/Synchronize")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        _metricsService.Count("UserFunctions_Synchronize");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}