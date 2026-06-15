using System.Text.Json.Nodes;

namespace pureservice_dotnet.Models;

public class CreateTicketUser
{
    public required string EmailAddress { get; init; }
    public required string Name { get; init; }
    public required string PhoneNumber { get; init; }
}

public class CreateTicketMetaData
{
    public required string AssignedDepartmentName { get; init; }
    public required string Description { get; init; }
    public required string PriorityName { get; init; }
    public required int RequestTypeId { get; init; }
    public required string SourceName { get; init; }
    public required string StatusName { get; init; }
    public required string Subject { get; init; }
    public required string TicketTypeName { get; init; }
}

public class CreateTicketPayload
{
    public JsonObject? AdditionalData  { get; init; }
    public required string OriginatingReference { get; init; }
    public required CreateTicketMetaData TicketMetaData { get; init; }
    public required CreateTicketUser User { get; init; }
}