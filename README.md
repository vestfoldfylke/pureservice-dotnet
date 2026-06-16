# pureservice-dotnet

## Synchronization timer service to add / update / disable / enable user objects in Pureservice

### Properties handled on employees from `Entra ID`

<b>All properties are kept in sync as long as the user is enabled in Entra ID. When a user is disabled in Entra ID, only the `disabled` property is kept in sync (set to true in Pureservice). When a user is re-enabled in Entra ID, all properties are kept in sync again.</b>

| Property in Pureservice | Property in Source | Description                                                       | Category     | Type   | Default Value |
|-------------------------|--------------------|-------------------------------------------------------------------|--------------|--------|---------------|
| firstName               | givenName          | First name                                                        | basic        | string | null          |
| lastName                | surname            | Last name                                                         | basic        | string | null          |
| title                   | jobTitle           | Job title                                                         | basic        | string | null          |
| managerId               | manager.id         | Manager ID for Pureservice user                                   | basic        | int    | null          |
| companyId               | companyName        | Company name                                                      | company      | int    | null          |
| companyDepartmentId     | department         | Company department                                                | company      | int    | null          |
| companyLocationId       | officeLocation     | Company location                                                  | company      | int    | null          |
| emailAddressId          | mail               | Email address                                                     | emailaddress | int    | null          |
| phoneNumberId           | csa.mobile         | Mobile phone number                                               | phonenumber  | int    | null          |
| cf_1 (Brukertype)       | csa.brukertype     | Custom field 1 (set to Brukertype from Custom Security Attribute) | customfield  | string | null          |
| disabled                | accountEnabled     | Disabled                                                          | basic        | int    | false         |
| languageId              | preferredLanguage  | Language (set to Norwegian for now)                               | basic        | int    | Norwegian     |
| role                    |                    | Role (UserRole) (only set on creation)                            | basic        | int    | Sluttbruker   |
| importUniqueKey         | id                 | Unique key for import (only set on creation)                      | basic        | int    | null          |
| username                | userPrincipalName  | Username (update does not work for users with role Administrator) | basic        | int    | null          |

### Properties handled on students from `Entra ID`

<b>All properties are kept in sync as long as the user is enabled in Entra ID. When a user is disabled in Entra ID, only the `disabled` property is kept in sync (set to true in Pureservice). When a user is re-enabled in Entra ID, all properties are kept in sync again.</b>

| Property in Pureservice | Property in Source | Description                                                       | Category     | Type   | Default Value |
|-------------------------|--------------------|-------------------------------------------------------------------|--------------|--------|---------------|
| firstName               | givenName          | First name                                                        | basic        | string | null          |
| lastName                | surname            | Last name                                                         | basic        | string | null          |
| title                   | jobTitle           | Job title                                                         | basic        | string | null          |
| companyId               | companyName        | Company name                                                      | company      | int    | null          |
| companyDepartmentId     | department         | Company department                                                | company      | int    | null          |
| companyLocationId       | officeLocation     | Company location                                                  | company      | int    | null          |
| emailAddressId          | mail               | Email address                                                     | emailaddress | int    | null          |
| phoneNumberId           | csa.mobile         | Mobile phone number                                               | phonenumber  | int    | null          |
| cf_1 (Brukertype)       | csa.brukertype     | Custom field 1 (set to Brukertype from Custom Security Attribute) | customfield  | string | null          |
| disabled                | accountEnabled     | Disabled                                                          | basic        | int    | false         |
| languageId              | preferredLanguage  | Language (set to Norwegian for now)                               | basic        | int    | Norwegian     |
| role                    |                    | Role (UserRole) (only set on creation)                            | basic        | int    | Sluttbruker   |
| importUniqueKey         | id                 | Unique key for import (only set on creation)                      | basic        | int    | null          |
| username                | userPrincipalName  | Username (update does not work for users with role Administrator) | basic        | int    | null          |

## CreateTicket method

> `POST` tickets/create<br />
> `Content-Type: application/json`<br />
> `Authorization` is done with an API key in the `X-Functions-Key` header. API key is generated on the Azure Function App

This method can be used to create tickets in Pureservice from other systems. It accepts a `CreateTicketPayload` object in the request body, which contains all the necessary information to create a ticket in Pureservice. The payload is then processed and a ticket is created in Pureservice using the Pureservice API.

It will return the created ticket object as the response if the ticket was created successfully, or an error message if there was an issue.

CreateTicketPayload:
```json5
{
  "AdditionalData": { // optional and can contain any and as many primitive properties as you like. If present, the content of this object will be added to the description of the ticket in Pureservice under the header defined in Pureservice_Ticket_ExtraInformation.
    "Address": "Home",
    "Street": "Street 123"
  },
  "OriginatingReference": "12345", // A reference to the ticket in the originating system. If a user is created by this method, the Notes field will contain this reference. This reference will also be added to the description of the ticket in Pureservice under the header "Referanse".
  "TicketMetaData": {
    "AssignedDepartmentName": "Support", // The Pureservice_ApiKey MUST have the correct collaboration zone chosen for this to work.
    "Description": "This is a test ticket created from the API. The content of AdditionalData will be added to the description of the ticket under the header defined in Pureservice_Ticket_ExtraInformation.",
    "PriorityName": "Normal",
    "RequestTypeId": 1, // Always use 1 (ticket)
    "SourceName": "Email",
    "StatusName": "Inbox",
    "Subject": "Test Ticket from API",
    "TicketTypeName": "Question"
  },
  "User": {
    "EmailAddress": "foo@bar.biz",
    "Name": "Foo Bar",
    "PhoneNumber": "+4781549300"
  }
}
```

