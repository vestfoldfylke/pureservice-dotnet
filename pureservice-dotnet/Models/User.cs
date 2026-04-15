using System;
using System.Text.Json.Serialization;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models;

public class User
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? MiddleName { get; init; }
    public string? FullName { get; init; }
    public required string Title { get; init; }
    public string? Location { get; init; } 
    public string? Department { get; init; }
    public string? Notes { get; init; }
    public bool IsAnonymized { get; init; }
    public bool IsSuperuser { get; init; }
    public Links? Links { get; init; }
    public Company? Company { get; init; }
    public int? ManagerId { get; init; }
    public int? CompanyId { get; init; }
    public int? CompanyDepartmentId { get; init; }
    public int? CompanyLocationId { get; init; }
    public int? AddressId { get; init; }
    public int? EmailAddressId { get; init; }
    public int? PhoneNumberId { get; init; }
    public int? CredentialsId { get; init; }
    public int? MfaCredentialsId { get; init; }
    public int? LanguageId { get; init; }
    public int? PictureId { get; init; }
    public UserRole Role { get; init; }
    public int? NotificationScheme { get; init; }
    public int? Type { get; init; }
    public bool HighlightNotifications { get; init; }
    public bool FlushNotifications { get; init; }
    public bool Unavailable { get; init; }
    public int? UnavailableChangedById { get; init; }
    public DateTime? UnavailableChangedByDate { get; init; }
    public bool Disabled { get; init; }
    public string? ImportUniqueKey { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
    public string? ManagerFullName { get; init; }
    
    /// <summary>
    /// CustomField1 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_1")]
    public object? CustomField1 { get; init; }
    
    /// <summary>
    /// CustomField2 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_2")]
    public object? CustomField2 { get; init; }
    
    /// <summary>
    /// CustomField3 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_3")]
    public object? CustomField3 { get; init; }
    
    /// <summary>
    /// CustomField4 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_4")]
    public object? CustomField4 { get; init; }
    
    /// <summary>
    /// CustomField5 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_5")]
    public object? CustomField5 { get; init; }
    
    /// <summary>
    /// CustomField6 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_6")]
    public object? CustomField6 { get; init; }
    
    /// <summary>
    /// CustomField7 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_7")]
    public object? CustomField7 { get; init; }
    
    /// <summary>
    /// CustomField8 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_8")]
    public object? CustomField8 { get; init; }
    
    /// <summary>
    /// CustomField9 can be one of the following types based on what the Custom Field is used for in Pureservice:<br />
    /// - string<br />
    /// - int<br />
    /// - DateTime
    /// </summary>
    [JsonPropertyName("cf_9")]
    public object? CustomField9 { get; init; }
}