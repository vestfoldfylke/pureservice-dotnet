using System.Text.Json.Serialization;

namespace pureservice_dotnet.Models.Fint;

public record FintAuthBody(
    [property: JsonPropertyName("grant_type")]
    string GrantType,
    [property: JsonPropertyName("username")]
    string Username,
    [property: JsonPropertyName("password")]
    string Password,
    [property: JsonPropertyName("client_id")]
    string ClientId,
    [property: JsonPropertyName("client_secret")]
    string ClientSecret,
    [property: JsonPropertyName("scope")]
    string Scope);