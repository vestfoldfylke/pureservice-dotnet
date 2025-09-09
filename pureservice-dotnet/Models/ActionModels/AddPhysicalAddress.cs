using System.Collections.Generic;

namespace pureservice_dotnet.Models.ActionModels;

public record AddPhysicalAddress(List<NewPhysicalAddress> Physicaladdresses);

public record NewPhysicalAddress(string? StreetAddress, string? City, string? PostalCode, string? Country);