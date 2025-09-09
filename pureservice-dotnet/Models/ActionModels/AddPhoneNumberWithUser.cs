using System.Collections.Generic;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models.ActionModels;

public record AddPhoneNumberWithUser(List<NewPhoneNumberWithUser> Phonenumbers);

public record NewPhoneNumberWithUser(string Number, PhoneNumberType Type, int UserId);