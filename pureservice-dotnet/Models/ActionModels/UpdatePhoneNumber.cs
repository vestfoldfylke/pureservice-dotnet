using System.Collections.Generic;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Models.ActionModels;

public record UpdatePhoneNumber(List<UpdatePhoneNumberItem> Phonenumbers);

public record UpdatePhoneNumberItem
{
    public int Id { get; init; }
    public string Number { get; init; }
    public PhoneNumberType Type { get; init; }
    public UpdatePhoneNumberLink Links { get; init; }

    public UpdatePhoneNumberItem(int id, string number, PhoneNumberType type, int userId)
    {
        Id = id;
        Number = number;
        Type = type;
        Links = new UpdatePhoneNumberLink(new UpdatePhoneNumberUser(userId));
    }
}

public record UpdatePhoneNumberLink(UpdatePhoneNumberUser User);

public record UpdatePhoneNumberUser(int Id);