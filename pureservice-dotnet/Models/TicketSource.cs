namespace pureservice_dotnet.Models;

public class TicketSource : PureserviceBase
{
    public required string Name { get; init; }
    public required bool Default { get; init; }
    public required bool DefaultSelfservice { get; init; }
    public required bool DefaultSelfserviceLocation { get; init; }
    public required int Index { get; init; }
    public required bool Disabled { get; init; }
    public required int RequestTypeId { get; init; }
}