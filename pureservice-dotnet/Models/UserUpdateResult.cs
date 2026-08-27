using System.Collections.Generic;

namespace pureservice_dotnet.Models;

public class UserUpdateResult
{
    public List<(string propertyName, (string? stringValue, int? intValue, bool? boolValue))>? BasicPropertiesUpdated { get; set; }
}