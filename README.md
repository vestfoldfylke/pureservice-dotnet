# pureservice-dotnet

Synchronization service to add / update / disable / enable user objects in Pureservice

## Properties handled on employees from `Entra ID`

| Property in Pureservice | Property in Source | Description                                                      | Category     | Type   | Default Value |
|------------------------|--------------------|-------------------------------------------------------------------|--------------|--------|---------------|
| firstName              | givenName          | First name                                                        | basic        | string | null          |
| lastName               | surname            | Last name                                                         | basic        | string | null          |
| title                  | jobTitle           | Job title                                                         | basic        | string | null          |
| managerId              | manager.id         | Manager ID for Pureservice user                                   | basic        | int    | null          |
| companyId              | companyName        | Company name                                                      | company      | int    | null          |
| companyDepartmentId    | department         | Company department                                                | company      | int    | null          |
| companyLocationId      | officeLocation     | Company location                                                  | company      | int    | null          |
| emailAddressId         | mail               | Email address                                                     | emailaddress | int    | null          |
| phoneNumberId          | csa.mobile         | Mobile phone number                                               | phonenumber  | int    | null          |
| disabled               | accountEnabled     | Disabled                                                          | basic        | int    | false         |
| languageId             | preferredLanguage  | Language (set to Norwegian for now)                               | basic        | int    | Norwegian     |
| role                   |                    | Role (UserRole) (only set on creation)                            | basic        | int    | Sluttbruker   |
| importUniqueKey        | id                 | Unique key for import (only set on creation)                      | basic        | int    | null          |
| username               | userPrincipalName  | Username (update does not work for users with role Administrator) | basic        | int    | null          |

## Properties handled on students from `Entra ID`

| Property in Pureservice | Property in Source | Description                                                      | Category     | Type   | Default Value |
|------------------------|--------------------|-------------------------------------------------------------------|--------------|--------|---------------|
| firstName              | givenName          | First name                                                        | basic        | string | null          |
| lastName               | surname            | Last name                                                         | basic        | string | null          |
| title                  | jobTitle           | Job title                                                         | basic        | string | null          |
| companyId              | companyName        | Company name                                                      | company      | int    | null          |
| companyDepartmentId    | department         | Company department                                                | company      | int    | null          |
| companyLocationId      | officeLocation     | Company location                                                  | company      | int    | null          |
| emailAddressId         | mail               | Email address                                                     | emailaddress | int    | null          |
| phoneNumberId          | csa.mobile         | Mobile phone number                                               | phonenumber  | int    | null          |
| disabled               | accountEnabled     | Disabled                                                          | basic        | int    | false         |
| languageId             | preferredLanguage  | Language (set to Norwegian for now)                               | basic        | int    | Norwegian     |
| role                   |                    | Role (UserRole) (only set on creation)                            | basic        | int    | Sluttbruker   |
| importUniqueKey        | id                 | Unique key for import (only set on creation)                      | basic        | int    | null          |
| username               | userPrincipalName  | Username (update does not work for users with role Administrator) | basic        | int    | null          |

## Setup

Create a `local.settings.json` file in the `pureservice-dotnet` folder with the following content:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SynchronizeSchedule": "0 */5 * * * *",
    "Serilog_MinimumLevel_Override_Microsoft.Hosting": "Information",
    "Serilog_MinimumLevel_Override_Microsoft.AspNetCore": "Warning",
    "Serilog_Console_MinimumLevel": "Debug",
    "BetterStack_SourceToken": "source-token",
    "BetterStack_Endpoint": "endpoint",
    "BetterStack_MinimumLevel": "Information",
    "MicrosoftTeams_WebhookUrl": "teams-webhook-url",
    "MicrosoftTeams_UseWorkflows": "true",
    "MicrosoftTeams_TitleTemplate": "pureservice-usersync-local-dev",
    "MicrosoftTeams_MinimumLevel": "Error",
    "Pureservice_BaseUrl": "https://instancename.pureservice.com/agent/api/",
    "Pureservice_ApiKey": "your-api-key",
    "Pureservice_Max_Requests_Per_Minute": "100",
    "Pureservice_Wait_When_Max_Requests_Reached": "true",
    "Pureservice_Wait_Seconds": "30",
    "AZURE_CLIENT_ID": "azure-client-id",
    "AZURE_CLIENT_SECRET": "azure-client-secret",
    "AZURE_TENANT_ID": "azure-tenant-id",
    "Employee_Auto_Users_OU": "OU=path,OU=to,OU=auto-users-ou,DC=domain,DC=something,DC=edu",
    "Employee_Auto_Disabled_Users_OU": "OU=path,OU=to,OU=auto-disabled-users-ou,DC=domain,DC=something,DC=edu",
    "Student_Email_Domain": "@school.edu",
    "Student_Job_Titles": "school-job-title;another-school-job-title"
  }
}
```

When deploying to Azure, these settings should be added to the Function App Configuration settings

> [!IMPORTANT]
> Azure App Services does not allow periods (.) in the app setting names.<br />
> So the following settings must be renamed:
> - `Serilog_MinimumLevel_Override_Microsoft.Hosting` **->** `Serilog_MinimumLevel_Override_Microsoft_Hosting`
> - `Serilog_MinimumLevel_Override_Microsoft.AspNetCore` **->** `Serilog_MinimumLevel_Override_Microsoft_AspNetCore`

### Explanation of settings and defaults

`SynchronizeSchedule` contains the [NCRONTAB expression](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#ncrontab-expressions) for when the **Synchronize** function will trigger. Adjust it to your liking

#### Azure App Registration settings

This app requires an Azure App Registration with the following API permissions:
- CustomSecAttributeAssignment.Read.All
- User.Read.All

#### Pureservice settings

This app requires a Pureservice API key which has READ/WRITE persmissions. The API key can be of type `Unlimited` or `Limited`.<br />
If the API key is of type `Limited`, it must have the correct collaboration zone chosen for it.<br />
The API key should have short expiry time and should be rotated often!

If `Pureservice_Wait_When_Max_Requests_Reached` is set to:<br />
- **False**: The function will skip the request (Create, Update or Disable) and continue to next user until requests can be made again
- **True**: The function will wait `Pureservice_Wait_Seconds` seconds and continue where it left off
- Not set: Default is **False**

If `Pureservice_Wait_Seconds` is not set, default is **0** seconds
