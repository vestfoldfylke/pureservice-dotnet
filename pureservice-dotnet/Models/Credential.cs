using System;

namespace pureservice_dotnet.Models;

public class Credential : PureserviceBase
{
    public required string Username { get; init; }
    public DateTime? LoginDate { get; init; }
    public int LoginCount { get; init; }
}