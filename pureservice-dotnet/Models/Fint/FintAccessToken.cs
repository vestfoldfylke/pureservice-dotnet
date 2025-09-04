namespace pureservice_dotnet.Models.Fint;

public record FintAccessToken(string AccessToken, string TokenType, int ExpiresIn, string Acr, string Scope);