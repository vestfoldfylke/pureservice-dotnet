# pureservice-dotnet

Synchronization service to update user objects in Pureservice

## Properties updated on employees

| Property      | Description      | Type   | Default Value | External source |
|---------------|------------------|--------|---------------|-----------------|
| `manager`     | Employee manager | string | null          | `Entra ID`      |

## Properties updated on students

| Property      | Description               | Type   | Default Value | External source |
|---------------|---------------------------|--------|---------------|-----------------|
| `phonenumber` | Phone numbers on students | string | null          | `FINT`          |

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
    "Feide_Name_Domain": "@domain.org",
    "Fint_BaseUrl": "url-to-fint",
    "Fint_Client_Id": "fint-client-id",
    "Fint_Client_Secret": "fint-client-secret",
    "Fint_Username": "fint-username",
    "Fint_Password": "fint-password",
    "Fint_Token_Url": "https://idp.felleskomponent.no/nidp/oauth/nam/token",
    "Fint_Scope": "fint-client",
    "AZURE_CLIENT_ID": "azure-client-id",
    "AZURE_CLIENT_SECRET": "azure-client-secret",
    "AZURE_TENANT_ID": "azure-tenant-id",
    "Student_Email_Domain": "@yourschool.edu"
  }
}
```