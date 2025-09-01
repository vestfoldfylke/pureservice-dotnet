using System.Collections.Generic;

namespace pureservice_dotnet.Models;

public record UserList(List<User> Users, Linked? Linked);