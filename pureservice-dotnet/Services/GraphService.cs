using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Vestfold.Extensions.Authentication.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;
    private readonly IMetricsService _metricsService;
    
    private const string MetricsServicePrefix = "Graph";
    
    public GraphService(IAuthenticationService authenticationService, ILogger<GraphService> logger, IMetricsService metricsService)
    {
        _graphClient = authenticationService.CreateGraphClient();
        _logger = logger;
        _metricsService = metricsService;
    }

    public async Task<User?> GetEmployeeManager(string userPrincipalName)
    {
        try
        {
            var manager = await _graphClient.Users[userPrincipalName].Manager.GetAsync();
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_GetEmployeeManager", "Number of GetEmployeeManager requests to Graph",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            
            if (manager is not null)
            {
                return (User)manager;
            }

            _logger.LogInformation("No manager found for user '{UserPrincipalName}'", userPrincipalName);
            return null;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "Error getting manager for user '{UserPrincipalName}'", userPrincipalName);
            _metricsService.Count($"{Constants.MetricsPrefix}_{MetricsServicePrefix}_GetEmployeeManager", "Number of GetEmployeeManager requests to Graph",
                (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
            return null;
        }
    }

    public async Task<List<User>> GetEmployees()
    {
        List<User> allUsers = [];

        // NOTE: When $expand is used, Microsoft has a hard limit of 100 users per page. Adding $top=999 has no effect!
        var allEmployees = await GetUsersPage(
            "https://graph.microsoft.com/v1.0/users?$filter=endsWith(userPrincipalName, '@vestfoldfylke.no')&$expand=manager($levels=1;$select=id)&$count=true&$select=id,displayName,userPrincipalName,mail,mobilePhone,accountEnabled&$top=999");

        allUsers.AddRange(allEmployees.Value ?? []);
        
        while (allEmployees.OdataNextLink is not null)
        {
            allEmployees = await GetUsersPage(allEmployees.OdataNextLink);
            allUsers.AddRange(allEmployees.Value ?? []);
        }

        return allUsers;
    }

    private async Task<UserCollectionResponse> GetUsersPage(string requestUrl)
    {
        var usersRequestBuilder = _graphClient.Users.WithUrl(requestUrl);
        var users = await usersRequestBuilder.GetAsync(request =>
            {
                request.Headers.Add("ConsistencyLevel", "eventual");
            });
        return users ?? new UserCollectionResponse();
    }
}