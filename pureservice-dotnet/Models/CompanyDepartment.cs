namespace pureservice_dotnet.Models;

public class CompanyDepartment : PureserviceBase
{
    public required string Name { get; init; }
    public int? CompanyId { get; init; }
}