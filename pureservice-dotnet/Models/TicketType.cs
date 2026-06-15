namespace pureservice_dotnet.Models;

public class TicketType : PureserviceBase
{
    public required string Name { get; init; }
    public required bool Default { get; init; }
    public required bool Disabled { get; init; }
    public required int Index { get; init; }
    public required bool IsGlobal { get; init; }
}