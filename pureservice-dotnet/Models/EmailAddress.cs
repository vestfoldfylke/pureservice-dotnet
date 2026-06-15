namespace pureservice_dotnet.Models;

public class EmailAddress : PureserviceBase
{
    public required string Email { get; init; }
    public int? UserId { get; init; }
}