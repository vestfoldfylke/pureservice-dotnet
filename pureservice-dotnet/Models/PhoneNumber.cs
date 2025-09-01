using System;

namespace pureservice_dotnet.Models;

public record PhoneNumber(
    string? Number,
    string? NormalizedNumber,
    int? Type,
    int? UserId,
    int? Id,
    DateTime? Created,
    DateTime? Modified,
    int? CreatedById,
    int? ModifiedById);