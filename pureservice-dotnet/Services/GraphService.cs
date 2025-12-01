using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Serialization;
using Vestfold.Extensions.Authentication.Services;

namespace pureservice_dotnet.Services;

public interface IGraphService
{
    string? GetCustomSecurityAttribute(User user, string attributeGroupName, string attributeName);
    Task<List<User>> GetEmployees();
    Task<List<User>> GetStudents();
}

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;

    private readonly string _employeeAutoUsersOu;
    private readonly string _employeeAutoDisabledUsersOu;
    private readonly string _studentUserDomain;
    private readonly string[] _studentJobTitles;
    
    private readonly string[] _userProperties =
    [
        "displayName",
        "givenName",
        "surname",
        "jobTitle",
        "companyName",
        "department",
        "officeLocation",
        "mail",
        "customSecurityAttributes",
        "preferredLanguage",
        "accountEnabled",
        "id",
        "userPrincipalName"
    ];
    
    public GraphService(IAuthenticationService authenticationService, IConfiguration configuration)
    {
        _graphClient = authenticationService.CreateGraphClient();
        
        _employeeAutoUsersOu = configuration["Employee_Auto_Users_OU"] ?? throw new InvalidOperationException("Employee_Auto_Users_OU not configured");
        _employeeAutoDisabledUsersOu = configuration["Employee_Auto_Disabled_Users_OU"] ?? throw new InvalidOperationException("Employee_Auto_Disabled_Users_OU not configured");
        _studentUserDomain = configuration["Student_Email_Domain"] ?? throw new InvalidOperationException("Student_Email_Domain not configured");
        _studentJobTitles = configuration["Student_Job_Titles"]?.Split(";") ?? throw new InvalidOperationException("Student_Job_Titles not configured");
    }

    public async Task<List<User>> GetEmployees()
    {
        List<User> allUsers = [];

        // NOTE: When $expand is used, Microsoft has a hard limit of 100 users per page. Adding $top=999 has no effect!
        var allEmployees = await GetUsersPage(
            $"https://graph.microsoft.com/v1.0/users?$filter=endsWith(onPremisesDistinguishedName, '{_employeeAutoUsersOu}') OR endsWith(onPremisesDistinguishedName, '{_employeeAutoDisabledUsersOu}')&$count=true&$select={string.Join(",", _userProperties)}&$expand=manager($levels=1;$select=id)&$top=999");

        allUsers.AddRange(allEmployees.Value ?? []);
        
        while (allEmployees.OdataNextLink is not null)
        {
            allEmployees = await GetUsersPage(allEmployees.OdataNextLink);
            allUsers.AddRange(allEmployees.Value ?? []);
        }

        return allUsers;
    }
    
    public async Task<List<User>> GetStudents()
    {
        List<User> allUsers = [];
        
        var allEmployees = await GetUsersPage(
            $"https://graph.microsoft.com/v1.0/users?$filter=endsWith(userPrincipalName, '{_studentUserDomain}') AND jobTitle in ('{string.Join("', '", _studentJobTitles)}')&$count=true&$select={string.Join(",", _userProperties)}&$top=999");

        allUsers.AddRange(allEmployees.Value ?? []);
        
        while (allEmployees.OdataNextLink is not null)
        {
            allEmployees = await GetUsersPage(allEmployees.OdataNextLink);
            allUsers.AddRange(allEmployees.Value ?? []);
        }

        return allUsers;
    }

    private async Task<UserCollectionResponse> GetUsersPage(string requestUrl)
    {
        var usersRequestBuilder = _graphClient.Users.WithUrl(requestUrl);
        var users = await usersRequestBuilder.GetAsync(request =>
            {
                request.Headers.Add("ConsistencyLevel", "eventual");
            });
        return users ?? new UserCollectionResponse();
    }

    public string? GetCustomSecurityAttribute(User user, string attributeGroupName, string attributeName)
    {
        if (user.CustomSecurityAttributes is null)
        {
            return null;
        }
        
        var csa = user.CustomSecurityAttributes.AdditionalData.TryGetValue(attributeGroupName, out var attributeGroupObject)
            ? attributeGroupObject as UntypedObject
            : null;
        if (csa is null)
        {
            return null;
        }
        
        var csaValue = csa.GetValue();
        var attributeItem = csaValue.TryGetValue(attributeName, out var attributeNameItem)
            ? attributeNameItem as UntypedString
            : null;

        return attributeItem?.GetValue();
    }
}