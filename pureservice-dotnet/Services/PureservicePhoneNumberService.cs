using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.ActionModels;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureservicePhoneNumberService
{
    Task<PhoneNumber?> AddNewPhoneNumber(string phoneNumber, PhoneNumberType type);
    Task<PhoneNumber?> AddNewPhoneNumberAndLinkToUser(string phoneNumber, PhoneNumberType type, int userId);
    (bool Update, string? PhoneNumber) NeedsPhoneNumberUpdate(PhoneNumber? phoneNumber, string? entraPhoneNumber);
    Task<bool> UpdatePhoneNumber(int phoneNumberId, string? phoneNumber, PhoneNumberType type, int userId);
}

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
    
    public async Task<PhoneNumber?> AddNewPhoneNumber(string phoneNumber, PhoneNumberType type)
    {
        _logger.LogInformation("Creating PhoneNumber {PhoneNumber}", phoneNumber);
        var phoneNumberResult = await _pureserviceCaller.PostAsync<PhoneNumber>($"{BasePath}", new AddPhoneNumber([new NewPhoneNumber(phoneNumber, type)]));
        
        if (phoneNumberResult is not null)
        {
            _logger.LogInformation("Successfully created PhoneNumber {PhoneNumber} with PhoneNumberId {PhoneNumberId}", phoneNumber, phoneNumberResult.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return phoneNumberResult;
        }
        
        _logger.LogError("Failed to create PhoneNumber {PhoneNumber}", phoneNumber);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<PhoneNumber?> AddNewPhoneNumberAndLinkToUser(string phoneNumber, PhoneNumberType type, int userId)
    {
        _logger.LogInformation("Creating PhoneNumber {PhoneNumber} and linking to UserId {UserId}", phoneNumber, userId);
        var phoneNumberResult = await _pureserviceCaller.PostAsync<PhoneNumber>($"{BasePath}", new AddPhoneNumberWithUser([new NewPhoneNumberWithUser(phoneNumber, type, userId)]));
        
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

    public (bool Update, string? PhoneNumber) NeedsPhoneNumberUpdate(PhoneNumber? phoneNumber, string? entraPhoneNumber)
    {
        if (phoneNumber is null)
        {
            return (entraPhoneNumber is not null, entraPhoneNumber);
        }
        
        if (entraPhoneNumber is null)
        {
            return (true, null);
        }
        
        return (phoneNumber.Number != entraPhoneNumber, entraPhoneNumber);
    }

    public async Task<bool> UpdatePhoneNumber(int phoneNumberId, string? phoneNumber, PhoneNumberType type, int userId)
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