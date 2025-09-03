using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly IFintFolkService _fintFolkService;
    private readonly IGraphService _graphService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureserviceUserService _pureserviceUserService;

    private readonly string _studentEmailDomain;

    public UserFunctions(IConfiguration configuration, IFintFolkService fintFolkService, IGraphService graphService, ILogger<UserFunctions> logger,
        IMetricsService metricsService, IPureservicePhoneNumberService pureservicePhoneNumberService,
        IPureserviceUserService pureserviceUserService)
    {
        _fintFolkService = fintFolkService;
        _graphService = graphService;
        _logger = logger;
        _metricsService = metricsService;
        _pureservicePhoneNumberService = pureservicePhoneNumberService;
        _pureserviceUserService = pureserviceUserService;
        
        _studentEmailDomain = configuration.GetValue<string>("Student_Email_Domain") ?? throw new InvalidOperationException("Student_Email_Domain is not configured");
    }

    [Function("Synchronize")]
    [OpenApiOperation(operationId: "Synchronize")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key",
        In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(SynchronizationResult), Description = "Trigger finished")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "text/plain", typeof(string),
        Description = "Trigger failed")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "text/plain", typeof(string),
        Description = "Error occured")]
    public async Task<IActionResult> Synchronize([HttpTrigger(
        AuthorizationLevel.Function,
        "post",
        Route = "User/Synchronize")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        _metricsService.Count("UserFunctions_Synchronize");
        
        var userList = await _pureserviceUserService.GetUsers(["emailaddress", "phonenumbers"]);

        if (userList.Linked?.EmailAddresses is null || userList.Linked?.PhoneNumbers is null)
        {
            _logger.LogError("No linked email addresses or phone numbers found in user list");
            return new BadRequestObjectResult("No linked email addresses or phone numbers found in user list");
        }
        
        var synchronizationResult = new SynchronizationResult();

        foreach (var user in userList.Users)
        {
            _logger.LogInformation("Processing user '{DisplayName}' with UserId {UserId}", user.FullName, user.Id);
            
            if (user.ImportUniqueKey is null)
            {
                _logger.LogInformation("UserId {UserId} has no ImportUniqueKey, meaning they are created manually. Skipping", user.Id);
                synchronizationResult.UserMissingImportUniqueKeyCount++;
                continue;
            }

            if (user.Links.EmailAddress is null)
            {
                _logger.LogWarning("UserId {UserId} has no email address, cannot look up in source systems. Skipping", user.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }
            
            var primaryEmailAddress = userList.Linked.EmailAddresses.Find(e => e.Id == user.Links.EmailAddress.Id);

            if (primaryEmailAddress is null)
            {
                _logger.LogWarning("UserId {UserId} has no primary email address, cannot look up in source systems. Skipping", user.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }

            if (primaryEmailAddress.Email.Contains(_studentEmailDomain, StringComparison.InvariantCultureIgnoreCase))
            {
                var primaryPhoneNumber = user.Links.PhoneNumber is not null
                    ? userList.Linked.PhoneNumbers.Find(p => p.Id == user.Links.PhoneNumber.Id)
                    : null;

                synchronizationResult.StudentCount++;
                await HandleStudent(user, primaryEmailAddress, primaryPhoneNumber, synchronizationResult);
                continue;
            }

            synchronizationResult.EmployeeCount++;
            await HandleEmployee(user, primaryEmailAddress, synchronizationResult);
        }
        
        return new JsonResult(synchronizationResult);
    }

    private async Task HandleStudent(User user, EmailAddress emailAddress, PhoneNumber? phoneNumber, SynchronizationResult synchronizationResult)
    {
        var student = await _fintFolkService.GetStudent(emailAddress.Email);
        if (student is null)
        {
            _logger.LogWarning("No student found in FintFolk for Email '{Email}' on UserId {UserId}", emailAddress.Email, user.Id);
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }

        if (student.Mobilephone is null)
        {
            _logger.LogWarning("No phone number found for student with Email '{Email}' on UserId {UserId} in FintFolk", emailAddress.Email, user.Id);
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }

        if (phoneNumber is null)
        {
            var phoneNumberResult = await _pureservicePhoneNumberService.AddNewPhoneNumberAndLinkToUser(student.Mobilephone, PhoneNumberType.Mobile, user.Id);
            if (phoneNumberResult is null)
            {
                synchronizationResult.StudentPhoneNumberErrorCount++;
                return;
            }

            synchronizationResult.StudentPhoneNumberCreatedAndLinkedCount++;
            if (await _pureserviceUserService.RegisterPhoneNumberAsDefault(user.Id, phoneNumberResult.Id))
            {
                synchronizationResult.StudentPhoneNumberSetAsDefaultCount++;
                return;
            }
            
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }
        
        if (phoneNumber.Number == student.Mobilephone)
        {
            _logger.LogInformation("Phone number on student with UserId {UserId} is up to date", user.Id);
            synchronizationResult.StudentPhoneNumberUpToDateCount++;
            return;
        }
        
        if (await _pureservicePhoneNumberService.UpdatePhoneNumber(phoneNumber.Id, student.Mobilephone,
                PhoneNumberType.Mobile, user.Id))
        {
            synchronizationResult.StudentPhoneNumberUpdatedCount++;
            return;
        }
        
        synchronizationResult.StudentPhoneNumberErrorCount++;
    }
    
    private async Task HandleEmployee(User user, EmailAddress emailAddress, SynchronizationResult synchronizationResult)
    {
        var manager = await _graphService.GetEmployeeManager(emailAddress.Email);
        if (manager is null)
        {
            _logger.LogWarning("No employee found in Entra ID for Email '{Email}' or more likely no manager set on employee in Entra ID", emailAddress.Email);
            synchronizationResult.EmployeeManagerErrorCount++;
            return;
        }
        
        var managerUsers = await _pureserviceUserService.GetUser($"emailAddress.Email == \"{manager.Mail}\"");
        User? managerUser = null;
        
        switch (managerUsers.Users.Count)
        {
            case 0:
                _logger.LogWarning("No users found in Pureservice with email address '{ManagerMail}', cannot set manager for employee with UserId {UserId}", manager.Mail, user.Id);
                synchronizationResult.EmployeeManagerErrorCount++;
                return;
            case > 1:
            {
                var importedUsers = managerUsers.Users.FindAll(u => u.ImportUniqueKey is not null);
                switch (importedUsers.Count)
                {
                    case 0:
                        _logger.LogWarning("Multiple users ({UserCount}) found in Pureservice with email address '{ManagerMail}', none of them are imported. Cannot set manager for employee with UserId {UserId}", managerUsers.Users.Count, manager.Mail, user.Id);
                        synchronizationResult.EmployeeManagerErrorCount++;
                        return;
                    case > 1:
                        _logger.LogWarning("Multiple users ({UserCount}) found in Pureservice with email address '{ManagerMail}', {ImportedCount} of them are imported. Cannot determine which one to use as manager for employee with UserId {UserId}", managerUsers.Users.Count, manager.Mail, importedUsers.Count, user.Id);
                        synchronizationResult.EmployeeManagerErrorCount++;
                        return;
                    default:
                        managerUser = importedUsers[0];
                        _logger.LogInformation("Multiple users ({UserCount}) found in Pureservice with email address '{ManagerMail}', but only 1 is imported and will be used as manager for employee with UserId {UserId}", managerUsers.Users.Count, manager.Mail, user.Id);
                        break;
                }
                break;
            }
            case 1:
            {
                managerUser = managerUsers.Users[0];
                if (managerUser.ImportUniqueKey is null)
                {
                    _logger.LogWarning("Only 1 user found in Pureservice with email address '{ManagerMail}', but it is created manually. Cannot set manager for employee with UserId {UserId}", manager.Mail, user.Id);
                    synchronizationResult.EmployeeManagerErrorCount++;
                    return;
                }
                
                break;
            }
        }
        
        if (user.ManagerId == managerUser!.Id)
        {
            _logger.LogInformation("Manager on employee with UserId {UserId} is up to date with ManagerId {ManagerId}", user.Id, managerUser.Id);
            synchronizationResult.EmployeeManagerUpToDateCount++;
            return;
        }
        
        if (await _pureserviceUserService.UpdateManager(user.Id, managerUser.Id))
        {
            synchronizationResult.EmployeeManagerAddedCount++;
            return;
        }
        
        synchronizationResult.EmployeeManagerErrorCount++;
    }
}