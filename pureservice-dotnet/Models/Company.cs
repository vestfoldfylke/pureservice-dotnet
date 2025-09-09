using System;
using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Company
{
    public required string Name { get; init; }
    public int? OrganizationNumber { get; init; }
    public int? CompanyNumber { get; init; }
    public string? Website { get; init; }
    public string? SupportWebsite { get; init; }
    public Links? Links { get; init; }
    [JsonPropertyName("phonenumberId")]
    public int? PhoneNumberId { get; init; }
    public int? EmailAddressId { get; init; }
    public bool Disabled { get; init; }
    public int? UsersCount { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
}