## Setup

Create a `local.settings.json` file in the `pureservice-dotnet` folder with the following content:
```json5
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobs.Synchronize.Disabled": "false", // optional. Set to true to disable the Synchronize function. Set to false or remove to enable it.
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "SynchronizeSchedule": "0 */5 * * * *",
    "Serilog__MinimumLevel__Override__Microsoft.Hosting": "Information",
    "Serilog__MinimumLevel__Override__Microsoft.AspNetCore": "Warning",
    "Serilog__Console__MinimumLevel": "Debug",
    "BetterStack__SourceToken": "source-token",
    "BetterStack__Endpoint": "endpoint",
    "BetterStack__MinimumLevel": "Information",
    "MicrosoftTeams__WebhookUrl": "teams-webhook-url",
    "MicrosoftTeams__UseWorkflows": "true",
    "MicrosoftTeams__TitleTemplate": "pureservice-usersync-local-dev",
    "MicrosoftTeams__MinimumLevel": "Error",
    "Pureservice_BaseUrl": "https://instancename.pureservice.com/agent/api/",
    "Pureservice_ApiKey": "your-api-key",
    "Pureservice_Max_Requests_Per_Minute": "100",
    "Pureservice_Wait_When_Max_Requests_Reached": "true",
    "Pureservice_Wait_Seconds": "30",
    "Pureservice_Ticket_ExtraInformation": "Extra info", // Optional, defaults to "Ekstra informasjon". Description header added to a ticket when CreateTicketPayload.AdditionalData is present.
    "Pureservice_Ticket_FallbackTypeName": "Question", // Optional, defaults to an empty string. The ticket type fallback name to use if CreateTicketPayload.TicketMetaData.TicketTypeName is not found in Pureservice when creating tickets.
    "Pureservice_Ticket_FallbackPriorityName": "Normal", // Optional, defaults to an empty string. The ticket priority fallback name to use if CreateTicketPayload.TicketMetaData.PriorityName is not found in Pureservice when creating tickets.
    "Pureservice_Ticket_FallbackStatusName": "Inbox", // Optional, defaults to an empty string. The ticket status fallback name to use if CreateTicketPayload.TicketMetaData.StatusName is not found in Pureservice when creating tickets.
    "Pureservice_Ticket_FallbackSourceName": "Email", // Optional, defaults to an empty string. The ticket source fallback name to use if CreateTicketPayload.TicketMetaData.SourceName is not found in Pureservice when creating tickets.
    "Pureservice_Ticket_FallbackAssignedDepartmentName": "Department", // Optional, defaults to an empty string. The ticket assigned department fallback name to use if CreateTicketPayload.TicketMetaData.AssignedDepartmentName is not found in Pureservice when creating tickets.
    "AZURE_CLIENT_ID": "azure-client-id",
    "AZURE_CLIENT_SECRET": "azure-client-secret",
    "AZURE_TENANT_ID": "azure-tenant-id",
    "Employee_Auto_Users_OU": "OU=path,OU=to,OU=auto-users-ou,DC=domain,DC=something,DC=edu",
    "Employee_Auto_Disabled_Users_OU": "OU=path,OU=to,OU=auto-disabled-users-ou,DC=domain,DC=something,DC=edu",
    "Student_Email_Domain": "@school.edu",
    "Student_Job_Titles": "school-job-title;another-school-job-title",
    "User_Type_Custom_Field_Id": "cf_1"
  }
}
```

When deploying to Azure, these settings should be added to the Function App Configuration settings

> [!IMPORTANT]
> Azure App Services does not allow periods (.) in the app setting names.<br />
> So the following settings must be renamed:
> - `Serilog__MinimumLevel__Override__Microsoft.Hosting` **->** `Serilog__MinimumLevel__Override__Microsoft_Hosting`
> - `Serilog__MinimumLevel__Override__Microsoft.AspNetCore` **->** `Serilog__MinimumLevel__Override__Microsoft_AspNetCore`

### Explanation of settings and defaults

`SynchronizeSchedule` contains the [NCRONTAB expression](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-timer?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#ncrontab-expressions) for when the **Synchronize** function will trigger. Adjust it to your liking

#### Azure App Registration settings

This app requires an Azure App Registration with the following API permissions:
- CustomSecAttributeAssignment.Read.All
- User.Read.All

#### Pureservice settings

This app requires a Pureservice API key which has READ/WRITE persmissions. The API key can be of type `Unlimited` or `Limited`.<br />
If the API key is of type `Limited`:
- it must have the correct collaboration zone chosen for it.
- it can NOT change usernames of users with the role `Administrator` in Pureservice!
The API key should have short expiry time and should be rotated often!

If `Pureservice_Wait_When_Max_Requests_Reached` is set to:<br />
- **False**: The function will skip the request (Create, Update or Disable) and continue to next user until requests can be made again
- **True**: The function will wait `Pureservice_Wait_Seconds` seconds and continue where it left off
- Not set: Default is **False**

If `Pureservice_Wait_Seconds` is not set, default is **0** seconds
