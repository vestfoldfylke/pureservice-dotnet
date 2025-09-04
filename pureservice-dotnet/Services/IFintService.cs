using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace pureservice_dotnet.Services;

public interface IFintService
{
    Task<JsonNode?> GetStudent(string userPrincipalName);
}