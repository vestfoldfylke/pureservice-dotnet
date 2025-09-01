using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models;

public class Linked
{
    [JsonPropertyName("phonenumbers")]
    public List<PhoneNumber>? PhoneNumbers { get; init; }
}