using System;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models;

public class User
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? MiddleName { get; init; }
    public string? FullName { get; init; }
    public string? Title { get; init; }
    public string? Location { get; init; }
    public string? Department { get; init; }
    public string? Notes { get; init; }
    public bool IsAnonymized { get; init; }
    public bool IsSuperuser { get; init; }
    public Links? Links { get; init; }
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
    public Guid? ImportUniqueKey { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public int? CreatedById { get; init; }
    public int? ModifiedById { get; init; }
    public string? ManagerFullName{ get; init; }
}