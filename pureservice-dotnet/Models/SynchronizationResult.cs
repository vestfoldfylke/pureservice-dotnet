namespace pureservice_dotnet.Models;

public class SynchronizationResult
{
    public int UserMissing { get; set; }
    public int UserMissingImportUniqueKeyCount { get; set; }
    public int UserMissingEmailAddressCount { get; set; }
    public int EmployeeCount { get; set; }
    public int EmployeeManagerMissingInEntraCount { get; set; }
    public int EmployeeManagerErrorCount { get; set; }
    public int EmployeeManagerAddedCount { get; set; }
    public int EmployeeManagerUpToDateCount { get; set; }
    public int StudentCount { get; set; }
    public int StudentPhoneNumberErrorCount { get; set; }
    public int StudentPhoneNumberCreatedAndLinkedCount { get; set; }
    public int StudentPhoneNumberSetAsDefaultCount { get; set; }
    public int StudentPhoneNumberUpdatedCount { get; set; }
    public int StudentPhoneNumberUpToDateCount { get; set; }
}