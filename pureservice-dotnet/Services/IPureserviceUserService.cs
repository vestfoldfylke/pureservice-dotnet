using System.Threading.Tasks;
using pureservice_dotnet.Models;

namespace pureservice_dotnet.Services;

public interface IPureserviceUserService
{
    Task<bool> UpdateManager(int userId, int managerId);
    Task<UserList> GetUser(string filter, string[]? entities = null);
    Task<UserList> GetUser(int userId, string[]? entities = null);
    Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false);
    Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId);
}