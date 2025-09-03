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
}