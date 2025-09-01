using System.Threading.Tasks;
using pureservice_dotnet.Models;

namespace pureservice_dotnet.Services;

public interface IPureserviceUserService
{
    Task<bool> AddManager(int userId, int managerId);
    Task<UserList> GetUser(int userId, string[]? entities = null);
    Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false);
}