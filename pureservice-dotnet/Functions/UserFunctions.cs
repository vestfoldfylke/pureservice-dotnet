using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly ILogger<UserFunctions> _logger;

    public UserFunctions(ILogger<UserFunctions> logger)
    {
        _logger = logger;
    }

    [Function("Synchronize")]
    public IActionResult Synchronize([HttpTrigger(
        AuthorizationLevel.Function,
        "post",
        Route = "User/Synchronize")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}