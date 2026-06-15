using System;

namespace pureservice_dotnet.Models;

public class PhysicalAddress : PureserviceBase
{
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}