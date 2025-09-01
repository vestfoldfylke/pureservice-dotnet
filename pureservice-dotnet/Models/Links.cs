using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Links
{
    public Link? Manager { get; init; }
    public Link? Company { get; init; }
    public Link? CompanyDepartment { get; init; }
    public Link? CompanyLocation { get; init; }
    public Link? Address { get; init; }
    public Link? EmailAddress { get; init; }
    [JsonPropertyName("phonenumber")]
    public Link? PhoneNumber { get; init; }
    public Link? Credentials { get; init; }
    public Link? MfaCredentials { get; init; }
    public Link? Language { get; init; }
    public Link? Picture { get; init; }
    public LinkIds? Memberships { get; init; }
    [JsonPropertyName("phonenumbers")]
    public LinkIds? PhoneNumbers { get; init; }
    public LinkIds? EmailAddresses { get; init; }
    public Link? UnavailableChangedBy { get; init; }
    public Link? CreatedBy { get; init; }
    public Link? ModifiedBy { get; init; }
}