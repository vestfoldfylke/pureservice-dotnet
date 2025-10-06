using System;

namespace pureservice_dotnet.Models;

public class Credential
{
    public required string Username { get; init; }
    public DateTime? LoginDate { get; init; }
    public int LoginCount { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public Links? Links { get; init; }
    public int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
}