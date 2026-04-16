using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceUserService
{
    Task<User?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId, int physicalAddressId, int? phoneNumberId, int emailAddressId, string? userType);
    Dictionary<string, object?> GetNewUserPayload(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId, int physicalAddressId, int? phoneNumberId, int emailAddressId,
        string? userType);
    Task<UserList> GetUser(int userId, string[]? entities = null);
    Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false);
    List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> NeedsBasicUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser = null, bool? handleStatusOnly = false, string? entraUserType = null);
    CompanyUpdateItem? NeedsCompanyUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies);
    CompanyUpdateItem? NeedsDepartmentUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyDepartment> companyDepartments);
    CompanyUpdateItem? NeedsLocationUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyLocation> companyLocations);
    (bool Update, string? Username) NeedsUsernameUpdate(Credential credential, Microsoft.Graph.Models.User entraUser);
    Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId);
    Task<bool> UpdateBasicProperties(int userId, List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)> propertiesToUpdate);
    Task<bool> UpdateCompanyProperties(int userId, List<CompanyUpdateItem> propertiesToUpdate);
    Task<bool> UpdateDepartmentAndLocation(int userId, int? departmentId, int? locationId);
    Task<bool> UpdateUsername(int userId, int credentialsId, string username);
}

public class PureserviceUserService : IPureserviceUserService
{
    private readonly ILogger<PureserviceUserService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "user";
    private readonly string _userTypeCustomField;
    
    public PureserviceUserService(IConfiguration configuration, ILogger<PureserviceUserService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
        
        _userTypeCustomField = configuration["User_Type_Custom_Field_Id"] ?? throw new InvalidOperationException("User_Type_Custom_Field_Id configuration value is not set");
    }

