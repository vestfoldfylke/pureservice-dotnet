using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceUserService
{
    Task<User?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId,
        int physicalAddressId, int phoneNumberId, int emailAddressId);
    Task<UserList> GetUser(int userId, string[]? entities = null);
    Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false);
    List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> NeedsBasicUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser = null);
    CompanyUpdateItem? NeedsCompanyUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies);
    CompanyUpdateItem? NeedsDepartmentUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyDepartment> companyDepartments);
    CompanyUpdateItem? NeedsLocationUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies, List<CompanyLocation> companyLocations);
    Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId);
    Task<bool> UpdateBasicProperties(int userId,
        List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)> propertiesToUpdate);
    Task<bool> UpdateCompanyProperties(int userId, List<CompanyUpdateItem> propertiesToUpdate);
    Task<bool> UpdateDepartmentAndLocation(int userId, int? departmentId, int? locationId);
}

public class PureserviceUserService : IPureserviceUserService
{
    private readonly ILogger<PureserviceUserService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    private const string BasePath = "user";
    
    public PureserviceUserService(ILogger<PureserviceUserService> logger, IMetricsService metricsService, IPureserviceCaller pureserviceCaller)
    {
        _logger = logger;
        _metricsService = metricsService;
        _pureserviceCaller = pureserviceCaller;
    }

    public async Task<User?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId,
        int physicalAddressId, int phoneNumberId, int emailAddressId)
    {
        var payload = new
        {
            users = new List<object>
            {
                new
                {
                    firstName = entraUser.GivenName!,
                    lastName = entraUser.Surname!,
                    unavailable = false,
                    title = entraUser.JobTitle ?? "",
                    managerId,
                    companyId,
                    notes = "",
                    role = UserRole.Enduser,
                    disabled = entraUser.AccountEnabled == false,
                    isSuperuser = false,
                    importUniqueKey = entraUser.Id,
                    flushNotifications = true, // NOTE: What does this mean?
                    highlightNotifications = true, // NOTE: What does this mean?
                    notificationScheme = 1, // NOTE: What does this mean?
                    languageId = 2, // NOTE: Norwegian Bokmål
                    links = new
                    {
                        address = new { id = physicalAddressId, type = "physicaladdress" },
                        emailAddress = new { id = emailAddressId, type = "emailaddress" },
                        phonenumber = new { id = phoneNumberId, type = "phonenumber" },
                        company = new { id = companyId, type = "company" }
                    }
                }
            }
        };
        
        _logger.LogInformation("Creating new pureservice user with ImportUniqueKey {ImportUniqueKey}", entraUser.Id);
        var result = await _pureserviceCaller.PostAsync<User>($"{BasePath}?include=company,company.departments,company.locations,emailaddress,language,phonenumbers", payload);

        if (result is not null)
        {
            _logger.LogInformation("Successfully created new pureservice user with ImportUniqueKey {ImportUniqueKey} and UserId {UserId}", result.ImportUniqueKey, result.Id);
            _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewUser", "Number of users created", (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return result;
        }
        
        _logger.LogError("Failed to create new pureservice user with ImportUniqueKey {ImportUniqueKey}: {@Payload}", entraUser.Id, payload);
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
            EmailAddresses = [],
            Languages = [],
            PhoneNumbers = []
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
                _logger.LogError("No result returned from API from start {Start} with limit {Limit}", currentStart, limit);
                return userList;
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
            
            if (result.Users.Count == 0)
            {
                _logger.LogInformation("Returning {UserCount} pureservice users, {CompanyCount} companies, {DepartmentCount} departments, {LocationCount} locations, {EmailAddressCount} email addresses, {LanguageCount} languages and {PhoneNumberCount} phone numbers",
                    userList.Users.Count, userList.Linked?.Companies?.Count ?? 0,
                    userList.Linked?.CompanyDepartments?.Count ?? 0, userList.Linked?.CompanyLocations?.Count ?? 0,
                    userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.Languages?.Count ?? 0,
                    userList.Linked?.PhoneNumbers?.Count ?? 0);
                return userList;
            }

            currentStart += result.Users.Count;
            _logger.LogDebug("Preparing to fetch next batch of users starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogWarning("Reached outside of while somehow 😱 Returning {UserCount} user count with {EmailAddressCount} emailaddresses and {PhoneNumberCount} phone numbers",
            userList.Users.Count, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0);
        return userList;
    }

    public List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> NeedsBasicUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser = null)
    {
        List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> propertiesToUpdate = [];
        
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
        
        if (pureserviceUser.Disabled != (entraUser.AccountEnabled == false))
        {
            propertiesToUpdate.Add(("disabled", (null, null, !entraUser.AccountEnabled)));
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
            ? companyDepartments.Find(d => d.Name.Equals(entraUser.Department, StringComparison.OrdinalIgnoreCase) &&
                                company.Links?.Departments?.Ids is not null && company.Links.Departments.Ids.Contains(d.Id))
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
            ? companyLocations.Find(l => l.Name.Equals(entraUser.OfficeLocation, StringComparison.OrdinalIgnoreCase) &&
                                company.Links?.Locations?.Ids is not null && company.Links.Locations.Ids.Contains(l.Id))
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
}