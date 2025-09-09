using System.Collections.Generic;

namespace pureservice_dotnet.Models.ActionModels;

public record UpdateEmailAddress(List<UpdateEmailAddressItem> Emailaddresses);

public record UpdateEmailAddressItem
{
    public int Id { get; init; }
    public string Email { get; init; }
    public UpdateEmailAddressLink Links { get; init; }

    public UpdateEmailAddressItem(int id, string email, int userId)
    {
        Id = id;
        Email = email;
        Links = new UpdateEmailAddressLink(new UpdateEmailAddressUser(userId));
    }
}

public record UpdateEmailAddressLink(UpdateEmailAddressUser User);

public record UpdateEmailAddressUser(int Id);