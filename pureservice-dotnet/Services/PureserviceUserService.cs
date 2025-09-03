using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.ActionModels;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public class PureserviceUserService : IPureserviceUserService
{
    private readonly ILogger<PureserviceUserService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "user";
    
    public PureserviceUserService(ILogger<PureserviceUserService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<UserList> GetUser(string filter, string[]? entities = null)
    {
        var endpoint = $"{BasePath}?filter={filter}";
        if (entities != null)
        {
            endpoint += $"&include={string.Join(",", entities)}";
        }
        
        return await _pureserviceCaller.GetAsync<UserList>(endpoint) ?? new UserList([], null);
    }
    
    public async Task<UserList> GetUser(int userId, string[]? entities = null)
    {
        if (entities == null)
        {
            return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}") ?? new UserList([], null);
        }
        
        return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}?include={string.Join(",", entities)}") ?? new UserList([], null);
    }

    public async Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false)
    {
        var userList = new UserList([], new Linked{ EmailAddresses = [], PhoneNumbers = [] });

        var currentStart = start;
        
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        
        if (!includeInactiveUsers)
        {
            queryString["filter"] = "disabled == false";
        }
        
        if (!includeSystemUsers)
        {
            var filter = queryString["filter"];
            queryString["filter"] = string.IsNullOrEmpty(filter)
                ? $"role > {(int)UserRole.None} AND role < {(int)UserRole.System}"
                : $"{filter} AND role > {(int)UserRole.None} AND role < {(int)UserRole.System}";
        }
        
        if (entities != null)
        {
            queryString["include"] = string.Join(",", entities);
        }

        queryString["start"] = currentStart.ToString();
        queryString["limit"] = limit.ToString();

        UserList? result = null;
        
        while (result is null || result.Users.Count > 0)
        {
            queryString["start"] = currentStart.ToString();
            
            result = await _pureserviceCaller.GetAsync<UserList>($"{BasePath}?{queryString}");
            if (result is null)
            {
                _logger.LogError("No result returned from API from start {Start} with limit {Limit}", currentStart, limit);
                return userList;
            }
            
            _logger.LogInformation("Fetched {Count} users starting from {Start} with limit {Limit}", result.Users.Count, currentStart, limit);
            userList.Users.AddRange(result.Users);
            
            if (result.Linked?.EmailAddresses is not null && userList.Linked?.EmailAddresses is not null)
            {
                _logger.LogInformation("Fetched {Count} email addresses linked to users", result.Linked.EmailAddresses.Count);
                userList.Linked.EmailAddresses.AddRange(result.Linked.EmailAddresses);
            }
            
            if (result.Linked?.PhoneNumbers is not null && userList.Linked?.PhoneNumbers is not null)
            {
                _logger.LogInformation("Fetched {Count} phone numbers linked to users", result.Linked.PhoneNumbers.Count);
                userList.Linked.PhoneNumbers.AddRange(result.Linked.PhoneNumbers);
            }
            
            if (result.Users.Count == 0)
            {
                _logger.LogInformation("No more users to fetch. Ending at start {Start}. Returning {UserCount} user count with {EmailAddressCount} emailaddresses and {PhoneNumberCount} phone numbers",
                    currentStart, userList.Users.Count, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0);
                return userList;
            }

            currentStart += result.Users.Count;
            _logger.LogInformation("Preparing to fetch next batch of users starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogInformation("Reached outside of while somehow 😱 Returning {UserCount} user count with {EmailAddressCount} emailaddresses and {PhoneNumberCount} phone numbers",
            userList.Users.Count, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0);
        return userList;
    }
    
    public async Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId)
    {
        _logger.LogInformation("Registering PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new RegisterPhoneNumberAsDefault(phoneNumberId));

        if (result)
        {
            _logger.LogInformation("Successfully registered PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to register PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
    
    public async Task<bool> UpdateManager(int userId, int managerId)
    {
        _logger.LogInformation("Updating manager on UserId {UserId} to ManagerId {ManagerId}", userId, managerId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new UpdateManager(managerId));
        
        if (result)
        {
            _logger.LogInformation("Successfully updated UserId {UserId} with ManagerId {ManagerId}", userId, managerId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateManager", "Number of manager updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update UserId {UserId} with ManagerId {ManagerId}", userId, managerId);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateManager", "Number of manager updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}