using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IGraphService _graphService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceEmailAddressService _pureserviceEmailAddressService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureservicePhysicalAddressService _pureservicePhysicalAddressService;
    private readonly IPureserviceUserService _pureserviceUserService;

    private readonly string _studentEmailDomain;

    public UserFunctions(IConfiguration configuration, IGraphService graphService, ILogger<UserFunctions> logger,
        IMetricsService metricsService, IPureserviceEmailAddressService pureserviceEmailAddressService,
        IPureservicePhoneNumberService pureservicePhoneNumberService,
        IPureservicePhysicalAddressService pureservicePhysicalAddressService, IPureserviceUserService pureserviceUserService)
    {
        _graphService = graphService;
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceEmailAddressService = pureserviceEmailAddressService;
        _pureservicePhoneNumberService = pureservicePhoneNumberService;
        _pureservicePhysicalAddressService = pureservicePhysicalAddressService;
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
        var entraStudents = await _graphService.GetStudents();
        var entraUsers = entraEmployees.Concat(entraStudents)
            .Where(u => !string.IsNullOrEmpty(u.Id))
            .DistinctBy(u => u.Id)
            .ToList();
        
        var pureserviceUsers = await _pureserviceUserService.GetUsers(["company", "company.departments", "company.locations", "emailaddress", "language", "phonenumbers"]);

        if (pureserviceUsers.Linked?.Companies is null || pureserviceUsers.Linked?.CompanyDepartments is null ||
            pureserviceUsers.Linked?.CompanyLocations is null || pureserviceUsers.Linked?.EmailAddresses is null ||
            pureserviceUsers.Linked?.Languages is null || pureserviceUsers.Linked?.PhoneNumbers is null)
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
            _logger.LogInformation("Processing Entra user '{DisplayName}' with Id {Id}", entraUser.DisplayName, entraUser.Id);

            if (entraUser.Mail is null)
            {
                _logger.LogError("Entra user with Id {Id} has no email address. Skipping", entraUser.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }
            
            var pureserviceUser = pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Id);
            
            var pureserviceManagerUser = entraUser.Manager?.Id is not null
                ? pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Manager.Id)
                : null;
            
            if (pureserviceUser is null)
            {
                var company = entraUser.CompanyName is not null
                    ? companies.Find(c => c.Name.Equals(entraUser.CompanyName, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (company is null)
                {
                    // TODO: Company needs to be created?
                    _logger.LogError("Company {CompanyName} for new user with Entra Id {EntraId} not found in Pureservice. User will not be created",
                        entraUser.CompanyName, entraUser.Id);
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

            if (pureserviceUser.Links.EmailAddress is null)
            {
                _logger.LogError("UserId {UserId} has no email address. Skipping", pureserviceUser.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }

            var primaryEmailAddress =
                pureserviceUsers.Linked.EmailAddresses.Find(e => e.Id == pureserviceUser.Links.EmailAddress.Id);

            if (primaryEmailAddress is null)
            {
                _logger.LogError("EmailAddressId {EmailAddressId} for UserId {UserId} not found in Pureservice. Skipping", pureserviceUser.Links.EmailAddress.Id, pureserviceUser.Id);
                synchronizationResult.UserMissingEmailAddressCount++;
                continue;
            }
            
            var primaryPhoneNumber = pureserviceUser.Links.PhoneNumber is not null
                ? pureserviceUsers.Linked.PhoneNumbers.Find(p => p.Id == pureserviceUser.Links.PhoneNumber.Id)
                : null;

            synchronizationResult.UserCount++;
            await UpdateUser(pureserviceUser, entraUser, primaryEmailAddress, primaryPhoneNumber, pureserviceManagerUser, companies, departments, locations, synchronizationResult);
        }
        
        // TODO: Loop through pureservice users not existing in entra and disable them?

        return new JsonResult(synchronizationResult);
    }

    private async Task CreateUser(Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser,
        int companyId, CompanyDepartment? department, CompanyLocation? location, SynchronizationResult synchronizationResult)
    {
        _logger.LogWarning("User with Id {UserId} not found in Pureservice by ImportUniqueKey. User will be created", entraUser.Id);
        
        if (entraUser.Manager?.Id is not null && pureserviceManagerUser is null)
        {
            _logger.LogError("Manager with Entra Id {EntraManagerId} for new user with Entra Id {EntraId} not found in Pureservice. Manager user must be created first.",
                entraUser.Manager.Id, entraUser.Id);
            synchronizationResult.UserErrorCount++;
            return;
        }
        
        // NOTE: Create new physical address (with empty fields since we don't need that info in Pureservice for now)
        var physicalAddressResult =
            await _pureservicePhysicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway");
                
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var pureservicePhoneNumber = entraPhoneNumber is not null
            ? await _pureservicePhoneNumberService.AddNewPhoneNumber(entraPhoneNumber, PhoneNumberType.Mobile)
            : null;

        var pureserviceEmailAddress = await _pureserviceEmailAddressService.AddNewEmailAddress(entraUser.Mail!);
        if (pureserviceEmailAddress is null)
        {
            _logger.LogError("Failed to create email address for new user with Entra Id {EntraId} and email {Email}. User will not be created",
                entraUser.Id, entraUser.Mail);
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: Create new user with ids for above created entities (include required fields to get needed info)
        var pureserviceUserList = await _pureserviceUserService.CreateNewUser(entraUser, pureserviceManagerUser?.Id,
            companyId, department?.Name, location?.Name, physicalAddressResult?.Id,
            pureservicePhoneNumber?.Id, pureserviceEmailAddress.Id);
        
        var pureserviceUser = pureserviceUserList?.Users.FirstOrDefault();

        if (pureserviceUser is null)
        {
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: Update department and location
        if (department is not null && location is not null &&
            await _pureserviceUserService.UpdateDepartmentAndLocation(pureserviceUser.Id, department.Id, location.Id))
        {
            synchronizationResult.UserCreatedCount++;
        }
        
        // TODO: Should new pureservice user be added to pureserviceUsers list? Probably not
    }
    
    private async Task UpdateUser(User pureserviceUser, Microsoft.Graph.Models.User entraUser, EmailAddress emailAddress,
        PhoneNumber? phoneNumber, User? pureserviceManagerUser, List<Company> companies, List<CompanyDepartment> companyDepartments,
        List<CompanyLocation> companyLocations, SynchronizationResult synchronizationResult)
    {
        var basicPropertiesToUpdate = _pureserviceUserService.NeedsBasicUpdate(
            pureserviceUser, entraUser, pureserviceManagerUser);
        
        var companyPropertiesToUpdate = _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, companyDepartments, companyLocations);
        
        var updateEmail = !emailAddress.Email.Equals(entraUser.Mail, StringComparison.OrdinalIgnoreCase);
        
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var phoneNumberUpdate = _pureservicePhoneNumberService.NeedsPhoneNumberUpdate(phoneNumber, entraPhoneNumber);
        
        if (basicPropertiesToUpdate.Count == 0 && companyPropertiesToUpdate.Count == 0 && !updateEmail &&
            !phoneNumberUpdate.Update)
        {
            synchronizationResult.UserUpToDateCount++;
            _logger.LogInformation("User with UserId {UserId} is up to date", pureserviceUser.Id);
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
                return;
            }

            var phoneNumberResult =
                await _pureservicePhoneNumberService.AddNewPhoneNumberAndLinkToUser(
                    phoneNumberUpdate.PhoneNumber, PhoneNumberType.Mobile, pureserviceUser.Id);
            if (phoneNumberResult is null)
            {
                synchronizationResult.UserErrorCount++;
                return;
            }
            
            if (await _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id,
                    phoneNumberResult.Id))
            {
                synchronizationResult.UserPhoneNumberUpdatedCount++;
                return;
            }

            synchronizationResult.UserErrorCount++;
            return;
        }
        
        if (await _pureservicePhoneNumberService.UpdatePhoneNumber(phoneNumber.Id, phoneNumberUpdate.PhoneNumber,
            PhoneNumberType.Mobile, pureserviceUser.Id))
        {
            synchronizationResult.UserPhoneNumberUpdatedCount++;
            return;
        }
        
        synchronizationResult.UserErrorCount++;
    }
}