using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Linked
{
    public List<Company>? Companies { get; init; }
    [JsonPropertyName("companylocations")]
    public List<CompanyLocation>? CompanyLocations { get; init; }
    [JsonPropertyName("companydepartments")]
    public List<CompanyDepartment>? CompanyDepartments { get; init; }
    [JsonPropertyName("emailaddresses")]
    public List<EmailAddress>? EmailAddresses { get; init; }
    public List<Language>? Languages { get; init; }
    [JsonPropertyName("phonenumbers")]
    public List<PhoneNumber>? PhoneNumbers { get; init; }
    [JsonPropertyName("physicaladdresses")]
    public List<PhysicalAddress>? PhysicalAddresses { get; init; }
}