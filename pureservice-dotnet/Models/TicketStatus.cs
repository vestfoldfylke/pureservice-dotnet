namespace pureservice_dotnet.Models;

public class TicketStatus : PureserviceBase
{
    public required string Name { get; init; }
    public required string UserDisplayName { get; init; }
    public required int Index { get; init; }
    public required bool Default { get; init; }
    public required bool Disabled { get; init; }
    public required bool IsGlobal { get; init; }
    public required int CoreStatus { get; init; }
    public required int RequestTypeId { get; init; }
}