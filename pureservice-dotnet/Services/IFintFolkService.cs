using System.Threading.Tasks;
using pureservice_dotnet.Models.Fint;

namespace pureservice_dotnet.Services;

public interface IFintFolkService
{
    Task<FintStudent?> GetStudent(string userPrincipalName);
}