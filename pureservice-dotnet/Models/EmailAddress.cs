using System;

namespace pureservice_dotnet.Models;

public record EmailAddress(
    string Email,
    int UserId,
    int Id,
    DateTime Created,
    DateTime? Modified,
    int CreatedById,
    int? ModifiedById);