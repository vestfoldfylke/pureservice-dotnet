using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Linked
{
    [JsonPropertyName("emailaddresses")]
    public List<EmailAddress>? EmailAddresses { get; init; }
    
    [JsonPropertyName("phonenumbers")]
    public List<PhoneNumber>? PhoneNumbers { get; init; }
}