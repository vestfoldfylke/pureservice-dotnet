using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.ActionModels;
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
        _logger.LogInformation("Creating EmailAddress {EmailAddress}", emailAddress);
        var result = await _pureserviceCaller.PostAsync<EmailAddress>($"{BasePath}", new AddEmailAddress([new NewEmailAddress(emailAddress)]));
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created EmailAddress {EmailAddress} with EmailAddressId {EmailAddressId}", emailAddress, result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressCreated", "Number of email addresses created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create EmailAddress {EmailAddress}", emailAddress);
        _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressCreated", "Number of email addresses created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    public async Task<bool> UpdateEmailAddress(int emailAddressId, string emailAddress, int userId)
    {
        _logger.LogInformation("Updating EmailAddressId {EmailAddressId} to {EmailAddress} for UserId {UserId}", emailAddressId, emailAddress);
        var result = await _pureserviceCaller.PutAsync($"{BasePath}/{emailAddressId}", new UpdateEmailAddress([new UpdateEmailAddressItem(emailAddressId, emailAddress, userId)]));

        if (result)
        {
            _logger.LogInformation("Successfully updated EmailAddressId {EmailAddressId} to {EmailAddress} for UserId {UserId}", emailAddressId, emailAddress);
            _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressUpdated", "Number of email addresses updated",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", emailAddressId, emailAddress);
        _metricsService.Count($"{Constants.MetricsPrefix}_EmailAddressUpdated", "Number of email addresses updated",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}