using System;
using System.Collections.Generic;
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
    private readonly IFintService _fintService;
    private readonly IGraphService _graphService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureserviceUserService _pureserviceUserService;

    private readonly string _studentEmailDomain;

    public UserFunctions(IConfiguration configuration, IFintService fintService, IGraphService graphService, ILogger<UserFunctions> logger,
        IMetricsService metricsService, IPureservicePhoneNumberService pureservicePhoneNumberService,
        IPureserviceUserService pureserviceUserService)
    {
        _fintService = fintService;
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

        var entraEmployees = await _graphService.GetEmployees();

        var pureserviceUsers = await _pureserviceUserService.GetUsers(["emailaddress", "phonenumbers"]);

        if (pureserviceUsers.Linked?.EmailAddresses is null || pureserviceUsers.Linked?.PhoneNumbers is null)
        {
            _logger.LogError("No linked email addresses or phone numbers found in user list");
            return new BadRequestObjectResult("No linked email addresses or phone numbers found in user list");
        }

        var synchronizationResult = new SynchronizationResult();

        foreach (var pureserviceUser in pureserviceUsers.Users)
        {
            _logger.LogInformation("Processing user '{DisplayName}' with UserId {UserId}", pureserviceUser.FullName, pureserviceUser.Id);

            if (pureserviceUser.ImportUniqueKey is null)
            {
                _logger.LogInformation("UserId {UserId} has no ImportUniqueKey, meaning they are created manually. Skipping", pureserviceUser.Id);
                synchronizationResult.UserMissingImportUniqueKeyCount++;
                continue;
            }

            if (pureserviceUser.Links.EmailAddress is null)
            {
                _logger.LogWarning("UserId {UserId} has no email address, cannot look up in source systems. Skipping", pureserviceUser.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }

            var primaryEmailAddress = pureserviceUsers.Linked.EmailAddresses.Find(e => e.Id == pureserviceUser.Links.EmailAddress.Id);

            if (primaryEmailAddress is null)
            {
                _logger.LogWarning("UserId {UserId} has no primary email address, cannot look up in source systems. Skipping", pureserviceUser.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }

            if (primaryEmailAddress.Email.Contains(_studentEmailDomain, StringComparison.InvariantCultureIgnoreCase))
            {
                var primaryPhoneNumber = pureserviceUser.Links.PhoneNumber is not null
                    ? pureserviceUsers.Linked.PhoneNumbers.Find(p => p.Id == pureserviceUser.Links.PhoneNumber.Id)
                    : null;

                synchronizationResult.StudentCount++;
                await HandleStudent(pureserviceUser, primaryEmailAddress, primaryPhoneNumber, synchronizationResult);
                continue;
            }

            synchronizationResult.EmployeeCount++;
            await HandleEmployee(pureserviceUser, entraEmployees, pureserviceUsers, synchronizationResult);
        }

        return new JsonResult(synchronizationResult);
    }

    private async Task HandleStudent(User pureserviceUser, EmailAddress emailAddress, PhoneNumber? phoneNumber, SynchronizationResult synchronizationResult)
    {
        var student = await _fintService.GetStudent(emailAddress.Email);

        var studentEntry = student?["data"]?["elev"];
        if (studentEntry is null)
        {
            _logger.LogWarning("No student found in FINT for Email '{Email}' on UserId {UserId}", emailAddress.Email, pureserviceUser.Id);
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }

        var studentMobilePhone = studentEntry["kontaktinformasjon"]?["mobiltelefonnummer"]?.ToString() ??
                                 studentEntry["person"]?["kontaktinformasjon"]?["mobiltelefonnummer"]?.ToString() ??
                                 null;
        if (studentMobilePhone is null)
        {
            _logger.LogWarning("No phone number found for student with Email '{Email}' on UserId {UserId} in FINT", emailAddress.Email, pureserviceUser.Id);
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }

        if (phoneNumber is null)
        {
            var phoneNumberResult = await _pureservicePhoneNumberService.AddNewPhoneNumberAndLinkToUser(studentMobilePhone, PhoneNumberType.Mobile, pureserviceUser.Id);
            if (phoneNumberResult is null)
            {
                synchronizationResult.StudentPhoneNumberErrorCount++;
                return;
            }

            synchronizationResult.StudentPhoneNumberCreatedAndLinkedCount++;
            if (await _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, phoneNumberResult.Id))
            {
                synchronizationResult.StudentPhoneNumberSetAsDefaultCount++;
                return;
            }
            
            synchronizationResult.StudentPhoneNumberErrorCount++;
            return;
        }
        
        if (phoneNumber.Number == studentMobilePhone)
        {
            _logger.LogInformation("Phone number on student with UserId {UserId} is up to date", pureserviceUser.Id);
            synchronizationResult.StudentPhoneNumberUpToDateCount++;
            return;
        }
        
        if (await _pureservicePhoneNumberService.UpdatePhoneNumber(phoneNumber.Id, studentMobilePhone,
                PhoneNumberType.Mobile, pureserviceUser.Id))
        {
            synchronizationResult.StudentPhoneNumberUpdatedCount++;
            return;
        }
        
        synchronizationResult.StudentPhoneNumberErrorCount++;
    }
    
    private async Task HandleEmployee(User pureserviceUser, List<Microsoft.Graph.Models.User> entraEmployees, UserList pureserviceUsers, SynchronizationResult synchronizationResult)
    {
        var entraUser = entraEmployees.Find(employee =>
            !string.IsNullOrEmpty(employee.Id) && employee.Id == pureserviceUser.ImportUniqueKey);
        if (entraUser is null)
        {
            _logger.LogError("UserId {UserId} not found in Entra ID by ImportUniqueKey {ImportUniqueKey}. Skipping", pureserviceUser.Id, pureserviceUser.ImportUniqueKey);
            synchronizationResult.UserMissing++;
            return;
        }

        if (string.IsNullOrEmpty(entraUser.Manager?.Id))
        {
            _logger.LogWarning("UserId {UserId} has no manager set in Entra ID, cannot synchronize manager. Skipping", pureserviceUser.Id);
            synchronizationResult.EmployeeManagerMissingInEntraCount++;
            return;
        }
            
        var entraManagerUser = entraEmployees.Find(employee => !string.IsNullOrEmpty(employee.Id) && employee.Id == entraUser.Manager.Id);
        if (entraManagerUser is null)
        {
            _logger.LogError("Manager with Id {ManagerId} not found in Entra ID, cannot synchronize manager. Skipping", entraUser.Manager.Id);
            synchronizationResult.EmployeeManagerMissingInEntraCount++;
            return;
        }
        
        var pureserviceManagerUser = pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Manager.Id);
        if (pureserviceManagerUser is null)
        {
            _logger.LogWarning("No users found in Pureservice with ImportUniqueKey '{ManagerId}', cannot set manager for employee with UserId {UserId}", entraUser.Manager.Id, pureserviceUser.Id);
            synchronizationResult.EmployeeManagerErrorCount++;
            return;
        }
        
        if (pureserviceUser.ManagerId == pureserviceManagerUser.Id)
        {
            _logger.LogInformation("Manager on employee with UserId {UserId} is up to date with ManagerId {ManagerId}", pureserviceUser.Id, pureserviceUser.ManagerId);
            synchronizationResult.EmployeeManagerUpToDateCount++;
            return;
        }
        
        if (await _pureserviceUserService.UpdateManager(pureserviceUser.Id, pureserviceManagerUser.Id))
        {
            synchronizationResult.EmployeeManagerAddedCount++;
            return;
        }
        
        synchronizationResult.EmployeeManagerErrorCount++;
    }
}