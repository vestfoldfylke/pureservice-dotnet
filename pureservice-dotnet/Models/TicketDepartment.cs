namespace pureservice_dotnet.Models;

public class TicketDepartment : PureserviceBase
{
    public required string Name { get; init; }
    public required bool Disabled { get; init; }
    public required int Type { get; init; }
    public required int TicketCategoryRequiredType { get; init; }
    public required int ChangeCategoryRequiredType { get; init; }
}