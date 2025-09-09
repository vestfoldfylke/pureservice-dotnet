using System.Collections.Generic;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models.ActionModels;

public record AddPhoneNumber(List<NewPhoneNumber> Phonenumbers);

public record NewPhoneNumber(string Number, PhoneNumberType Type);