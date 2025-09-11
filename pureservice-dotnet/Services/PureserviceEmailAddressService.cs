using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceEmailAddressService
{
    Task<EmailAddress?> AddNewEmailAddress(string emailAddress);
    Task<bool> UpdateEmailAddress(int emailAddressId, string emailAddress, int userId);
}

public class PureserviceEmailAddressService : IPureserviceEmailAddressService
{
    private readonly ILogger<PureserviceEmailAddressService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "emailaddress";
    
    public PureserviceEmailAddressService(ILogger<PureserviceEmailAddressService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<EmailAddress?> AddNewEmailAddress(string emailAddress)
    {
        var payload = new
        {
            emailaddresses = new[]
            {
                new
                {
                    email = emailAddress
                }
            }
        };
        
        _logger.LogInformation("Creating EmailAddress {EmailAddress}", emailAddress);
        var result = await _pureserviceCaller.PostAsync<EmailAddress>($"{BasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created EmailAddress {EmailAddress} with EmailAddressId {EmailAddressId}", emailAddress, result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressCreated", "Number of email addresses created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create EmailAddress {EmailAddress}: {@Payload}", emailAddress, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressCreated", "Number of email addresses created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<bool> UpdateEmailAddress(int emailAddressId, string emailAddress, int userId)
    {
        var payload = new
        {
            emailaddresses = new[]
            {
                new
                {
                    id = emailAddressId,
                    email = emailAddress,
                    links = new
                    {
                        user = new
                        {
                            id = userId
                        }
                    }
                }
            }
        };
        
        _logger.LogInformation("Updating EmailAddressId {EmailAddressId} to {EmailAddress} for UserId {UserId}", emailAddressId, emailAddress, userId);
        var result = await _pureserviceCaller.PutAsync($"{BasePath}/{emailAddressId}", payload);

        if (result)
        {
            _logger.LogInformation("Successfully updated EmailAddressId {EmailAddressId} to {EmailAddress} for UserId {UserId}", emailAddressId, emailAddress, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressUpdated", "Number of email addresses updated",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update EmailAddressId {EmailAddressId} to {EmailAddress} for UserId {UserId}: {@Payload}", emailAddressId, emailAddress, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressUpdated", "Number of email addresses updated",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}