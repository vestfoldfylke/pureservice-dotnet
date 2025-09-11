using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
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
        var payload = new
        {
            phonenumbers = new[]
            {
                new
                {
                    number = phoneNumber,
                    type = (int)type
                }
            }
        };
        
        _logger.LogInformation("Creating PhoneNumber {PhoneNumber}", phoneNumber);
        var result = await _pureserviceCaller.PostAsync<PhoneNumber>($"{BasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created PhoneNumber {PhoneNumber} with PhoneNumberId {PhoneNumberId}", phoneNumber, result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create PhoneNumber {PhoneNumber}: {@Payload}", phoneNumber, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<PhoneNumber?> AddNewPhoneNumberAndLinkToUser(string phoneNumber, PhoneNumberType type, int userId)
    {
        var payload = new
        {
            phonenumbers = new[]
            {
                new
                {
                    number = phoneNumber,
                    type = (int)type,
                    userId
                }
            }
        };
        
        _logger.LogInformation("Creating PhoneNumber {PhoneNumber} and linking to UserId {UserId}", phoneNumber, userId);
        var result = await _pureserviceCaller.PostAsync<PhoneNumber>($"{BasePath}", payload);
        
        if (result is not null)
        {
            _logger.LogInformation("Successfully created PhoneNumber {PhoneNumber} with PhoneNumberId {PhoneNumberId} and linked to UserId {UserId}", phoneNumber, result.Id, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberCreated", "Number of phone numbers created",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create PhoneNumber {PhoneNumber} with link to UserId {UserId}: {@Payload}", phoneNumber, userId, payload);
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
        var payload = new
        {
            phonenumbers = new[]
            {
                new
                {
                    id = phoneNumberId,
                    number = phoneNumber,
                    type = (int)type,
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
        
        _logger.LogInformation("Updating PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", phoneNumberId, phoneNumber, userId);
        var result = await _pureserviceCaller.PutAsync<Linked>($"{BasePath}/{phoneNumberId}", payload);

        if (result is not null)
        {
            _logger.LogInformation("Successfully updated PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}", phoneNumberId, phoneNumber, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberUpdated", "Number of phone numbers updated",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update PhoneNumberId {PhoneNumberId} to {PhoneNumber} for UserId {UserId}: {@Payload}", phoneNumberId, phoneNumber, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_PhoneNumberUpdated", "Number of phone numbers updated",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
}