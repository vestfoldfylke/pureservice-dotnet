using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;
using Serilog.Context;

namespace pureservice_dotnet.Functions;

public class UserFunctions
{
    private readonly IGraphService _graphService;
    private readonly ILogger<UserFunctions> _logger;
    private readonly IPureserviceCaller _pureserviceCaller;
    private readonly IPureserviceCompanyService _pureserviceCompanyService;
    private readonly IPureserviceEmailAddressService _pureserviceEmailAddressService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureservicePhysicalAddressService _pureservicePhysicalAddressService;
    private readonly IPureserviceUserService _pureserviceUserService;

    private const int MaxRunTimeInMinutes = 20;

    public UserFunctions(IGraphService graphService, ILogger<UserFunctions> logger, IPureserviceCaller pureserviceCaller, IPureserviceCompanyService pureserviceCompanyService,
        IPureserviceEmailAddressService pureserviceEmailAddressService, IPureservicePhoneNumberService pureservicePhoneNumberService,
        IPureservicePhysicalAddressService pureservicePhysicalAddressService, IPureserviceUserService pureserviceUserService)
    {
        _graphService = graphService;
        _logger = logger;
        _pureserviceCaller = pureserviceCaller;
        _pureserviceCompanyService = pureserviceCompanyService;
        _pureserviceEmailAddressService = pureserviceEmailAddressService;
        _pureservicePhoneNumberService = pureservicePhoneNumberService;
        _pureservicePhysicalAddressService = pureservicePhysicalAddressService;
        _pureserviceUserService = pureserviceUserService;
    }

