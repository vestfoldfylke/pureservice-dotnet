using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace pureservice_dotnet.Services;

public interface IGraphService
{
    Task<User?> GetEmployeeManager(string userPrincipalName);
    Task<List<User>> GetEmployees();
}