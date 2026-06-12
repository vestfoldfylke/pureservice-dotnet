namespace pureservice_dotnet.Models;

public class TicketPayload
{
    public required string Description { get; init; }
    public required Links Links { get; init; }
    public required string OriginatingReference { get; init; }
    public required string Subject { get; init; }
}