    [Function("Synchronize")]
    public async Task Synchronize([TimerTrigger("%SynchronizeSchedule%")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Starting UserFunctions_Synchronize with a MaxRunTimeLimit of {MaxRunTimeLimit} minutes", MaxRunTimeInMinutes);
        
        var startTime = DateTime.UtcNow;

        var entraEmployees = await _graphService.GetEmployees();
        var entraStudents = await _graphService.GetStudents();
        var entraUsers = entraEmployees.Concat(entraStudents)
            .Where(u => !string.IsNullOrEmpty(u.Id))
            .DistinctBy(u => u.Id)
            .ToList();
        
        _logger.LogInformation("Retrieved {EmployeeCount} employees, {StudentCount} students, total {TotalCount} from Entra", entraEmployees.Count, entraStudents.Count, entraUsers.Count);
        
        var pureserviceUsers = await _pureserviceUserService.GetUsers(["credentials", "emailaddress", "phonenumbers"], includeInactiveUsers: true);

        if (pureserviceUsers.Linked?.Credentials is null || pureserviceUsers.Linked?.EmailAddresses is null || pureserviceUsers.Linked?.PhoneNumbers is null)
        {
            _logger.LogError("Expected linked results were not found in user list");
            throw new InvalidOperationException("Expected linked results were not found in user list");
        }

        var companies = await _pureserviceCompanyService.GetCompanies();
        var departments = await _pureserviceCompanyService.GetDepartments();
        var locations = await _pureserviceCompanyService.GetLocations();
        
        _logger.LogInformation("Retrieved {CompanyCount} companies, {DepartmentCount} departments and {LocationCount} locations from Pureservice", companies.Count, departments.Count, locations.Count);
        
        var synchronizationResult = new SynchronizationResult();
        
        // create or update users
        foreach (var entraUser in entraUsers)
        {
            using (LogContext.PushProperty("EntraId", entraUser.Id))
            {
                if (HasExceededRunLimit(startTime))
                {
                    _logger.LogWarning("Function has been running for more than {MaxRunTimeLimit} minutes, stopping further processing to avoid collisions and next runs", MaxRunTimeInMinutes);
                    _logger.LogInformation("UserFunctions_Synchronize finished: {@SynchronizationResult}", synchronizationResult);
                    return;
                }
                
                _logger.LogDebug("Processing Entra user {DisplayName} with EntraId {EntraId}", entraUser.DisplayName, entraUser.Id);

                var (pureserviceUser, pureserviceManagerUser, skipUser) = GetPureserviceUserInfo(entraUser, pureserviceUsers, synchronizationResult);
                
                if (skipUser)
                {
                    continue;
                }

                if (pureserviceUser is null)
                {
                    if (entraUser.AccountEnabled.HasValue && !entraUser.AccountEnabled.Value)
                    {
                        _logger.LogInformation("Entra user with Id {EntraId} is disabled in Entra. Skipping creation in Pureservice", entraUser.Id);
                        synchronizationResult.UserDisabledCount++;
                        continue;
                    }

                    var company = companies.Find(c => c.Name.Equals(entraUser.CompanyName, StringComparison.OrdinalIgnoreCase));
                    if (company is null)
                    {
                        company = await _pureserviceCompanyService.AddCompany(entraUser.CompanyName!);
                        if (company is null)
                        {
                            _logger.LogError("CompanyName {CompanyName} for new Pureservice user with EntraId {EntraId} not created in Pureservice. User will not be created", entraUser.CompanyName,
                                entraUser.Id);
                            synchronizationResult.CompanyMissingInPureserviceCount++;
                            continue;
                        }
                        
                        companies.Add(company);
                    }

                    // NOTE: If department isn't found because company was just created, it will be created in the next sweep when user will be updated
                    var department = entraUser.Department is not null
                        ? departments.Find(d => d.Name.Equals(entraUser.Department, StringComparison.OrdinalIgnoreCase) && d.CompanyId == company.Id)
                        : null;

                    // NOTE: If location isn't found because company was just created, it will be created in the next sweep when user will be updated
                    var location = entraUser.OfficeLocation is not null
                        ? locations.Find(l => l.Name.Equals(entraUser.OfficeLocation, StringComparison.OrdinalIgnoreCase) && l.CompanyId == company.Id)
                        : null;

                    await CreateUser(entraUser, pureserviceManagerUser, company.Id, department, location, synchronizationResult);
                    continue;
                }
                
                using (LogContext.PushProperty("UserId", pureserviceUser.Id))
                {
                    var (credential, primaryEmailAddress, primaryPhoneNumber, phoneNumberIds) = GetPureserviceUserContactInfo(pureserviceUser, pureserviceUsers, synchronizationResult);
                    if (credential is null || primaryEmailAddress is null)
                    {
                        continue;
                    }
                    
                    var phoneNumbers = pureserviceUsers.Linked.PhoneNumbers.Where(p => phoneNumberIds.Contains(p.Id)).ToList();

                    await UpdateUser(pureserviceUser, entraUser, credential, primaryEmailAddress, primaryPhoneNumber, phoneNumbers, pureserviceManagerUser, companies, departments, locations, synchronizationResult);
                }
            }
        }
        
        // TODO: Loop through Pureservice users not existing in entra and disable them (should we anonymize them as well, if so, how?)

        _logger.LogInformation("UserFunctions_Synchronize finished: {@SynchronizationResult}", synchronizationResult);
    }

    public async Task CreateUser(Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser, int companyId, CompanyDepartment? department, CompanyLocation? location,
        SynchronizationResult synchronizationResult)
    {
        // PhysicalAddress, PhoneNumber (maybe), EmailAddress, User, DepartmentAndLocation (maybe)
        const int expectedRequestCount = 5;
        var (needsToWait, requestCountLastMinute, secondsToWait) = _pureserviceCaller.NeedsToWait(expectedRequestCount);
        if (needsToWait)
        {
            if (!secondsToWait.HasValue)
            {
                _logger.LogWarning("Throttling in Pureservice API detected. Skipping user creation this sweep. Request count last minute: {RequestCountLastMinute}", requestCountLastMinute);
                return;
            }
            
            _logger.LogWarning("Throttling in Pureservice API detected. Waiting {SecondsToWait} seconds before creating user. Request count last minute: {RequestCountLastMinute}",
                secondsToWait.Value, requestCountLastMinute);
            _logger.LogInformation("SynchronizationResult so far: {@SynchronizationResult}", synchronizationResult);
            
            await Task.Delay(secondsToWait.Value * 1000);
        }
        
        synchronizationResult.UserHandledCount++;
        
        _logger.LogWarning("Entra user with Id {EntraId} not found in Pureservice by ImportUniqueKey. User will be created", entraUser.Id);

        if (entraUser.Manager?.Id is not null && pureserviceManagerUser is null)
        {
            _logger.LogInformation("Manager with EntraId {EntraManagerId} for new Pureservice user with EntraId {EntraId} not found in Pureservice. Manager will be updated on user on next sweep",
                entraUser.Manager.Id, entraUser.Id);
        }
        
        // NOTE: Create new physical address (with empty fields since we don't need that info in Pureservice for now)
        var physicalAddressResult = await _pureservicePhysicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway");
        if (physicalAddressResult is null)
        {
            _logger.LogError("Failed to create physical address for new Pureservice user with EntraId {EntraId}. User will not be created", entraUser.Id);
            synchronizationResult.UserErrorCount++;
            return;
        }
                
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var pureservicePhoneNumber = !string.IsNullOrWhiteSpace(entraPhoneNumber)
            ? await _pureservicePhoneNumberService.AddNewPhoneNumber(entraPhoneNumber, PhoneNumberType.Mobile)
            : null;
        if (pureservicePhoneNumber is null && !string.IsNullOrWhiteSpace(entraPhoneNumber))
        {
            _logger.LogError("Failed to create phone number for new Pureservice user with EntraId {EntraId} and PhoneNumber {PhoneNumber}. User will not be created", entraUser.Id, entraPhoneNumber);
            synchronizationResult.UserErrorCount++;
            return;
        }

        // NOTE: We create the user with UserPrincipalName as email address so SSO will work. If Mail actually is different, it will be updated at next sweep.
        var pureserviceEmailAddress = await _pureserviceEmailAddressService.AddNewEmailAddress(entraUser.UserPrincipalName!);
        if (pureserviceEmailAddress is null)
        {
            _logger.LogError("Failed to create email address for new Pureservice user with EntraId {EntraId} and Email {Email}. User will not be created", entraUser.Id, entraUser.Mail);
            synchronizationResult.UserErrorCount++;
            return;
        }

        var pureserviceUser = await _pureserviceUserService.CreateNewUser(entraUser, pureserviceManagerUser?.Id, companyId, physicalAddressResult.Id, pureservicePhoneNumber?.Id, pureserviceEmailAddress.Id);

        if (pureserviceUser is null)
        {
            synchronizationResult.UserErrorCount++;
            return;
        }
        
        synchronizationResult.UserCreatedCount++;

        if (department is not null || location is not null)
        {
            await _pureserviceUserService.UpdateDepartmentAndLocation(pureserviceUser.Id, department?.Id, location?.Id);
        }
    }
    
    public async Task UpdateUser(User pureserviceUser, Microsoft.Graph.Models.User entraUser, Credential credential, EmailAddress emailAddress, PhoneNumber? phoneNumber, List<PhoneNumber> phoneNumbers,
        User? pureserviceManagerUser, List<Company> companies, List<CompanyDepartment> companyDepartments, List<CompanyLocation> companyLocations, SynchronizationResult synchronizationResult)
    {
        // BasicProperties, Username, CompanyProperties, EmailAddress, PhoneNumber (maybe add) and PhoneNumber (maybe set as default)
        // BasicProperties, Username, CompanyProperties, EmailAddress, PhoneNumber (maybe update) and PhoneNumber (maybe set as default)
        // BasicProperties, Username, CompanyProperties, EmailAddress, PhoneNumber (maybe update)
        const int expectedRequestCount = 6;
        var (needsToWait, requestCountLastMinute, secondsToWait) = _pureserviceCaller.NeedsToWait(expectedRequestCount);
        if (needsToWait)
        {
            if (!secondsToWait.HasValue)
            {
                _logger.LogWarning("Throttling in Pureservice API detected. Skipping user update this sweep. Request count last minute: {RequestCountLastMinute}", requestCountLastMinute);
                return;
            }
            
            _logger.LogWarning("Throttling in Pureservice API detected. Waiting {SecondsToWait} seconds before updating user. Request count last minute: {RequestCountLastMinute}",
                secondsToWait.Value, requestCountLastMinute);
            _logger.LogInformation("SynchronizationResult so far: {@SynchronizationResult}", synchronizationResult);
            
            await Task.Delay(secondsToWait.Value * 1000);
        }
        
        synchronizationResult.UserHandledCount++;
        
        if (entraUser.Manager?.Id is not null && pureserviceManagerUser is null)
        {
            _logger.LogInformation("Manager with EntraId {EntraManagerId} for Pureservice user with UserId {UserId} not found in Pureservice. Manager will be updated on user on next sweep",
                entraUser.Manager.Id, pureserviceUser.Id);
        }
        
        var basicPropertiesToUpdate = _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser);

        var usernameUpdate = _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser);
        
