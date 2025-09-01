using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using pureservice_dotnet.Models;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly IMetricsService _metricsService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IPureserviceUserService _pureserviceUserService;

    public UserFunctions(IMetricsService metricsService, ILogger<UserFunctions> logger, IPureserviceUserService pureserviceUserService)
    {
        _metricsService = metricsService;
        _logger = logger;
        _pureserviceUserService = pureserviceUserService;
    }

    [Function("Synchronize")]
    [OpenApiOperation(operationId: "Synchronize")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key",
        In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(UserList), Description = "Trigger finished")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "text/plain", typeof(string),
        Description = "Trigger failed")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "text/plain", typeof(string),
        Description = "Error occured")]
    public async Task<IActionResult> Synchronize([HttpTrigger(
        AuthorizationLevel.Function,
        "post",
        Route = "User/Synchronize")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        _metricsService.Count("UserFunctions_Synchronize");
        
        /*var result = await _pureserviceUserService.AddManager(25, 8);
        
        return new JsonResult(new { Result = result });*/
        
        /*UserList users = await _pureserviceUserService.GetUser(25, ["phonenumbers"]);

        return new JsonResult(users);*/
        
        UserList users = await _pureserviceUserService.GetUsers(["phonenumbers"], 0, 500);
        
        return new JsonResult(users);
    }
}