using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Services;

public class PureserviceUserService : IPureserviceUserService
{
    private readonly ILogger<PureserviceUserService> _logger;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "user";
    
    public PureserviceUserService(ILogger<PureserviceUserService> logger, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<bool> AddManager(int userId, int managerId) =>
        await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new { managerId });

    public async Task<UserList> GetUser(int userId, string[]? entities = null)
    {
        if (entities == null)
        {
            return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}") ?? new UserList([], null);
        }
        
        return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}?include={string.Join(",", entities)}") ?? new UserList([], null);
    }

    public async Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false)
    {
        var userList = new UserList([], new Linked{ PhoneNumbers = [] });

        var currentStart = start;
        _logger.LogInformation("Fetching users starting from {Start} with limit {Limit}", start, limit);

        var filterString = !includeSystemUsers
            ? $"filter=role > {(int)UserRole.None} AND role < {(int)UserRole.System}&"
            : "";
        var entitiesString = entities != null
            ? $"&include={string.Join(",", entities)}"
            : "";

        UserList? result = null;
        
        while (result is null || result.Users.Count > 0)
        {
            result = await _pureserviceCaller.GetAsync<UserList>($"{BasePath}?{filterString}start={currentStart}&limit={limit}{entitiesString}");
            if (result is null)
            {
                _logger.LogError("No result returned from API from start {Start}", currentStart);
                return userList;
            }
            
            _logger.LogInformation("Fetched {Count} users starting from {Start}", result.Users.Count, currentStart);
            userList.Users.AddRange(result.Users);
            if (result.Linked?.PhoneNumbers is not null)
            {
                _logger.LogInformation("Fetched {Count} phone numbers linked to users", result.Linked.PhoneNumbers.Count);
                userList.Linked!.PhoneNumbers!.AddRange(result.Linked.PhoneNumbers);
            }
            
            if (result.Users.Count == 0)
            {
                _logger.LogInformation("No more users to fetch. Ending at start {Start}. Returning {UserCount} user count with {PhoneNumberCount} phone numbers", currentStart, userList.Users.Count, userList.Linked?.PhoneNumbers?.Count ?? 0);
                return userList;
            }

            currentStart += result.Users.Count;
            _logger.LogInformation("Preparing to fetch next batch of users starting from {Start}", currentStart);
        }
        
        _logger.LogInformation("Reached outside of while somehow. Returning {UserCount} user count with {PhoneNumberCount} phone numbers", userList.Users.Count, userList.Linked?.PhoneNumbers?.Count ?? 0);
        return userList;
    }
}