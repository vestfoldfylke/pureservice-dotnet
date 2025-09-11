using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;
using Serilog.Context;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly IGraphService _graphService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceEmailAddressService _pureserviceEmailAddressService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureservicePhysicalAddressService _pureservicePhysicalAddressService;
    private readonly IPureserviceUserService _pureserviceUserService;

    public UserFunctions(IGraphService graphService, ILogger<UserFunctions> logger, IMetricsService metricsService, IPureserviceEmailAddressService pureserviceEmailAddressService,
        IPureservicePhoneNumberService pureservicePhoneNumberService, IPureservicePhysicalAddressService pureservicePhysicalAddressService, IPureserviceUserService pureserviceUserService)
    {
        _graphService = graphService;
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceEmailAddressService = pureserviceEmailAddressService;
        _pureservicePhoneNumberService = pureservicePhoneNumberService;
        _pureservicePhysicalAddressService = pureservicePhysicalAddressService;
        _pureserviceUserService = pureserviceUserService;
    }

    [Function("Synchronize")]
    [OpenApiOperation(operationId: "Synchronize")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(SynchronizationResult), Description = "Trigger finished")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "text/plain", typeof(string), Description = "Trigger failed")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "text/plain", typeof(string), Description = "Error occured")]
    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    public async Task<IActionResult> Synchronize([HttpTrigger(AuthorizationLevel.Function, "post", Route = "User/Synchronize")] HttpRequest req)
    {
        _logger.LogInformation("Starting UserFunctions_Synchronize");
        using var _ = _metricsService.Histogram($"{Constants.MetricsPrefix}_UserFunctions_Synchronize", "Duration of UserFunctions_Synchronize in seconds");

        var entraEmployees = await _graphService.GetEmployees();
        var entraStudents = await _graphService.GetStudents();
        var entraUsers = entraEmployees.Concat(entraStudents)
            .Where(u => !string.IsNullOrEmpty(u.Id))
            .DistinctBy(u => u.Id)
            .ToList();
        
        _logger.LogInformation("Retrieved {EmployeeCount} employees, {StudentCount} students, total {TotalCount} from Entra", entraEmployees.Count, entraStudents.Count, entraUsers.Count);
        
        var pureserviceUsers = await _pureserviceUserService.GetUsers(["company", "company.departments", "company.locations", "emailaddress", "language", "phonenumbers"]);

        if (pureserviceUsers.Linked?.Companies is null || pureserviceUsers.Linked?.CompanyDepartments is null || pureserviceUsers.Linked?.CompanyLocations is null ||
            pureserviceUsers.Linked?.EmailAddresses is null || pureserviceUsers.Linked?.Languages is null || pureserviceUsers.Linked?.PhoneNumbers is null)
        {
            _logger.LogError("Expected linked results were not found in user list");
            return new BadRequestObjectResult("Expected linked results were not found in user list");
        }

        var synchronizationResult = new SynchronizationResult();
        
        var companies = pureserviceUsers.Linked.Companies;
        var departments = pureserviceUsers.Linked.CompanyDepartments;
        var locations = pureserviceUsers.Linked.CompanyLocations;
        
        // create or update users
        foreach (var entraUser in entraUsers)
        {
            using (LogContext.PushProperty("EntraId", entraUser.Id))
            {
                _logger.LogInformation("Processing Entra user '{DisplayName}' with EntraId {EntraId}", entraUser.DisplayName, entraUser.Id);

                var (pureserviceUser, pureserviceManagerUser, skipUser) = GetPureserviceUserInfo(entraUser, pureserviceUsers, synchronizationResult);
                
                if (skipUser)
                {
                    continue;
                }

                if (pureserviceUser is null)
                {
                    if (entraUser.AccountEnabled.HasValue && !entraUser.AccountEnabled.Value)
                    {
                        _logger.LogInformation("Entra user with Id {EntraId} is disabled in Entra. Skipping creation in Pureservice");
                        synchronizationResult.UserDisabledCount++;
                        continue;
                    }

                    var company = companies.Find(c => c.Name.Equals(entraUser.CompanyName, StringComparison.OrdinalIgnoreCase));
                    if (company is null)
                    {
                        // TODO: Company needs to be created?
                        _logger.LogError("CompanyName {CompanyName} for new pureservice user with EntraId {EntraId} not found in Pureservice. User will not be created", entraUser.CompanyName, entraUser.Id);
                        synchronizationResult.UserErrorCount++;
                        continue;
                    }

                    var department = entraUser.Department is not null
                        ? departments.Find(d => d.Name.Equals(entraUser.Department, StringComparison.OrdinalIgnoreCase) && d.CompanyId == company.Id)
                        : null;

                    var location = entraUser.OfficeLocation is not null
                        ? locations.Find(l => l.Name.Equals(entraUser.OfficeLocation, StringComparison.OrdinalIgnoreCase) && l.CompanyId == company.Id)
                        : null;

                    await CreateUser(entraUser, pureserviceManagerUser, company.Id, department, location, synchronizationResult);
                    continue;
                }
                
                using (LogContext.PushProperty("UserId", pureserviceUser.Id))
                {
                    var (primaryEmailAddress, primaryPhoneNumber, phoneNumberIds) = GetPureserviceUserContactInfo(pureserviceUser, pureserviceUsers, synchronizationResult);
                    if (primaryEmailAddress is null)
                    {
                        continue;
                    }
                    
                    var phoneNumbers = pureserviceUsers.Linked.PhoneNumbers.Where(p => phoneNumberIds.Contains(p.Id)).ToList();

                    synchronizationResult.UserCount++;
                    await UpdateUser(pureserviceUser, entraUser, primaryEmailAddress, primaryPhoneNumber, phoneNumbers, pureserviceManagerUser, companies, departments, locations, synchronizationResult);
                }
            }
        }
        
        // TODO: Loop through pureservice users not existing in entra and disable them (should we anonymize them as well, if so, how?)

        _logger.LogInformation("UserFunctions_Synchronize finished: {@SynchronizationResult}", synchronizationResult);
        
        return new JsonResult(synchronizationResult);
    }

    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    private (User? pureserviceUser, User? pureserviceManagerUser, bool skipUser) GetPureserviceUserInfo(Microsoft.Graph.Models.User entraUser, UserList pureserviceUsers, SynchronizationResult synchronizationResult)
    {
        if (entraUser.Mail is null)
        {
            _logger.LogError("Entra user with Id {EntraId} has no email address. Skipping");
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, true);
        }

        if (entraUser.CompanyName is null)
        {
            _logger.LogError("Entra user with Id {EntraId} has no company name. Skipping");
            synchronizationResult.UserMissingCompanyNameCount++;
            return (null, null, true);
        }
            
        var pureserviceUser = pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Id);
            
        var pureserviceManagerUser = entraUser.Manager?.Id is not null
            ? pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Manager.Id)
            : null;
        
        return (pureserviceUser, pureserviceManagerUser, false);
    }

    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    private (EmailAddress? primaryEmailAddress, PhoneNumber? primaryPhoneNumber, List<int> phoneNumberIds) GetPureserviceUserContactInfo(User pureserviceUser, UserList pureserviceUsers, SynchronizationResult synchronizationResult)
    {
        if (pureserviceUser.Links is null)
        {
            _logger.LogError("UserId {UserId} has no links. Skipping");
            synchronizationResult.UserErrorCount++;
            return (null, null, []);
        }

        if (pureserviceUser.Links.EmailAddress is null)
        {
            _logger.LogError("UserId {UserId} has no email address. Skipping");
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, []);
        }

        var primaryEmailAddress = pureserviceUsers.Linked!.EmailAddresses!.Find(e => e.Id == pureserviceUser.Links.EmailAddress.Id);

        if (primaryEmailAddress is null)
        {
            _logger.LogError("EmailAddressId {EmailAddressId} for UserId {UserId} not found in Pureservice. Skipping", pureserviceUser.Links.EmailAddress.Id, pureserviceUser.Id);
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, []);
        }
            
        var primaryPhoneNumber = pureserviceUser.Links.PhoneNumber is not null
            ? pureserviceUsers.Linked!.PhoneNumbers!.Find(p => p.Id == pureserviceUser.Links.PhoneNumber.Id)
            : null;

        var phoneNumberIds = pureserviceUser.Links.PhoneNumbers?.Ids ?? [];
        
        return (primaryEmailAddress, primaryPhoneNumber, phoneNumberIds);
    }

    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    private async Task CreateUser(Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser, int companyId, CompanyDepartment? department, CompanyLocation? location,
        SynchronizationResult synchronizationResult)
    {
        _logger.LogWarning("Entra user with Id {EntraId} not found in Pureservice by ImportUniqueKey. User will be created");
        
        // NOTE: Create new physical address (with empty fields since we don't need that info in Pureservice for now)
        var physicalAddressResult = await _pureservicePhysicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway");
        if (physicalAddressResult is null)
        {
            _logger.LogError("Failed to create physical address for new pureservice user with EntraId {EntraId}. User will not be created");
            synchronizationResult.UserErrorCount++;
            return;
        }
                
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var pureservicePhoneNumber = await _pureservicePhoneNumberService.AddNewPhoneNumber(entraPhoneNumber ?? "", PhoneNumberType.Mobile);
        if (pureservicePhoneNumber is null)
        {
            _logger.LogError("Failed to create phone number for new pureservice user with EntraId {EntraId} and PhoneNumber {PhoneNumber}. User will not be created", entraUser.Id, entraPhoneNumber);
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: We create the user with UserPrincipalName as email address so SSO will work. If Mail actually is different, it will be updated at next sweep.
        var pureserviceEmailAddress = await _pureserviceEmailAddressService.AddNewEmailAddress(entraUser.UserPrincipalName!);
        if (pureserviceEmailAddress is null)
        {
            _logger.LogError("Failed to create email address for new pureservice user with EntraId {EntraId} and Email {Email}. User will not be created", entraUser.Id, entraUser.Mail);
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: Create new user with ids for above created entities
        var pureserviceUser = await _pureserviceUserService.CreateNewUser(entraUser, pureserviceManagerUser?.Id, companyId, physicalAddressResult.Id, pureservicePhoneNumber.Id, pureserviceEmailAddress.Id);

        if (pureserviceUser is null)
        {
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: Update department and location
        // TODO: Check department and location separately and update only if needed
        if ((department is not null || location is not null) && await _pureserviceUserService.UpdateDepartmentAndLocation(pureserviceUser.Id, department?.Id, location?.Id))
        {
            synchronizationResult.UserCreatedCount++;
        }
    }
    
    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    private async Task UpdateUser(User pureserviceUser, Microsoft.Graph.Models.User entraUser, EmailAddress emailAddress, PhoneNumber? phoneNumber, List<PhoneNumber> phoneNumbers,
        User? pureserviceManagerUser, List<Company> companies, List<CompanyDepartment> companyDepartments, List<CompanyLocation> companyLocations, SynchronizationResult synchronizationResult)
    {
        var basicPropertiesToUpdate = _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser);
        
        var companyPropertiesToUpdate = _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, companyDepartments, companyLocations);
        
        var updateEmail = !emailAddress.Email.Equals(entraUser.Mail, StringComparison.OrdinalIgnoreCase);
        
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var phoneNumberUpdate = _pureservicePhoneNumberService.NeedsPhoneNumberUpdate(phoneNumber, entraPhoneNumber);
        
        if (basicPropertiesToUpdate.Count == 0 && companyPropertiesToUpdate.Count == 0 && !updateEmail && !phoneNumberUpdate.Update)
        {
            synchronizationResult.UserUpToDateCount++;
            _logger.LogInformation("User with UserId {UserId} is up to date");
            return;
        }
        
        if (basicPropertiesToUpdate.Count > 0 && await _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, basicPropertiesToUpdate))
        {
            synchronizationResult.UserBasicPropertiesUpdatedCount++;
        }
        
        if (companyPropertiesToUpdate.Count > 0 && await _pureserviceUserService.UpdateCompanyProperties(pureserviceUser.Id, companyPropertiesToUpdate))
        {
            synchronizationResult.UserCompanyPropertiesUpdatedCount++;
        }

        if (updateEmail && await _pureserviceEmailAddressService.UpdateEmailAddress(emailAddress.Id, entraUser.Mail!, pureserviceUser.Id))
        {
            synchronizationResult.UserEmailAddressUpdatedCount++;
        }

        if (!phoneNumberUpdate.Update)
        {
            return;
        }
        
        if (phoneNumber is null)
        {
            if (phoneNumberUpdate.PhoneNumber is null)
            {
                _logger.LogError("No phone number exists for pureservice UserId {UserId} and no phone number found in Entra on EntraId {EntraId}. Cannot add empty phone number", pureserviceUser.Id, entraUser.Id);
                return;
            }
            
            phoneNumber = phoneNumbers.Find(p => p.Number == phoneNumberUpdate.PhoneNumber);
            if (phoneNumber is not null)
            {
                if (await _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, phoneNumber.Id))
                {
                    synchronizationResult.UserPhoneNumberUpdatedCount++;
                    return;
                }

                synchronizationResult.UserErrorCount++;
                return;
            }

            var phoneNumberResult = await _pureservicePhoneNumberService.AddNewPhoneNumberAndLinkToUser(phoneNumberUpdate.PhoneNumber, PhoneNumberType.Mobile, pureserviceUser.Id);
            if (phoneNumberResult is null)
            {
                synchronizationResult.UserErrorCount++;
                return;
            }

            if (await _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, phoneNumberResult.Id))
            {
                synchronizationResult.UserPhoneNumberUpdatedCount++;
                return;
            }

            synchronizationResult.UserErrorCount++;
            return;
        }

        if (await _pureservicePhoneNumberService.UpdatePhoneNumber(phoneNumber.Id, phoneNumberUpdate.PhoneNumber, PhoneNumberType.Mobile, pureserviceUser.Id))
        {
            synchronizationResult.UserPhoneNumberUpdatedCount++;
            return;
        }
        
        synchronizationResult.UserErrorCount++;
    }
}