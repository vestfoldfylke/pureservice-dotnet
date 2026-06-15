namespace pureservice_dotnet.Models;

public class CompanyLocation : PureserviceBase
{
    public required string Name { get; init; }
    public int? CompanyId { get; init; }
}