using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Company : PureserviceBase
{
    public required string Name { get; init; }
    public int? OrganizationNumber { get; init; }
    public int? CompanyNumber { get; init; }
    public string? Website { get; init; }
    public string? SupportWebsite { get; init; }
    [JsonPropertyName("phonenumberId")]
    public int? PhoneNumberId { get; init; }
    public int? EmailAddressId { get; init; }
    public List<CompanyDepartment>? Departments { get; init; }
    public List<CompanyLocation>? Locations { get; init; }
    public bool Disabled { get; init; }
    public int? UsersCount { get; init; }
}