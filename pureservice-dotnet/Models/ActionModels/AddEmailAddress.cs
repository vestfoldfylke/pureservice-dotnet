using System.Collections.Generic;

namespace pureservice_dotnet.Models.ActionModels;

public record AddEmailAddress(List<NewEmailAddress> Emailaddresses);

public record NewEmailAddress(string Email);