namespace pureservice_dotnet.Models;

public class Ticket : PureserviceBaseWithCustomFields
{
    public string? Solution { get; init; }
    public string? EmailAddress { get; init; }
    public int? AssignedDepartmentId { get; init; }
    public int? PriorityId { get; init; }
    public int? TicketTypeId { get; init; }
    public int? UserId { get; init; }
    public int? AssignedTeamId { get; init; }
    public int? AssignedAgentId { get; init; }
    public required int RequestNumber { get; init; }
    public string? Subject { get; init; }
    public string? Description { get; init; }
    public int? SourceId { get; init; }
    public int? StatusId { get; init; }
    public int? Category1Id { get; init; }
    public int? Category2Id { get; init; }
    public int? Category3Id { get; init; }
    public int? RequestTypeId { get; init; }
}