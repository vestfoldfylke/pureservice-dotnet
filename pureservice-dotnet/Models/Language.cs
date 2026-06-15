namespace pureservice_dotnet.Models;

public class Language : PureserviceBase
{
    public required string Name { get; init; }
    public bool IsSystemLanguage { get; init; }
    public int? IsCopyOfLanguageId { get; init; }
    public required string LanguageCode { get; init; }
}