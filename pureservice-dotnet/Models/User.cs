using System;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models;

public class User
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string MiddleName { get; init; }
    public required string FullName { get; init; }
    public required string Title { get; init; }
    public required string Location { get; init; }
    public required string Department { get; init; }
    public string? Notes { get; init; }
    public bool IsAnonymized { get; init; }
    public bool IsSuperuser { get; init; }
    public required Links Links { get; init; }
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
    public required int Id { get; init; }
    public required DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public required int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
    public required string ManagerFullName{ get; init; }
}