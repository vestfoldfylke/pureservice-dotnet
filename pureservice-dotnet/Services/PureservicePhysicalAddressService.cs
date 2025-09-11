using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureservicePhysicalAddressService
{
    Task<PhysicalAddress?> AddNewPhysicalAddress(string? streetAddress, string? city, string? postalCode, string? country);
    Task<bool> UpdatePhysicalAddress(int physicalAddressId, string? streetAddress, string? city, string? postalCode, string? country);
}

public class PureservicePhysicalAddressService : IPureservicePhysicalAddressService
{
    private readonly ILogger<PureservicePhysicalAddressService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "physicaladdress";
    
    public PureservicePhysicalAddressService(ILogger<PureservicePhysicalAddressService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<PhysicalAddress?> AddNewPhysicalAddress(string? streetAddress, string? city, string? postalCode, string? country)
    {
        var payload = new
        {
            physicaladdresses = new[]
            {
                new
                {
                    streetAddress,
                    city,
                    postalCode,
                    country
                }
            }
        };
        
        _logger.LogInformation("Creating physical address");
        var result = await _pureserviceCaller.PostAsync<PhysicalAddress>($"{BasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created physical address with PhysicalAddressId {PhysicalAddressId}", result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhysicalAddressCreated", "Number of physical addresses created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create physical address: {@Payload}", payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhysicalAddressCreated", "Number of physical addresses created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<bool> UpdatePhysicalAddress(int physicalAddressId, string? streetAddress, string? city, string? postalCode, string? country)
    {
        var payload = new
        {
            physicaladdresses = new[]
            {
                new
                {
                    id = physicalAddressId,
                    streetAddress,
                    city,
                    postalCode,
                    country
                }
            }
        };
        
        _logger.LogInformation("Updating PhysicalAddressId {PhysicalAddressId}", physicalAddressId);
        var result = await _pureserviceCaller.PutAsync($"{BasePath}/{physicalAddressId}", payload);

        if (result)
        {
            _logger.LogInformation("Successfully updated PhysicalAddressId {PhysicalAddressId}", physicalAddressId);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhysicalAddressUpdated", "Number of physical addresses updated",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update PhysicalAddressId {PhysicalAddressId}: {@Payload}", physicalAddressId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhysicalAddressUpdated", "Number of physical addresses updated",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}