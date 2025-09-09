using System;

namespace pureservice_dotnet.Models;

public record PhysicalAddress(
    string? StreetAddress,
    string? City,
    string? PostalCode,
    string? Country,
    int Id,
    DateTime Created,
    DateTime? Modified,
    Links? Links,
    int CreatedById,
    int? ModifiedById);