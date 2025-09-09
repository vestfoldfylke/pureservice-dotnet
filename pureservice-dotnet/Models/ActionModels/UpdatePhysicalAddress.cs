using System.Collections.Generic;

namespace pureservice_dotnet.Models.ActionModels;

public record UpdatePhysicalAddress(List<UpdatePhysicalAddressItem> Physicaladdresses);

public record UpdatePhysicalAddressItem(int Id, string? StreetAddress, string? City, string? PostalCode, string? Country);