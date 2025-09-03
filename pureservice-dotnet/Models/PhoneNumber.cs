using System;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models;

public record PhoneNumber(
    string Number,
    string? NormalizedNumber,
    PhoneNumberType? Type,
    int? UserId,
    int Id,
    DateTime Created,
    DateTime? Modified,
    int CreatedById,
    int? ModifiedById);