        var companyUpdate = _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies);
        var departmentUpdate = _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, companyDepartments);
        var locationUpdate = _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, companyLocations);
        
        var updateEmail = !emailAddress.Email.Equals(entraUser.Mail, StringComparison.OrdinalIgnoreCase);
        
        var entraPhoneNumber = _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile");
        var phoneNumberUpdate = _pureservicePhoneNumberService.NeedsPhoneNumberUpdate(phoneNumber, entraPhoneNumber);
        
        if (basicPropertiesToUpdate.Count == 0 && !usernameUpdate.Update && companyUpdate is null && departmentUpdate is null && locationUpdate is null && !updateEmail && !phoneNumberUpdate.Update)
        {
            synchronizationResult.UserUpToDateCount++;
            _logger.LogDebug("User with UserId {UserId} is up to date", pureserviceUser.Id);
            return;
        }
        
        if (basicPropertiesToUpdate.Count > 0 && await _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, basicPropertiesToUpdate))
        {
            synchronizationResult.UserBasicPropertiesUpdatedCount++;
        }
        
        if (usernameUpdate.Update && await _pureserviceUserService.UpdateUsername(pureserviceUser.Id, credential.Id, usernameUpdate.Username!))
        {
            synchronizationResult.UserUsernameUpdatedCount++;
        }

        if (companyUpdate is not null || departmentUpdate is not null || locationUpdate is not null)
        {
            List<CompanyUpdateItem> propertiesToUpdate = [];

            if (companyUpdate is not null)
            {
                var (updateItem, company) = await GetOrCreateCompany(companyUpdate);
                if (updateItem is not null)
                {
                    propertiesToUpdate.Add(updateItem);
                }

                if (company is not null)
                {
                    companies.Add(company);
                }
            }
            
            if (companyUpdate is null && departmentUpdate is not null)
            {
                if (!pureserviceUser.CompanyId.HasValue)
                {
                    _logger.LogError("UserId {UserId} has no CompanyId set in Pureservice, cannot update department: {@Department}", pureserviceUser.Id, departmentUpdate);
                }
                else
                {
                    var company = companies.Find(c => c.Id == pureserviceUser.CompanyId.Value);
                    if (company is not null)
                    {
                        var (updateItem, department) = await GetOrCreateDepartment(departmentUpdate, company);
                        if (updateItem is not null)
                        {
                            propertiesToUpdate.Add(updateItem);
                        }

                        if (department is not null)
                        {
                            companyDepartments.Add(department);
                        }
                    }
                }
            }
            
            if (companyUpdate is null && locationUpdate is not null)
            {
                if (!pureserviceUser.CompanyId.HasValue)
                {
                    _logger.LogError("UserId {UserId} has no CompanyId set in Pureservice, cannot update location: {@Location}", pureserviceUser.Id, locationUpdate);
                }
                else
                {
                    var company = companies.Find(c => c.Id == pureserviceUser.CompanyId.Value);
                    if (company is not null)
                    {
                        var (updateItem, location) = await GetOrCreateLocation(locationUpdate, company);
                        if (updateItem is not null)
                        {
                            propertiesToUpdate.Add(updateItem);
                        }

                        if (location is not null)
                        {
                            companyLocations.Add(location);
                        }
                    }
                }
            }
            
            await _pureserviceUserService.UpdateCompanyProperties(pureserviceUser.Id, propertiesToUpdate);
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
                _logger.LogError("No phone number exists for Pureservice UserId {UserId} and no phone number found in Entra on EntraId {EntraId}. Cannot add empty phone number", pureserviceUser.Id, entraUser.Id);
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

    private (User? pureserviceUser, User? pureserviceManagerUser, bool skipUser) GetPureserviceUserInfo(Microsoft.Graph.Models.User entraUser, UserList pureserviceUsers,
        SynchronizationResult synchronizationResult)
    {
        if (entraUser.Mail is null && entraUser.AccountEnabled.HasValue && entraUser.AccountEnabled.Value)
        {
            _logger.LogWarning("Entra user with Id {EntraId} has no email address. Skipping", entraUser.Id);
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, true);
        }

        if (entraUser.CompanyName is null && entraUser.AccountEnabled.HasValue && entraUser.AccountEnabled.Value)
        {
            _logger.LogWarning("Entra user with Id {EntraId} has no company name. Skipping", entraUser.Id);
            synchronizationResult.UserMissingCompanyNameCount++;
            return (null, null, true);
        }
            
        var pureserviceUser = pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Id);
            
        var pureserviceManagerUser = entraUser.Manager?.Id is not null
            ? pureserviceUsers.Users.Find(u => !string.IsNullOrEmpty(u.ImportUniqueKey) && u.ImportUniqueKey == entraUser.Manager.Id)
            : null;
        
        return (pureserviceUser, pureserviceManagerUser, false);
    }

    private (Credential? credential, EmailAddress? primaryEmailAddress, PhoneNumber? primaryPhoneNumber, List<int> phoneNumberIds) GetPureserviceUserContactInfo(User pureserviceUser, UserList pureserviceUsers,
        SynchronizationResult synchronizationResult)
    {
        if (pureserviceUser.Links is null)
        {
            _logger.LogError("UserId {UserId} has no links. Skipping", pureserviceUser.Id);
            synchronizationResult.UserErrorCount++;
            return (null, null, null, []);
        }

        if (pureserviceUser.Links.Credentials is null)
        {
            _logger.LogError("UserId {UserId} has no credential. Skipping", pureserviceUser.Id);
            synchronizationResult.UserMissingCredentialsCount++;
            return (null, null, null, []);
        }

        if (pureserviceUser.Links.EmailAddress is null)
        {
            _logger.LogWarning("UserId {UserId} has no email address. Skipping", pureserviceUser.Id);
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, null, []);
        }
        
        var credential = pureserviceUsers.Linked!.Credentials!.Find(c => c.Id == pureserviceUser.Links.Credentials.Id);
        
        if (credential is null)
        {
            _logger.LogError("CredentialsId {CredentialsId} for UserId {UserId} not found in Pureservice. Skipping", pureserviceUser.Links.Credentials.Id, pureserviceUser.Id);
            synchronizationResult.UserMissingCredentialsCount++;
            return (null, null, null, []);
        }

        var primaryEmailAddress = pureserviceUsers.Linked!.EmailAddresses!.Find(e => e.Id == pureserviceUser.Links.EmailAddress.Id);

        if (primaryEmailAddress is null)
        {
            _logger.LogError("EmailAddressId {EmailAddressId} for UserId {UserId} not found in Pureservice. Skipping", pureserviceUser.Links.EmailAddress.Id, pureserviceUser.Id);
            synchronizationResult.UserMissingEmailAddressCount++;
            return (null, null, null, []);
        }
            
        var primaryPhoneNumber = pureserviceUser.Links.PhoneNumber is not null
            ? pureserviceUsers.Linked!.PhoneNumbers!.Find(p => p.Id == pureserviceUser.Links.PhoneNumber.Id)
            : null;

        var phoneNumberIds = pureserviceUser.Links.PhoneNumbers?.Ids ?? [];
        
        return (credential, primaryEmailAddress, primaryPhoneNumber, phoneNumberIds);
    }

    private async Task<(CompanyUpdateItem? UpdateItem, Company? company)> GetOrCreateCompany(CompanyUpdateItem updateItem)
    {
        if (updateItem.NameToCreate is null)
        {
            return (new CompanyUpdateItem(updateItem.PropertyName, updateItem.Id), null);
        }
        
        var company = await _pureserviceCompanyService.AddCompany(updateItem.NameToCreate);
        return company is not null
            ? (new CompanyUpdateItem(updateItem.PropertyName, company.Id), company)
            : (null, null);
    }
    
    private async Task<(CompanyUpdateItem? UpdateItem, CompanyDepartment? department)> GetOrCreateDepartment(CompanyUpdateItem updateItem, Company company)
    {
        if (updateItem.NameToCreate is null)
        {
            return (new CompanyUpdateItem(updateItem.PropertyName, updateItem.Id), null);
        }
        
        var department = await _pureserviceCompanyService.AddDepartment(updateItem.NameToCreate, company.Id);
        return department is not null
            ? (new CompanyUpdateItem(updateItem.PropertyName, department.Id), department)
            : (null, null);
    }
    
    private async Task<(CompanyUpdateItem? UpdateItem, CompanyLocation? location)> GetOrCreateLocation(CompanyUpdateItem updateItem, Company company)
    {
        if (updateItem.NameToCreate is null)
        {
            return (new CompanyUpdateItem(updateItem.PropertyName, updateItem.Id), null);
        }
        
        var location = await _pureserviceCompanyService.AddLocation(updateItem.NameToCreate, company.Id);
        return location is not null
            ? (new CompanyUpdateItem(updateItem.PropertyName, location.Id), location)
            : (null, null);
    }
    
    private static bool HasExceededRunLimit(DateTime startTime) =>
        DateTime.UtcNow - startTime > TimeSpan.FromMinutes(MaxRunTimeInMinutes);
}