using System.Threading.Tasks;

namespace pureservice_dotnet.Services;

public interface IPureserviceCaller
{
    Task<T?> GetAsync<T>(string endpoint) where T : class;
    Task<bool> PatchAsync(string endpoint, object payload);
    Task<T?> PatchAsync<T>(string endpoint, object payload) where T : class;
    Task<T?> PutAsync<T>(string endpoint, object payload) where T : class;
}