    public async Task<User?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId, int physicalAddressId, int? phoneNumberId, int emailAddressId, string? userType)
    {
        var payload = GetNewUserPayload(entraUser, managerId, companyId, physicalAddressId, phoneNumberId, emailAddressId, userType);
        
        _logger.LogInformation("Creating new Pureservice user with ImportUniqueKey {ImportUniqueKey}", entraUser.Id);
        var result = await _pureserviceCaller.PostAsync<User>($"{BasePath}?include=company,company.departments,company.locations,emailaddress,language,phonenumbers", payload);

        if (result is not null)
        {
            _logger.LogInformation("Successfully created new Pureservice user with ImportUniqueKey {ImportUniqueKey} and UserId {UserId}", result.ImportUniqueKey, result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewUser", "Number of users created", (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create new Pureservice user with ImportUniqueKey {ImportUniqueKey}: {@Payload}", entraUser.Id, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewUser", "Number of users created", (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }
    
    public async Task<UserList> GetUser(int userId, string[]? entities = null)
    {
        if (entities == null)
        {
            return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}") ?? new UserList([], null);
        }
        
        return await _pureserviceCaller.GetAsync<UserList>($"{BasePath}/{userId}?include={string.Join(",", entities)}") ?? new UserList([], null);
    }

    public async Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false)
    {
        var userList = new UserList([], new Linked
        {
            Companies = [],
            CompanyDepartments = [],
            CompanyLocations = [],
            Credentials = [],
            EmailAddresses = [],
            Languages = [],
            PhoneNumbers = [],
            PhysicalAddresses = []
        });

        var currentStart = start;
        
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        
        if (!includeInactiveUsers)
        {
            queryString["filter"] = "disabled == false";
        }
        
        if (!includeSystemUsers)
        {
            var filter = queryString["filter"];
            queryString["filter"] = string.IsNullOrEmpty(filter)
                ? $"role > {(int)UserRole.None} AND role < {(int)UserRole.System}"
                : $"{filter} AND role > {(int)UserRole.None} AND role < {(int)UserRole.System}";
        }
        
        if (entities != null)
        {
            queryString["include"] = string.Join(",", entities);
        }

        queryString["start"] = currentStart.ToString();
        queryString["limit"] = limit.ToString();

        UserList? result = null;
        
        while (result is null || result.Users.Count > 0)
        {
            queryString["start"] = currentStart.ToString();
            
            result = await _pureserviceCaller.GetAsync<UserList>($"{BasePath}?{queryString}");
            if (result is null)
            {
                throw new InvalidOperationException($"No result returned from Pureservice API from start {currentStart} with limit {limit}");
            }
            
            _logger.LogDebug("Fetched {Count} users starting from {Start} with limit {Limit}", result.Users.Count, currentStart, limit);
            userList.Users.AddRange(result.Users);
            
            if (result.Linked?.Companies is not null && userList.Linked?.Companies is not null)
            {
                _logger.LogDebug("Fetched {Count} companies linked to users", result.Linked.Companies.Count);
                userList.Linked.Companies.AddRange(result.Linked.Companies);
            }
            
            if (result.Linked?.CompanyDepartments is not null && userList.Linked?.CompanyDepartments is not null)
            {
                _logger.LogDebug("Fetched {Count} company departments available to users", result.Linked.CompanyDepartments.Count);
                userList.Linked.CompanyDepartments.AddRange(result.Linked.CompanyDepartments);
            }
            
            if (result.Linked?.CompanyLocations is not null && userList.Linked?.CompanyLocations is not null)
            {
                _logger.LogDebug("Fetched {Count} company locations available to users", result.Linked.CompanyLocations.Count);
                userList.Linked.CompanyLocations.AddRange(result.Linked.CompanyLocations);
            }
            
            if (result.Linked?.Credentials is not null && userList.Linked?.Credentials is not null)
            {
                _logger.LogDebug("Fetched {Count} credentials linked to users", result.Linked.Credentials.Count);
                userList.Linked.Credentials.AddRange(result.Linked.Credentials);
            }
            
            if (result.Linked?.EmailAddresses is not null && userList.Linked?.EmailAddresses is not null)
            {
                _logger.LogDebug("Fetched {Count} email addresses linked to users", result.Linked.EmailAddresses.Count);
                userList.Linked.EmailAddresses.AddRange(result.Linked.EmailAddresses);
            }
            
            if (result.Linked?.Languages is not null && userList.Linked?.Languages is not null)
            {
                _logger.LogDebug("Fetched {Count} languages available to users", result.Linked.Languages.Count);
                userList.Linked.Languages.AddRange(result.Linked.Languages);
            }
            
            if (result.Linked?.PhoneNumbers is not null && userList.Linked?.PhoneNumbers is not null)
            {
                _logger.LogDebug("Fetched {Count} phone numbers linked to users", result.Linked.PhoneNumbers.Count);
                userList.Linked.PhoneNumbers.AddRange(result.Linked.PhoneNumbers);
            }
            
            if (result.Linked?.PhysicalAddresses is not null && userList.Linked?.PhysicalAddresses is not null)
            {
                _logger.LogDebug("Fetched {Count} physical addresses linked to users", result.Linked.PhysicalAddresses.Count);
                userList.Linked.PhysicalAddresses.AddRange(result.Linked.PhysicalAddresses);
            }
            
            if (result.Users.Count == 0)
            {
                _logger.LogInformation("Returning {UserCount} Pureservice users, {CompanyCount} companies, {DepartmentCount} departments, {LocationCount} locations, {CredentialCount} credentials, {EmailAddressCount} email addresses, {LanguageCount} languages, {PhoneNumberCount} phone numbers and {PhysicalAddressCount} physical addresses",
                    userList.Users.Count, userList.Linked?.Companies?.Count ?? 0, userList.Linked?.CompanyDepartments?.Count ?? 0, userList.Linked?.CompanyLocations?.Count ?? 0,
                    userList.Linked?.Credentials?.Count ?? 0, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.Languages?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0,
                    userList.Linked?.PhysicalAddresses?.Count ?? 0);
                return userList;
            }

            currentStart += result.Users.Count;
            _logger.LogDebug("Preparing to fetch next batch of users starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogWarning("Reached outside of while somehow 😱 Returning {UserCount} Pureservice users, {CompanyCount} companies, {DepartmentCount} departments, {LocationCount} locations, {CredentialCount} credentials, {EmailAddressCount} email addresses, {LanguageCount} languages, {PhoneNumberCount} phone numbers and {PhysicalAddressCount} physical addresses",
            userList.Users.Count, userList.Linked?.Companies?.Count ?? 0, userList.Linked?.CompanyDepartments?.Count ?? 0, userList.Linked?.CompanyLocations?.Count ?? 0,
            userList.Linked?.Credentials?.Count ?? 0, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.Languages?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0,
            userList.Linked?.PhysicalAddresses?.Count ?? 0);
        return userList;
    }

    public List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> NeedsBasicUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser = null, bool? handleStatusOnly = false, string? entraUserType = null)
    {
        List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> propertiesToUpdate = [];
        
        if (pureserviceUser.Disabled != (entraUser.AccountEnabled == false))
        {
            propertiesToUpdate.Add(("disabled", (null, null, !entraUser.AccountEnabled)));
        }
        
        if (handleStatusOnly == true)
        {
            return propertiesToUpdate;
        }
        
        if (pureserviceUser.FirstName != entraUser.GivenName)
        {
            propertiesToUpdate.Add(("firstName", (entraUser.GivenName, null, null)));
        }
        
        if (pureserviceUser.LastName != entraUser.Surname)
        {
            propertiesToUpdate.Add(("lastName", (entraUser.Surname, null, null)));
        }
        
        if (pureserviceUser.Title != entraUser.JobTitle)
        {
            propertiesToUpdate.Add(("title", (entraUser.JobTitle, null, null)));
        }
        
        if (pureserviceUser.ManagerId != pureserviceManagerUser?.Id)
        {
            propertiesToUpdate.Add(("managerId", (null, pureserviceManagerUser?.Id, null)));
        }

        var currentUserType = GetCustomFieldValueFromPureserviceUser<string?>(pureserviceUser, _userTypeCustomField) as string;
        if (currentUserType != entraUserType)
        {
            propertiesToUpdate.Add((_userTypeCustomField, (entraUserType, null, null)));
        }
        
        return propertiesToUpdate;
    }

    public CompanyUpdateItem? NeedsCompanyUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies)
    {
        var company = GetCompany(pureserviceUser, entraUser, companies);
        return company.Update
            ? new CompanyUpdateItem("companyId", company.Company?.Id, company.Name)
            : null;
    }
    
    public CompanyUpdateItem? NeedsDepartmentUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyDepartment> companyDepartments)
    {
        var company = GetCompany(pureserviceUser, entraUser, companies);
        if (company.Company is null)
        {
            return null;
        }
        
        var department = GetDepartment(pureserviceUser, entraUser, companyDepartments, company.Company);
        return department.Update
            ? new CompanyUpdateItem("companyDepartmentId", department.CompanyDepartment?.Id, department.Name)
            : null;
    }
    
    public CompanyUpdateItem? NeedsLocationUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyLocation> companyLocations)
    {
        var company = GetCompany(pureserviceUser, entraUser, companies);
        if (company.Company is null)
        {
            return null;
        }
        
        var location = GetLocation(pureserviceUser, entraUser, companyLocations, company.Company);
        return location.Update
            ? new CompanyUpdateItem("companyLocationId", location.CompanyLocation?.Id, location.Name)
            : null;
    }

    public (bool Update, string? Username) NeedsUsernameUpdate(Credential credential, Microsoft.Graph.Models.User entraUser)
    {
        if (entraUser.UserPrincipalName is not null)
        {
            return (!credential.Username.Equals(entraUser.UserPrincipalName, StringComparison.OrdinalIgnoreCase), entraUser.UserPrincipalName);
        }
        
        _logger.LogWarning("UserPrincipalName is null for user with EntraId {EntraId}. Cannot determine if username update is needed.", entraUser.Id);
        return (false, null);
    }
    
    public async Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId)
    {
        var payload = new
        {
            phonenumberId = phoneNumberId
        };
        
        _logger.LogInformation("Registering PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);

        if (result)
        {
            _logger.LogInformation("Successfully registered PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to register PhoneNumberId {PhoneNumberId} as default for UserId {UserId}: {@Payload}", phoneNumberId, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateBasicProperties(int userId, List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)> propertiesToUpdate)
    {
        var payload = new Dictionary<string, object?>();

        foreach (var propertyItem in propertiesToUpdate)
        {
            if (propertyItem.PropertyValue.StringValue is not null)
            {
                payload.Add(propertyItem.PropertyName, propertyItem.PropertyValue.StringValue);
                continue;
            }
            
            if (propertyItem.PropertyValue.IntValue is not null)
            {
                payload.Add(propertyItem.PropertyName, propertyItem.PropertyValue.IntValue);
                continue;
            }
            
            if (propertyItem.PropertyValue.BoolValue is not null)
            {
                payload.Add(propertyItem.PropertyName, propertyItem.PropertyValue.BoolValue);
                continue;
            }
            
            payload.Add(propertyItem.PropertyName, null);
        }

        if (payload.Count == 0)
        {
            return false;
        }

        var propertyNames = propertiesToUpdate.Select(p => p.PropertyName);

        _logger.LogInformation("Updating basic PropertyNames {@PropertyNames} on UserId {UserId}", propertyNames, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated {PropertyCount} basic properties on UserId {UserId}", payload.Count, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedBasicProperties", "Number of basic properties updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update {PropertyCount} basic properties on UserId {UserId}: {@Payload}", propertiesToUpdate.Count, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedBasicProperties", "Number of basic properties updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateCompanyProperties(int userId, List<CompanyUpdateItem> propertiesToUpdate)
    {
        var payload = new Dictionary<string, int?>();

        foreach (var propertyItem in propertiesToUpdate)
        {
            payload.Add(propertyItem.PropertyName, propertyItem.Id);
        }
        
        if (payload.Count == 0)
        {
            return false;
        }
        
        var propertyNames = propertiesToUpdate.Select(p => p.PropertyName);

        _logger.LogInformation("Updating company PropertyNames {@PropertyNames} on UserId {UserId}", propertyNames, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated {PropertyCount} company properties on UserId {UserId}", payload.Count, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedCompanyProperties", "Number of company properties updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update {PropertyCount} company properties on UserId {UserId}: {@Payload}", propertiesToUpdate.Count, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedCompanyProperties", "Number of company properties updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
    
    public async Task<bool> UpdateDepartmentAndLocation(int userId, int? departmentId, int? locationId)
    {
        var payload = new Dictionary<string, int?>();
        if (departmentId.HasValue)
        {
            payload.Add("companyDepartmentId", departmentId.Value);
        }
        if (locationId.HasValue)
        {
            payload.Add("companyLocationId", locationId.Value);
        }
        
        var propertyNames = payload.Select(p => p.Key);
        
        _logger.LogInformation("Updating company PropertyNames {PropertyNames} on UserId {UserId}", propertyNames, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated {PropertyCount} company properties on UserId {UserId}", payload.Count, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateDepartmentAndLocation", "Number of department and/or location updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update {PropertyCount} company properties on UserId {UserId}: {@Payload}", payload.Count, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateDepartmentAndLocation", "Number of department and/or location updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateUsername(int userId, int credentialsId, string username)
    {
        var payload = new
        {
            credentials = new
            {
                id = credentialsId,
                username
            }
        };
        
        _logger.LogInformation("Updating username with CredentialsId {CredentialsId} for UserId {UserId}", credentialsId, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);

        if (result)
        {
            _logger.LogInformation("Successfully updated CredentialsId {CredentialsId} for UserId {UserId}", credentialsId, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateUsername", "Number of username updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update CredentialsId {CredentialsId} for UserId {UserId}: {@Payload}", credentialsId, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateUsername", "Number of username updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public Dictionary<string, object?> GetNewUserPayload(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId, int physicalAddressId, int? phoneNumberId, int emailAddressId, string? userType)
    {
        var userPayload = new Dictionary<string, object?>
        {
            ["firstName"] = entraUser.GivenName,
            ["lastName"] = entraUser.Surname,
            ["unavailable"] = false,
            ["title"] = entraUser.JobTitle ?? "",
            ["managerId"] = managerId,
            ["companyId"] = companyId,
            ["notes"] = "",
            ["role"] = UserRole.Enduser,
            ["disabled"] = entraUser.AccountEnabled == false,
            ["isSuperuser"] = false,
            ["importUniqueKey"] = entraUser.Id,
            ["flushNotifications"] = true,
            ["highlightNotifications"] = true,
            ["notificationScheme"] = 1,
            ["languageId"] = 2,
            [_userTypeCustomField] = userType,
            ["links"] = new Dictionary<string, object?>
            {
                ["address"] = new Dictionary<string, object?> { ["id"] = physicalAddressId, ["type"] = "physicaladdress" },
                ["emailAddress"] = new Dictionary<string, object?> { ["id"] = emailAddressId, ["type"] = "emailaddress" },
                ["company"] = new Dictionary<string, object?> { ["id"] = companyId, ["type"] = "company" }
            }
        };

        if (!phoneNumberId.HasValue)
        {
            return new Dictionary<string, object?>
            {
                ["users"] = new List<object> { userPayload }
            };
        }

        if (userPayload["links"] is not Dictionary<string, object?> links)
        {
            throw new InvalidOperationException("Links dictionary is null when trying to add phone number link for new user payload");
        }
        
        links.Add("phonenumber", new Dictionary<string, object?>
        {
            ["id"] = phoneNumberId,
            ["type"] = "phonenumber"
        });

        return new Dictionary<string, object?>
        {
            ["users"] = new List<object> { userPayload }
        };
    }

    private (bool Update, Company? Company, string? Name) GetCompany(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies)
    {
        var company = pureserviceUser.CompanyId.HasValue
            ? companies.Find(c => c.Id == pureserviceUser.CompanyId.Value)
            : null;
        
        var wantedCompany = entraUser.CompanyName is not null
            ? companies.Find(c => c.Name.Equals(entraUser.CompanyName, StringComparison.OrdinalIgnoreCase))
            : null;
        
        if (company is null)
        {
            if (wantedCompany is not null)
            {
                return (true, wantedCompany, null);
            }
            
            if (entraUser.CompanyName is null)
            {
                // user has no company in Entra and should not have one in Pureservice
                return (false, null, null);
            }
            
            // user has a company in Entra, but we could not find it in Pureservice, so it needs to be created
            _logger.LogInformation("Could not find CompanyName {CompanyName} in Pureservice which UserId {UserId} should have. It needs to be created", entraUser.CompanyName, pureserviceUser.Id);
            
            return (true, wantedCompany, entraUser.CompanyName);
        }
        
        // company is not null
        if (wantedCompany is not null)
        {
            if (company.Id != wantedCompany.Id)
            {
                return (true, wantedCompany, null);
            }
            
            _logger.LogDebug("UserId {UserId} has correct CompanyId {CompanyId}", pureserviceUser.Id, company.Id);
            return (false, company, null);
        }
        
        // company is not null and wantedCompany is null
        if (entraUser.CompanyName is null)
        {
            // user has no company in Entra and should not have one in Pureservice
            return (true, null, null);
        }
        
        _logger.LogInformation("Could not find CompanyName {CompanyName} in Pureservice which UserId {UserId} should have. It needs to be created", entraUser.CompanyName, pureserviceUser.Id);
        
        return (true, wantedCompany, entraUser.CompanyName);
    }

    private (bool Update, CompanyDepartment? CompanyDepartment, string? Name) GetDepartment(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<CompanyDepartment> companyDepartments, Company? company)
    {
        if (company is null)
        {
            return (pureserviceUser.CompanyDepartmentId is not null, null, null);
        }
        
        var department = pureserviceUser.CompanyDepartmentId.HasValue
            ? companyDepartments.Find(d => d.Id == pureserviceUser.CompanyDepartmentId.Value && d.CompanyId == company.Id)
            : null;
        
        var wantedDepartment = entraUser.Department is not null
            ? companyDepartments.Find(d => d.Name.Equals(entraUser.Department, StringComparison.OrdinalIgnoreCase) && d.CompanyId == company.Id)
            : null;
        
        if (department is null)
        {
            if (wantedDepartment is not null)
            {
                return (true, wantedDepartment, null);
            }
            
            if (entraUser.Department is null)
            {
                // user has no department in Entra and should not have one in Pureservice
                return (false, null, null);
            }
            
            // user has a department in Entra, but we could not find it in Pureservice, so it needs to be created
            _logger.LogInformation("Could not find DepartmentName {DepartmentName} under CompanyId {CompanyId} which UserId {UserId} should have. It needs to be created",
                entraUser.Department, company.Id, pureserviceUser.Id);
            
            return (true, wantedDepartment, entraUser.Department);
        }
        
        // department is not null
        if (wantedDepartment is not null)
        {
            if (department.Id != wantedDepartment.Id)
            {
                return (true, wantedDepartment, null);
            }
            
            _logger.LogDebug("UserId {UserId} has correct DepartmentId {DepartmentId} under CompanyId {CompanyId}", pureserviceUser.Id, department.Id, company.Id);
            return (false, null, null);
        }
        
        // department is not null and wantedDepartment is null
        if (entraUser.Department is null)
        {
            // user has no department in Entra and should not have one in Pureservice
            return (true, null, null);
        }
        
        _logger.LogInformation("Could not find DepartmentName {DepartmentName} under CompanyId {CompanyId} which UserId {UserId} should have. It needs to be created",
            entraUser.Department, company.Id, pureserviceUser.Id);
        
        return (true, wantedDepartment, entraUser.Department);
    }
    
    private (bool Update, CompanyLocation? CompanyLocation, string? Name) GetLocation(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<CompanyLocation> companyLocations, Company? company)
    {
        if (company is null)
        {
            return (pureserviceUser.CompanyLocationId is not null, null, null);
        }
        
        var location = pureserviceUser.CompanyLocationId.HasValue
            ? companyLocations.Find(l => l.Id == pureserviceUser.CompanyLocationId.Value && l.CompanyId == company.Id)
            : null;
        
        var wantedLocation = entraUser.OfficeLocation is not null
            ? companyLocations.Find(l => l.Name.Equals(entraUser.OfficeLocation, StringComparison.OrdinalIgnoreCase) && l.CompanyId == company.Id)
            : null;
        
        if (location is null)
        {
            if (wantedLocation is not null)
            {
                return (true, wantedLocation, null);
            }
            
            if (entraUser.OfficeLocation is null)
            {
                // user has no location in Entra and should not have one in Pureservice
                return (false, null, null);
            }
            
            // user has a location in Entra, but we could not find it in Pureservice, so it needs to be created
            _logger.LogInformation("Could not find LocationName {LocationName} under CompanyId {CompanyId} which UserId {UserId} should have. It needs to be created",
                entraUser.OfficeLocation, company.Id, pureserviceUser.Id);
            
            return (true, wantedLocation, entraUser.OfficeLocation);
        }
        
        // location is not null
        if (wantedLocation is not null)
        {
            if (location.Id != wantedLocation.Id)
            {
                return (true, wantedLocation, null);
            }
            
            _logger.LogDebug("UserId {UserId} has correct LocationId {LocationId} under CompanyId {CompanyId}", pureserviceUser.Id, location.Id, company.Id);
            return (false, null, null);
        }
        
        // location is not null and wantedLocation is null
        if (entraUser.OfficeLocation is null)
        {
            // user has no location in Entra and should not have one in Pureservice
            return (true, null, null);
        }
        
        _logger.LogWarning("Could not find LocationName {LocationName} under CompanyId {CompanyId} which UserId {UserId} should have. It needs to be created",
            entraUser.OfficeLocation, company.Id, pureserviceUser.Id);
        
        return (true, wantedLocation, entraUser.OfficeLocation);
    }

    private static object? GetCustomFieldValueFromPureserviceUser<T>(User pureserviceUser, string customFieldName)
    {
        if (typeof(T) != typeof(string) && typeof(T) != typeof(int) && typeof(T) != typeof(DateTime))
        {
            throw new ArgumentException($"Type {typeof(T)} is not supported. Only string, int and DateTime are supported.", nameof(T));
        }

        var property = typeof(User).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false)
                .Cast<System.Text.Json.Serialization.JsonPropertyNameAttribute>()
                .Any(attr => attr.Name == customFieldName));

        if (property is null)
        {
            throw new InvalidOperationException($"Could not find property with JsonPropertyNameAttribute with name {customFieldName} on User class");
        }

        var propertyValue = property.GetValue(pureserviceUser);

        return propertyValue switch
        {
            null => null,
            JsonElement element when typeof(T) == typeof(string) => element.GetString(),
            JsonElement element when typeof(T) == typeof(int) => element.GetInt32(),
            JsonElement element when typeof(T) == typeof(DateTime) => element.GetDateTime(),
            JsonElement => throw new ArgumentException($"Type {typeof(T)} is not supported. Only string, int and DateTime are supported.", nameof(T)),
            _ => (T?)propertyValue
        };
    }
}