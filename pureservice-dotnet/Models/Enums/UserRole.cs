namespace pureservice_dotnet.Models.Enums;

public enum UserRole
{
    None = 0,
    PendingActivate = 1,
    LocationPendingActivate = 2,
    Enduser = 10,
    Agent = 20,
    ZoneAdmin = 25,
    Administrator = 30,
    System = 50
}