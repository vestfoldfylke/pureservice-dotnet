using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.ActionModels;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public class PureservicePhoneNumberService : IPureservicePhoneNumberService
{
    private readonly ILogger<PureservicePhoneNumberService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "phonenumber";
    
    public PureservicePhoneNumberService(ILogger<PureservicePhoneNumberService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<PhoneNumber?> AddNewPhoneNumberAndLinkToUser(string phoneNumber, PhoneNumberType type, int userId)
    {
        _logger.LogInformation("Creating PhoneNumber {PhoneNumber} and linking to UserId {UserId}", phoneNumber, userId);
        var phoneNumberResult = await _pureserviceCaller.PostAsync<PhoneNumber>($"{BasePath}", new AddPhoneNumber([new NewPhoneNumber(phoneNumber, type, userId)]));
        
        if (phoneNumberResult is not null)
        {
            _logger.LogInformation("Successfully created PhoneNumber {PhoneNumber} with PhoneNumberId {PhoneNumberId} and linked to UserId {UserId}", phoneNumber, phoneNumberResult.Id, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return phoneNumberResult;
        }
        
        _logger.LogError("Failed to create PhoneNumber {PhoneNumber} with link to UserId {UserId}", phoneNumber, userId);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<bool> UpdatePhoneNumber(int phoneNumberId, string phoneNumber, PhoneNumberType type, int userId)
    {
        _logger.LogInformation("Updating PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", phoneNumberId, phoneNumber, userId);
        var phoneNumberResult = await _pureserviceCaller.PutAsync<Linked>($"{BasePath}/{phoneNumberId}", new UpdatePhoneNumber([new UpdatePhoneNumberItem(phoneNumberId, phoneNumber, type, userId)]));

        if (phoneNumberResult is not null)
        {
            _logger.LogInformation("Successfully updated PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", phoneNumberId, phoneNumber, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberUpdated", "Number of phone numbers updated",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", phoneNumberId, phoneNumber, userId);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberUpdated", "Number of phone numbers updated",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}