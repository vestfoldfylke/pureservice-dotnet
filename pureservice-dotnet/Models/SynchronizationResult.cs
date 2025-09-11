namespace pureservice_dotnet.Models;

public class SynchronizationResult
{
    public int CompanyMissingInPureserviceCount { get; set; }
    public int UserHandledCount { get; set; }
    public int UserDisabledCount { get; set; }
    public int UserMissingCompanyNameCount { get; set; }
    public int UserMissingEmailAddressCount { get; set; }
    public int UserErrorCount { get; set; }
    public int UserBasicPropertiesUpdatedCount { get; set; }
    public int UserCompanyPropertiesUpdatedCount { get; set; }
    public int UserEmailAddressUpdatedCount { get; set; }
    public int UserPhoneNumberUpdatedCount { get; set; }
    public int UserCreatedCount { get; set; }
    public int UserUpToDateCount { get; set; }
}
