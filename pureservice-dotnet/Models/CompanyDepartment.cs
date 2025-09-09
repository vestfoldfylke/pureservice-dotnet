using System;

namespace pureservice_dotnet.Models;

public class CompanyDepartment
{
    public required string Name { get; init; }
    public Links? Links { get; init; }
    public int? CompanyId { get; init; }
    public int Id { get; init; }
    public DateTime Created { get; init; }
    public DateTime? Modified { get; init; }
    public int CreatedById { get; init; }
    public int? ModifiedById { get; init; }
}