# pureservice-dotnet

Synchronization service to add / update / disable / enable user objects in Pureservice

## Properties handled on employees from `Entra ID`

| Property in Pureservice | Property in Source | Description                    | Category     | Type   | Default Value |
|------------------------|--------------------|---------------------------------|--------------|--------|---------------|
| fullName               | displayName        | Full name                       | basic        | string | null          |
| firstName              | givenName          | First name                      | basic        | string | null          |
| lastName               | surname            | Last name                       | basic        | string | null          |
| title                  | jobTitle           | Job title                       | basic        | string | null          |
| managerId              | manager.id         | Manager ID for Pureservice user | basic        | int    | null          |
| companyId              | companyName        | Company name                    | company      | int    | null          |
| companyDepartmentId    | department         | Company department              | company      | int    | null          |
| companyLocationId      | officeLocation     | Company location                | company      | int    | null          |
| emailAddressId         | mail               | Email address                   | emailaddress | int    | null          |
| phoneNumberId          | csa.mobile         | Mobile phone number             | phonenumber  | int    | null          |
| languageId             | preferredLanguage  | Language                        | basic        | int    | Norwegian     |
| role                   |                    | Role (UserRole) - Sluttbruker   | basic        | int    | null          |
| disabled               | accountEnabled     | Disabled (true/false)           | basic        | int    | false         |
| importUniqueKey        | id                 | Unique key for import           | basic        | int    | null          |
| username               | userPrincipalName  | Username                        | basic        | int    | null          |

## Properties handled on students from `Entra ID`

| Property in Pureservice | Property in Source | Description                    | Category     | Type   | Default Value |
|------------------------|--------------------|---------------------------------|--------------|--------|---------------|
| fullName               | displayName        | Full name                       | basic        | string | null          |
| firstName              | givenName          | First name                      | basic        | string | null          |
| lastName               | surname            | Last name                       | basic        | string | null          |
| title                  | jobTitle           | Job title                       | basic        | string | null          |
| companyId              | companyName        | Company name                    | company      | int    | null          |
| companyDepartmentId    | department         | Company department              | company      | int    | null          |
| companyLocationId      | officeLocation     | Company location                | company      | int    | null          |
| emailAddressId         | mail               | Email address                   | emailaddress | int    | null          |
| phoneNumberId          | csa.mobile         | Mobile phone number             | phonenumber  | int    | null          |
| languageId             | preferredLanguage  | Language                        | basic        | int    | Norwegian     |
| role                   |                    | Role (UserRole) - Sluttbruker   | basic        | int    | null          |
| disabled               | accountEnabled     | Disabled (true/false)           | basic        | int    | false         |
| importUniqueKey        | id                 | Unique key for import           | basic        | int    | null          |
| username               | userPrincipalName  | Username                        | basic        | int    | null          |

## Setup

Create a `local.settings.json` file in the `pureservice-dotnet` folder with the following content:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "Serilog_MinimumLevel_Override_Microsoft.Hosting": "Information",
    "Serilog_MinimumLevel_Override_Microsoft.AspNetCore": "Warning",
    "Serilog_MinimumLevel_Override_OpenApiTriggerFunction": "Warning",
    "BetterStack_SourceToken": "source-token",
    "BetterStack_Endpoint": "endpoint",
    "BetterStack_MinimumLevel": "Information",
    "MicrosoftTeams_WebhookUrl": "teams-webhook-url",
    "MicrosoftTeams_UseWorkflows": "true",
    "MicrosoftTeams_TitleTemplate": "Something",
    "MicrosoftTeams_MinimumLevel": "Error",
    "Pureservice_BaseUrl": "https://instancename.pureservice.com/agent/api/",
    "Pureservice_ApiKey": "your-api-key",
    "AZURE_CLIENT_ID": "azure-client-id",
    "AZURE_CLIENT_SECRET": "azure-client-secret",
    "AZURE_TENANT_ID": "azure-tenant-id",
    "Employee_Email_Domain": "@yourorg.edu",
    "Student_Email_Domain": "@yourschool.edu"
  }
}
```