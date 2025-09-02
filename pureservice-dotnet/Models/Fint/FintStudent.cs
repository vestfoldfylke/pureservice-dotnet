using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models.Fint;

public class FintStudent
{
    [JsonPropertyName("feidenavn")]
    public string? FeideName { get; init; }
    [JsonPropertyName("upn")]
    public string? UserPrincipalName { get; init; }
    [JsonPropertyName("kontaktMobiltelefonnummer")]
    public string? Mobilephone { get; init; }
    [JsonPropertyName("privatMobiltelefonnummer")]
    public string? PrivateMobilephone { get; init; }
}