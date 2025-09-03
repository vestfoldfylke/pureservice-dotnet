using System.Threading.Tasks;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;

namespace pureservice_dotnet.Services;

public interface IPureservicePhoneNumberService
{
    Task<PhoneNumber?> AddNewPhoneNumberAndLinkToUser(string phoneNumber, PhoneNumberType type, int userId);
    Task<bool> UpdatePhoneNumber(int phoneNumberId, string phoneNumber, PhoneNumberType type, int userId);
}