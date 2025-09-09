using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.ActionModels;
using pureservice_dotnet.Models.Enums;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet.Services;

public interface IPureserviceUserService
{
    Task<UserList?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId,
        string? departmentName, string? locationName, int? physicalAddressId, int? phoneNumberId, int emailAddressId);
    Task<UserList> GetUser(string filter, string[]? entities = null);
    Task<UserList> GetUser(int userId, string[]? entities = null);
    Task<UserList> GetUsers(string[]? entities = null, int start = 0, int limit = 500, bool includeSystemUsers = false, bool includeInactiveUsers = false);
    List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))> NeedsBasicUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser, User? pureserviceManagerUser = null);

    List<(string propertyName, int? id)> NeedsCompanyUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser,
        List<Company> companies, List<CompanyDepartment> companyDepartments, List<CompanyLocation> companyLocations);
    Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId);
    Task<bool> UpdateBasicProperties(int userId,
        List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)> propertiesToUpdate);
    Task<bool> UpdateCompany(int userId, int companyId);
    Task<bool> UpdateCompanyProperties(int userId, List<(string PropertyName, int? Id)> propertiesToUpdate);
    Task<bool> UpdateDepartmentAndLocation(int userId, int departmentId, int locationId);
    Task<bool> UpdateManager(int userId, int managerId);
    Task<bool> UpdateName(int userId, string firstName, string lastName, string? fullName = null);
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

    public async Task<UserList?> CreateNewUser(Microsoft.Graph.Models.User entraUser, int? managerId, int companyId,
        string? departmentName, string? locationName, int? physicalAddressId, int? phoneNumberId, int emailAddressId)
    {
        List<User> users =
        [
            new User
            {
                FirstName = entraUser.GivenName!,
                MiddleName = "",
                LastName = entraUser.Surname!,
                FullName = entraUser.DisplayName!,
                Unavailable = false,
                Title = entraUser.JobTitle ?? "",
                ManagerId = managerId,
                CompanyId = companyId,
                Notes = null,
                Role = UserRole.Enduser,
                Disabled = entraUser.AccountEnabled == false,
                IsSuperuser = false,
                ImportUniqueKey = entraUser.Id,
                FlushNotifications = true, // NOTE: What does this mean?
                HighlightNotifications = true, // NOTE: What does this mean?
                NotificationScheme = 1, // NOTE: What does this mean?
                LanguageId = 2, // NOTE: Norwegian Bokmål
                EmailAddressId = emailAddressId,
                Location = locationName,
                Department = departmentName,
                Links = new Links
                {
                    Address = physicalAddressId.HasValue ? new Link(physicalAddressId.Value, "physicaladdress") : null,
                    EmailAddress = new Link(emailAddressId, "emailaddress"),
                    PhoneNumber = phoneNumberId.HasValue ? new Link(phoneNumberId.Value, "phonenumber") : null,
                    Company = new Link(companyId, "company")
                }
            }
        ];
        
        var payload = new UserList(users, null);
        
        _logger.LogInformation("Creating new pureservice user with ImportUniqueKey {ImportUniqueKey}", entraUser.Id);
        var result = await _pureserviceCaller.PostAsync<UserList>($"{BasePath}", payload);

        if (result is not null)
        {
            var pureserviceUser = result.Users.FirstOrDefault();
            if (pureserviceUser is not null)
            {
                _logger.LogInformation(
                    "Successfully created new pureservice user with ImportUniqueKey {ImportUniqueKey} and UserId {UserId}",
                    pureserviceUser.ImportUniqueKey, pureserviceUser.Id);
                _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewUser", "Number of users created",
                    (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
                return result;
            }
            
            _logger.LogError("Failed to create new pureservice user with ImportUniqueKey {ImportUniqueKey}: No user returned in result", entraUser.Id);
            return null;
        }
        
        _logger.LogError("Failed to create new pureservice user with ImportUniqueKey {ImportUniqueKey}", entraUser.Id);
        _metricsService.Count($"{Constants.MetricsPrefix}_CreatedNewUser", "Number of users created",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return null;
    }

    public async Task<UserList> GetUser(string filter, string[]? entities = null)
    {
        var endpoint = $"{BasePath}?filter={filter}";
        if (entities != null)
        {
            endpoint += $"&include={string.Join(",", entities)}";
        }
        
        return await _pureserviceCaller.GetAsync<UserList>(endpoint) ?? new UserList([], null);
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
            
            _logger.LogInformation("Fetched {Count} users starting from {Start} with limit {Limit}", result.Users.Count, currentStart, limit);
            userList.Users.AddRange(result.Users);
            
            if (result.Linked?.Companies is not null && userList.Linked?.Companies is not null)
            {
                _logger.LogInformation("Fetched {Count} companies linked to users", result.Linked.Companies.Count);
                userList.Linked.Companies.AddRange(result.Linked.Companies);
            }
            
            if (result.Linked?.CompanyDepartments is not null && userList.Linked?.CompanyDepartments is not null)
            {
                _logger.LogInformation("Fetched {Count} company departments available to users", result.Linked.CompanyDepartments.Count);
                userList.Linked.CompanyDepartments.AddRange(result.Linked.CompanyDepartments);
            }
            
            if (result.Linked?.CompanyLocations is not null && userList.Linked?.CompanyLocations is not null)
            {
                _logger.LogInformation("Fetched {Count} company locations available to users", result.Linked.CompanyLocations.Count);
                userList.Linked.CompanyLocations.AddRange(result.Linked.CompanyLocations);
            }
            
            if (result.Linked?.EmailAddresses is not null && userList.Linked?.EmailAddresses is not null)
            {
                _logger.LogInformation("Fetched {Count} email addresses linked to users", result.Linked.EmailAddresses.Count);
                userList.Linked.EmailAddresses.AddRange(result.Linked.EmailAddresses);
            }
            
            if (result.Linked?.Languages is not null && userList.Linked?.Languages is not null)
            {
                _logger.LogInformation("Fetched {Count} languages available to users", result.Linked.Languages.Count);
                userList.Linked.Languages.AddRange(result.Linked.Languages);
            }
            
            if (result.Linked?.PhoneNumbers is not null && userList.Linked?.PhoneNumbers is not null)
            {
                _logger.LogInformation("Fetched {Count} phone numbers linked to users", result.Linked.PhoneNumbers.Count);
                userList.Linked.PhoneNumbers.AddRange(result.Linked.PhoneNumbers);
            }
            
            if (result.Users.Count == 0)
            {
                _logger.LogInformation("No more users to fetch. Ending at start {Start}. Returning {UserCount} user count with {EmailAddressCount} emailaddresses and {PhoneNumberCount} phone numbers",
                    currentStart, userList.Users.Count, userList.Linked?.EmailAddresses?.Count ?? 0, userList.Linked?.PhoneNumbers?.Count ?? 0);
                return userList;
            }

            currentStart += result.Users.Count;
            _logger.LogInformation("Preparing to fetch next batch of users starting from start {Start} and limit {Limit}", currentStart, limit);
        }
        
        _logger.LogInformation("Reached outside of while somehow 😱 Returning {UserCount} user count with {EmailAddressCount} emailaddresses and {PhoneNumberCount} phone numbers",
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
        
        if (pureserviceUser.FullName != entraUser.DisplayName)
        {
            propertiesToUpdate.Add(("fullName", (entraUser.DisplayName, null, null)));
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

    public List<(string propertyName, int? id)> NeedsCompanyUpdate(User pureserviceUser, Microsoft.Graph.Models.User entraUser,
        List<Company> companies, List<CompanyDepartment> companyDepartments, List<CompanyLocation> companyLocations)
    {
        List<(string propertyName, int? id)> propertiesToUpdate = [];
        
        var companyUpdate = GetCompany(pureserviceUser, entraUser, companies);
        if (companyUpdate.Update)
        {
            propertiesToUpdate.Add(("companyId", companyUpdate.Company?.Id));
        }
        
        var department = GetDepartment(pureserviceUser, entraUser, companyDepartments, companyUpdate.Company);
        if (department.Update)
        {
            propertiesToUpdate.Add(("companyDepartmentId", department.CompanyDepartment?.Id));
        }
        
        var location = GetLocation(pureserviceUser, entraUser, companyLocations, companyUpdate.Company);
        if (location.Update)
        {
            propertiesToUpdate.Add(("companyLocationId", location.CompanyLocation?.Id));
        }
        
        return propertiesToUpdate;
    }
    
    public async Task<bool> RegisterPhoneNumberAsDefault(int userId, int phoneNumberId)
    {
        _logger.LogInformation("Registering PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new RegisterPhoneNumberAsDefault(phoneNumberId));

        if (result)
        {
            _logger.LogInformation("Successfully registered PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to register PhoneNumberId {PhoneNumberId} as default for UserId {UserId}", phoneNumberId, userId);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatePhoneNumberDefault", "Number of phone number default updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateBasicProperties(int userId,
        List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)> propertiesToUpdate)
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

        _logger.LogInformation("Updating {PropertyCount} properties on UserId {UserId}", propertiesToUpdate.Count, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated {PropertyCount} properties on UserId {UserId}", propertiesToUpdate.Count, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedBasicProperties", "Number of basic properties updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update {PropertyCount} properties on UserId {UserId}: {@Properties}", propertiesToUpdate.Count, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedBasicProperties", "Number of basic properties updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateCompany(int userId, int companyId)
    {
        _logger.LogInformation("Updating company on UserId {UserId}", userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new UpdateCompany(companyId));
        
        if (result)
        {
            _logger.LogInformation("Successfully updated company on UserId {UserId} to CompanyId {CompanyId}", userId, companyId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateCompany", "Number of company updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update company on UserId {UserId} to CompanyId {CompanyId}", userId, companyId);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateCompany", "Number of company updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateCompanyProperties(int userId, List<(string PropertyName, int? Id)> propertiesToUpdate)
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

        _logger.LogInformation("Updating {PropertyCount} company properties on UserId {UserId}", propertiesToUpdate.Count, userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated {PropertyCount} company properties on UserId {UserId}", propertiesToUpdate.Count, userId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedCompanyProperties", "Number of company properties updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update {PropertyCount} company properties on UserId {UserId}: {@Properties}", propertiesToUpdate.Count, userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdatedCompanyProperties", "Number of company properties updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    public async Task<bool> UpdateDepartmentAndLocation(int userId, int departmentId, int locationId)
    {
        var payload = new UpdateDepartmentAndLocation(departmentId, locationId);
        
        _logger.LogInformation("Updating department and location on UserId {UserId}", userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated department and location on UserId {UserId}: {@DepartmentAndLocation}", userId, payload);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateDepartmentAndLocation", "Number of department and location updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update department and location on UserId {UserId}: {@DepartmentAndLocation}", userId, new { departmentId, locationId });
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateDepartmentAndLocation", "Number of department and location updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
    
    public async Task<bool> UpdateManager(int userId, int managerId)
    {
        _logger.LogInformation("Updating manager on UserId {UserId} to ManagerId {ManagerId}", userId, managerId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", new UpdateManager(managerId));
        
        if (result)
        {
            _logger.LogInformation("Successfully updated UserId {UserId} with ManagerId {ManagerId}", userId, managerId);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateManager", "Number of manager updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update UserId {UserId} with ManagerId {ManagerId}", userId, managerId);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateManager", "Number of manager updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }
    
    public async Task<bool> UpdateName(int userId, string firstName, string lastName, string? fullName = null)
    {
        fullName ??= $"{firstName} {lastName}";
        
        var payload = new UpdateName(firstName, lastName, fullName);
        
        _logger.LogInformation("Updating name on UserId {UserId}", userId);
        var result = await _pureserviceCaller.PatchAsync($"{BasePath}/{userId}", payload);
        
        if (result)
        {
            _logger.LogInformation("Successfully updated name on UserId {UserId}: {@Name}", userId, payload);
            _metricsService.Count($"{Constants.MetricsPrefix}_UpdateName", "Number of name updates",
                (Constants.MetricsResultLabelName, Constants.MetricsResultSuccessLabelValue));
            return true;
        }
        
        _logger.LogError("Failed to update name on UserId {UserId}: {@Name}", userId, payload);
        _metricsService.Count($"{Constants.MetricsPrefix}_UpdateName", "Number of name updates",
            (Constants.MetricsResultLabelName, Constants.MetricsResultFailedLabelValue));
        return false;
    }

    private (bool Update, Company? Company) GetCompany(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<Company> companies)
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
                return (true, wantedCompany);
            }
            
            if (entraUser.CompanyName is null)
            {
                // user has no company in Entra and should not have one in PureService
                return (false, null);
            }
            
            // user has a company in Entra, but we could not find it in PureService, so it needs to be created
            _logger.LogWarning("Could not find company with name {CompanyName} which UserId {UserId} should have", entraUser.CompanyName, pureserviceUser.Id);
            
            // TODO: Needs to be created and added
            return (true, null);
        }
        
        // company is not null
        if (wantedCompany is not null)
        {
            if (company.Id != wantedCompany.Id)
            {
                return (true, wantedCompany);
            }
            
            _logger.LogInformation("UserId {UserId} has correct CompanyId {CompanyId}", pureserviceUser.Id, company.Id);
            return (false, company);
        }
        
        // company is not null and wantedCompany is null
        if (entraUser.CompanyName is null)
        {
            // user has no company in Entra and should not have one in PureService
            return (true, null);
        }
        
        _logger.LogWarning("Could not find company with name {CompanyName} which UserId {UserId} should have",
            entraUser.CompanyName, pureserviceUser.Id);
            
        // TODO: Needs to be created and added
        return (true, null);
    }

    private (bool Update, CompanyDepartment? CompanyDepartment) GetDepartment(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<CompanyDepartment> companyDepartments, Company? company)
    {
        if (company is null)
        {
            return (pureserviceUser.CompanyDepartmentId is not null, null);
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
                return (true, wantedDepartment);
            }
            
            if (entraUser.Department is null)
            {
                // user has no department in Entra and should not have one in PureService
                return (false, null);
            }
            
            // user has a department in Entra, but we could not find it in PureService, so it needs to be created
            _logger.LogWarning("Could not find department with name {DepartmentName} which UserId {UserId} should have", entraUser.Department, pureserviceUser.Id);
            
            // TODO: Needs to be created and added??? HOW???
            return (true, null);
        }
        
        // department is not null
        if (wantedDepartment is not null)
        {
            if (department.Id != wantedDepartment.Id)
            {
                return (true, wantedDepartment);
            }
            
            _logger.LogInformation("UserId {UserId} has correct DepartmentId {DepartmentId}", pureserviceUser.Id, department.Id);
            return (false, null);
        }
        
        // department is not null and wantedDepartment is null
        if (entraUser.Department is null)
        {
            // user has no department in Entra and should not have one in PureService
            return (true, null);
        }
        
        _logger.LogWarning("Could not find department with name {DepartmentName} which UserId {UserId} should have",
            entraUser.Department, pureserviceUser.Id);
            
        // TODO: Needs to be created and added??? HOW???
        return (true, null);
    }
    
    private (bool Update, CompanyLocation? CompanyLocation) GetLocation(User pureserviceUser, Microsoft.Graph.Models.User entraUser, List<CompanyLocation> companyLocations, Company? company)
    {
        if (company is null)
        {
            return (pureserviceUser.CompanyLocationId is not null, null);
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
                return (true, wantedLocation);
            }
            
            if (entraUser.OfficeLocation is null)
            {
                // user has no location in Entra and should not have one in PureService
                return (false, null);
            }
            
            // user has a location in Entra, but we could not find it in PureService, so it needs to be created
            _logger.LogWarning("Could not find location with name {LocationName} which UserId {UserId} should have", entraUser.OfficeLocation, pureserviceUser.Id);
            
            // TODO: Needs to be created and added??? HOW???
            return (true, null);
        }
        
        // location is not null
        if (wantedLocation is not null)
        {
            if (location.Id != wantedLocation.Id)
            {
                return (true, wantedLocation);
            }
            
            _logger.LogInformation("UserId {UserId} has correct LocationId {LocationId}", pureserviceUser.Id, location.Id);
            return (false, null);
        }
        
        // location is not null and wantedLocation is null
        if (entraUser.OfficeLocation is null)
        {
            // user has no location in Entra and should not have one in PureService
            return (true, null);
        }
        
        _logger.LogWarning("Could not find location with name {LocationName} which UserId {UserId} should have",
            entraUser.OfficeLocation, pureserviceUser.Id);
            
        // TODO: Needs to be created and added??? HOW???
        return (true, null);
    }
}