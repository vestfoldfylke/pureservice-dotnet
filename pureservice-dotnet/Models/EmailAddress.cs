using System;

namespace pureservice_dotnet.Models;

public class EmailAddress
{
    public required string Email { get; init; }
    public int? UserId { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
}