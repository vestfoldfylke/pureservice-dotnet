using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using pureservice_dotnet.Functions;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet_tests;

public class UserFunctionsTests
{
    private readonly UserFunctions _service;
    private readonly IGraphService _graphService;
    private readonly IPureserviceCaller _pureserviceCaller;
    private readonly IPureserviceCompanyService _companyService;
    private readonly IPureserviceEmailAddressService _emailAddressService;
    private readonly IPureservicePhoneNumberService _phoneNumberService;
    private readonly IPureservicePhysicalAddressService _physicalAddressService;
    private readonly IPureserviceUserService _pureserviceUserService;
    
    public UserFunctionsTests()
    {
        _graphService = Substitute.For<IGraphService>();
        _pureserviceCaller = Substitute.For<IPureserviceCaller>();
        _companyService = Substitute.For<IPureserviceCompanyService>();
        _emailAddressService = Substitute.For<IPureserviceEmailAddressService>();
        _phoneNumberService = Substitute.For<IPureservicePhoneNumberService>();
        _physicalAddressService = Substitute.For<IPureservicePhysicalAddressService>();
        _pureserviceUserService = Substitute.For<IPureserviceUserService>();

        _service = new UserFunctions(_graphService, Substitute.For<ILogger<UserFunctions>>(), Substitute.For<IMetricsService>(), _pureserviceCaller, _companyService,
            _emailAddressService, _phoneNumberService, _physicalAddressService, _pureserviceUserService);
    }

    // UpdateUser
    [Theory]
    [InlineData(5, 5, 5)]
    [InlineData(5, 3, 5)]
    [InlineData(1, 1, 1)]
    public async Task UpdateUser_Needs_To_Wait_When_Request_Limit_Is_Reached(int expectedRequestCount, int requestCountLastMinute, int maxRequestsPerMinute)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };

        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };
        
        var needsToWait = expectedRequestCount + requestCountLastMinute > maxRequestsPerMinute;
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((needsToWait, requestCountLastMinute, null));
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, null, [],
            null, [], [], [], synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(0, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        _pureserviceUserService.DidNotReceive().NeedsBasicUpdate(Arg.Any<User>(), Arg.Any<Microsoft.Graph.Models.User>());
        _pureserviceUserService.DidNotReceive().NeedsUsernameUpdate(Arg.Any<Credential>(), Arg.Any<Microsoft.Graph.Models.User>());
        _pureserviceUserService.DidNotReceive().NeedsCompanyUpdate(Arg.Any<User>(), Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<List<Company>>());
        _pureserviceUserService.DidNotReceive().NeedsDepartmentUpdate(Arg.Any<User>(), Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<List<Company>>(), Arg.Any<List<CompanyDepartment>>());
        _pureserviceUserService.DidNotReceive().NeedsLocationUpdate(Arg.Any<User>(), Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<List<Company>>(), Arg.Any<List<CompanyLocation>>());
        _graphService.DidNotReceive().GetCustomSecurityAttribute(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<string>(), Arg.Any<string>());
        _phoneNumberService.DidNotReceive().NeedsPhoneNumberUpdate(Arg.Any<PhoneNumber>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceUserService.DidNotReceive().UpdateUsername(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateCompanyProperties(Arg.Any<int>(), Arg.Any<List<CompanyUpdateItem>>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task UpdateUser_Should_Not_Throw_When_Disabled_User_Is_Enabled_With_Existing_Company_Department_And_Location()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        
        const int companyId = 2;
        const int departmentId = 3;
        const int locationId = 4;
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = entraUser.CompanyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = entraUser.Department,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = entraUser.OfficeLocation,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser).Returns([]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((false, null));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).Returns(new CompanyUpdateItem("companyId", companyId));
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", departmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", locationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);

        Assert.Single(companies);
        Assert.Single(departments);
        Assert.Single(locations);

        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 1 &&
            cui.Exists(c => c.PropertyName == "companyId" && c.Id == companyId)));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceUserService.DidNotReceive().UpdateUsername(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task UpdateUser_Should_Not_Throw_When_Disabled_User_Is_Enabled_With_Existing_Department_And_Location()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        
        const int companyId = 2;
        const int departmentId = 3;
        const int locationId = 4;
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = entraUser.CompanyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = entraUser.Department,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = entraUser.OfficeLocation,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser).Returns([]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((false, null));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", departmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", locationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);

        Assert.Single(companies);
        Assert.Single(departments);
        Assert.Single(locations);
        
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 2 &&
            cui.Exists(c => c.PropertyName == "companyDepartmentId" && c.Id == departmentId) &&
            cui.Exists(c => c.PropertyName == "companyLocationId" && c.Id == locationId)));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceUserService.DidNotReceive().UpdateUsername(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateUser_Should_Not_Throw_When_User_Needs_Update_On_Everything(bool phoneNumberExists)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const int managerId = 8;
        const string mobile = "+4781549300";
        
        const string newFirstName = "Ernst";
        const string newLastName = "Rumpeloersen";
        const string newTitle = "Løkhue";
        const string newEmail = "ernst.rumpeloersen@foo.biz";
        const int newManagerId = 9;
        const string newMobile = "+4781549301";
        
        const int companyId = 2;
        const string companyName = "Foo";
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        const int newCompanyId = 5;
        const string newCompanyName = "Foo5";
        const int newDepartmentId = 6;
        const string newDepartmentName = "Bar6";
        const int newLocationId = 7;
        const string newLocationName = "Biz7";
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            ManagerId = managerId,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var pureserviceManagerUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-43",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = newFirstName,
            Surname = newLastName,
            JobTitle = newTitle,
            CompanyName = newCompanyName,
            Department = newDepartmentName,
            OfficeLocation = newLocationName,
            Mail = newEmail,
            AccountEnabled = true,
            Manager = new Microsoft.Graph.Models.User { Id = newManagerId.ToString() },
            Id = "rr-42",
            UserPrincipalName = newEmail
        };
        
        var phoneNumber = phoneNumberExists
            ? new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now.AddDays(-10), null, 42, null)
            : null;
        
        var newPhoneNumber = new PhoneNumber(newMobile, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now, null, 42, null);

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = companyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            },
            new Company
            {
                Name = newCompanyName,
                Id = newCompanyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = departmentName,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            },
            new CompanyDepartment()
            {
                Name = newDepartmentName,
                Id = newDepartmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = locationName,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            },
            new CompanyLocation()
            {
                Name = newLocationName,
                Id = newLocationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser).Returns([
            ("firstName", (newFirstName, null, null)),
            ("lastName", (newLastName, null, null)),
            ("title", (newTitle, null, null)),
            ("managerId", (null, newManagerId, null)),
            ("disabled", (null, null, false))
        ]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((true, newEmail));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).Returns(new CompanyUpdateItem("companyId", newCompanyId));
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", newDepartmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", newLocationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(newMobile);
        _phoneNumberService.NeedsPhoneNumberUpdate(phoneNumberExists ? phoneNumber : null, newMobile).Returns((true, newMobile));
        _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, Arg.Any<List<(string, (string?, int?, bool?))>>()).Returns(true);
        _pureserviceUserService.UpdateUsername(pureserviceUser.Id, credential.Id, newEmail).Returns(true);
        _emailAddressService.UpdateEmailAddress(emailAddress.Id, entraUser.Mail, pureserviceUser.Id).Returns(true);
        _phoneNumberService.AddNewPhoneNumberAndLinkToUser(newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(newPhoneNumber);
        _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, newPhoneNumber.Id).Returns(true);
        _phoneNumberService.UpdatePhoneNumber(phoneNumberExists ? phoneNumber!.Id : 0, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(true);
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, phoneNumberExists ? phoneNumber : null, [],
            pureserviceManagerUser, companies, departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(1, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        Assert.Equal(2, companies.Count);
        Assert.Equal(2, departments.Count);
        Assert.Equal(2, locations.Count);

        await _pureserviceUserService.Received(1).UpdateBasicProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<(string, (string?, int?, bool?))>>(bui =>
            bui.Count == 5 &&
            bui.Exists(b => b.Item1 == "firstName" && b.Item2.Item1 == newFirstName) &&
            bui.Exists(b => b.Item1 == "lastName" && b.Item2.Item1 == newLastName) &&
            bui.Exists(b => b.Item1 == "title" && b.Item2.Item1 == newTitle) &&
            bui.Exists(b => b.Item1 == "managerId" && b.Item2.Item2 == newManagerId) &&
            bui.Exists(b => b.Item1 == "disabled" && b.Item2.Item3 == false)));
        
        await _pureserviceUserService.Received(1).UpdateUsername(Arg.Is(pureserviceUser.Id), Arg.Is(credential.Id), Arg.Is(newEmail));
        
        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 1 &&
            cui.Exists(c => c.PropertyName == "companyId" && c.Id == newCompanyId)));
        
        await _emailAddressService.Received(1).UpdateEmailAddress(Arg.Is(emailAddress.Id), Arg.Is(entraUser.Mail), Arg.Is(pureserviceUser.Id));
        
        if (phoneNumberExists)
        {
            Assert.NotNull(phoneNumber);
            await _phoneNumberService.Received(1).UpdatePhoneNumber(Arg.Is(phoneNumber.Id), Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            
            await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
            await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        else
        {
            Assert.Null(phoneNumber);
            await _phoneNumberService.Received(1).AddNewPhoneNumberAndLinkToUser(Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            await _pureserviceUserService.Received(1).RegisterPhoneNumberAsDefault(Arg.Is(pureserviceUser.Id), Arg.Is(newPhoneNumber.Id));
            
            await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateUser_Should_Not_Throw_When_User_Needs_Update_On_Everything_Except_Company_With_Existing_Company_And_Location(bool phoneNumberExists)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const int managerId = 8;
        const string mobile = "+4781549300";
        
        const string newFirstName = "Ernst";
        const string newLastName = "Rumpeloersen";
        const string newTitle = "Løkhue";
        const string newEmail = "ernst.rumpeloersen@foo.biz";
        const int newManagerId = 9;
        const string newMobile = "+4781549301";
        
        const int companyId = 2;
        const string companyName = "Foo";
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        const int newDepartmentId = 6;
        const string newDepartmentName = "Bar6";
        const int newLocationId = 7;
        const string newLocationName = "Biz7";
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            ManagerId = managerId,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var pureserviceManagerUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-43",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = newFirstName,
            Surname = newLastName,
            JobTitle = newTitle,
            CompanyName = companyName,
            Department = newDepartmentName,
            OfficeLocation = newLocationName,
            Mail = newEmail,
            AccountEnabled = true,
            Manager = new Microsoft.Graph.Models.User { Id = newManagerId.ToString() },
            Id = "rr-42",
            UserPrincipalName = newEmail
        };
        
        var phoneNumber = phoneNumberExists
            ? new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now.AddDays(-10), null, 42, null)
            : null;
        
        var newPhoneNumber = new PhoneNumber(newMobile, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now, null, 42, null);

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = companyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = departmentName,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            },
            new CompanyDepartment()
            {
                Name = newDepartmentName,
                Id = newDepartmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = locationName,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            },
            new CompanyLocation()
            {
                Name = newLocationName,
                Id = newLocationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser).Returns([
            ("firstName", (newFirstName, null, null)),
            ("lastName", (newLastName, null, null)),
            ("title", (newTitle, null, null)),
            ("managerId", (null, newManagerId, null)),
            ("disabled", (null, null, false))
        ]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((true, newEmail));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", newDepartmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", newLocationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(newMobile);
        _phoneNumberService.NeedsPhoneNumberUpdate(phoneNumberExists ? phoneNumber : null, newMobile).Returns((true, newMobile));
        _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, Arg.Any<List<(string, (string?, int?, bool?))>>()).Returns(true);
        _pureserviceUserService.UpdateUsername(pureserviceUser.Id, credential.Id, newEmail).Returns(true);
        _emailAddressService.UpdateEmailAddress(emailAddress.Id, entraUser.Mail, pureserviceUser.Id).Returns(true);
        _phoneNumberService.AddNewPhoneNumberAndLinkToUser(newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(newPhoneNumber);
        _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, newPhoneNumber.Id).Returns(true);
        _phoneNumberService.UpdatePhoneNumber(phoneNumberExists ? phoneNumber!.Id : 0, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(true);
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, phoneNumberExists ? phoneNumber : null, [],
            pureserviceManagerUser, companies, departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(1, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        Assert.Single(companies);
        Assert.Equal(2, departments.Count);
        Assert.Equal(2, locations.Count);

        await _pureserviceUserService.Received(1).UpdateBasicProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<(string, (string?, int?, bool?))>>(bui =>
            bui.Count == 5 &&
            bui.Exists(b => b.Item1 == "firstName" && b.Item2.Item1 == newFirstName) &&
            bui.Exists(b => b.Item1 == "lastName" && b.Item2.Item1 == newLastName) &&
            bui.Exists(b => b.Item1 == "title" && b.Item2.Item1 == newTitle) &&
            bui.Exists(b => b.Item1 == "managerId" && b.Item2.Item2 == newManagerId) &&
            bui.Exists(b => b.Item1 == "disabled" && b.Item2.Item3 == false)));
        
        await _pureserviceUserService.Received(1).UpdateUsername(Arg.Is(pureserviceUser.Id), Arg.Is(credential.Id), Arg.Is(newEmail));
        
        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 2 &&
            cui.Exists(c => c.PropertyName == "companyDepartmentId" && c.Id == newDepartmentId) &&
            cui.Exists(c => c.PropertyName == "companyLocationId" && c.Id == newLocationId)));
        
        await _emailAddressService.Received(1).UpdateEmailAddress(Arg.Is(emailAddress.Id), Arg.Is(entraUser.Mail), Arg.Is(pureserviceUser.Id));
        
        if (phoneNumberExists)
        {
            Assert.NotNull(phoneNumber);
            await _phoneNumberService.Received(1).UpdatePhoneNumber(Arg.Is(phoneNumber.Id), Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            
            await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
            await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        else
        {
            Assert.Null(phoneNumber);
            await _phoneNumberService.Received(1).AddNewPhoneNumberAndLinkToUser(Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            await _pureserviceUserService.Received(1).RegisterPhoneNumberAsDefault(Arg.Is(pureserviceUser.Id), Arg.Is(newPhoneNumber.Id));
            
            await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateUser_Should_Not_Throw_When_User_Needs_Update_On_Everything_Except_Company_With_NonExisting_Company_And_Location(bool phoneNumberExists)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const int managerId = 8;
        const string mobile = "+4781549300";
        
        const string newFirstName = "Ernst";
        const string newLastName = "Rumpeloersen";
        const string newTitle = "Løkhue";
        const string newEmail = "ernst.rumpeloersen@foo.biz";
        const int newManagerId = 9;
        const string newMobile = "+4781549301";
        
        const int companyId = 2;
        const string companyName = "Foo";
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        const int newDepartmentId = 6;
        const string newDepartmentName = "Bar6";
        const int newLocationId = 7;
        const string newLocationName = "Biz7";
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            ManagerId = managerId,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var pureserviceManagerUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-43",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = newFirstName,
            Surname = newLastName,
            JobTitle = newTitle,
            CompanyName = companyName,
            Department = newDepartmentName,
            OfficeLocation = newLocationName,
            Mail = newEmail,
            AccountEnabled = true,
            Manager = new Microsoft.Graph.Models.User { Id = newManagerId.ToString() },
            Id = "rr-42",
            UserPrincipalName = newEmail
        };
        
        var phoneNumber = phoneNumberExists
            ? new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now.AddDays(-10), null, 42, null)
            : null;
        
        var newPhoneNumber = new PhoneNumber(newMobile, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now, null, 42, null);

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = companyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = departmentName,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var newDepartment = new CompanyDepartment
        {
            Name = newDepartmentName,
            Id = newDepartmentId,
            Created = DateTime.Now,
            CreatedById = 1
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = locationName,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var newLocation = new CompanyLocation
        {
            Name = newLocationName,
            Id = newLocationId,
            Created = DateTime.Now,
            CreatedById = 1
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser).Returns([
            ("firstName", (newFirstName, null, null)),
            ("lastName", (newLastName, null, null)),
            ("title", (newTitle, null, null)),
            ("managerId", (null, newManagerId, null)),
            ("disabled", (null, null, false))
        ]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((true, newEmail));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", null, newDepartmentName));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", null, newLocationName));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(newMobile);
        _phoneNumberService.NeedsPhoneNumberUpdate(phoneNumberExists ? phoneNumber : null, newMobile).Returns((true, newMobile));

        _companyService.AddDepartment(newDepartmentName, companyId).Returns(newDepartment);
        _companyService.AddLocation(newLocationName, companyId).Returns(newLocation);
        _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, Arg.Any<List<(string, (string?, int?, bool?))>>()).Returns(true);
        _pureserviceUserService.UpdateUsername(pureserviceUser.Id, credential.Id, newEmail).Returns(true);
        _emailAddressService.UpdateEmailAddress(emailAddress.Id, entraUser.Mail, pureserviceUser.Id).Returns(true);
        _phoneNumberService.AddNewPhoneNumberAndLinkToUser(newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(newPhoneNumber);
        _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, newPhoneNumber.Id).Returns(true);
        _phoneNumberService.UpdatePhoneNumber(phoneNumberExists ? phoneNumber!.Id : 0, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(true);
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, phoneNumberExists ? phoneNumber : null, [],
            pureserviceManagerUser, companies, departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(1, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        Assert.Single(companies);
        Assert.Equal(2, departments.Count);
        Assert.Equal(2, locations.Count);

        await _pureserviceUserService.Received(1).UpdateBasicProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<(string, (string?, int?, bool?))>>(bui =>
            bui.Count == 5 &&
            bui.Exists(b => b.Item1 == "firstName" && b.Item2.Item1 == newFirstName) &&
            bui.Exists(b => b.Item1 == "lastName" && b.Item2.Item1 == newLastName) &&
            bui.Exists(b => b.Item1 == "title" && b.Item2.Item1 == newTitle) &&
            bui.Exists(b => b.Item1 == "managerId" && b.Item2.Item2 == newManagerId) &&
            bui.Exists(b => b.Item1 == "disabled" && b.Item2.Item3 == false)));
        
        await _pureserviceUserService.Received(1).UpdateUsername(Arg.Is(pureserviceUser.Id), Arg.Is(credential.Id), Arg.Is(newEmail));
        
        await _companyService.Received(1).AddDepartment(Arg.Is(newDepartmentName), Arg.Is(companyId));
        await _companyService.Received(1).AddLocation(Arg.Is(newLocationName), Arg.Is(companyId));
        
        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 2 &&
            cui.Exists(c => c.PropertyName == "companyDepartmentId" && c.Id == newDepartmentId) &&
            cui.Exists(c => c.PropertyName == "companyLocationId" && c.Id == newLocationId)));
        
        await _emailAddressService.Received(1).UpdateEmailAddress(Arg.Is(emailAddress.Id), Arg.Is(entraUser.Mail), Arg.Is(pureserviceUser.Id));
        
        if (phoneNumberExists)
        {
            Assert.NotNull(phoneNumber);
            await _phoneNumberService.Received(1).UpdatePhoneNumber(Arg.Is(phoneNumber.Id), Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            
            await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
            await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        else
        {
            Assert.Null(phoneNumber);
            await _phoneNumberService.Received(1).AddNewPhoneNumberAndLinkToUser(Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            await _pureserviceUserService.Received(1).RegisterPhoneNumberAsDefault(Arg.Is(pureserviceUser.Id), Arg.Is(newPhoneNumber.Id));
            
            await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateUser_Should_Not_Throw_When_User_Needs_Update_On_Everything_With_NonExisting_Company_Without_Company_And_Location(bool phoneNumberExists)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const int managerId = 8;
        const string mobile = "+4781549300";
        
        const string newFirstName = "Ernst";
        const string newLastName = "Rumpeloersen";
        const string newTitle = "Løkhue";
        const string newEmail = "ernst.rumpeloersen@foo.biz";
        const int newManagerId = 9;
        const string newMobile = "+4781549301";
        
        const int companyId = 2;
        const string companyName = "Foo";
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        const int newCompanyId = 6;
        const string newCompanyName = "Bar6";
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            ManagerId = managerId,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var pureserviceManagerUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-43",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = newFirstName,
            Surname = newLastName,
            JobTitle = newTitle,
            CompanyName = newCompanyName,
            Department = departmentName,
            OfficeLocation = locationName,
            Mail = newEmail,
            AccountEnabled = true,
            Manager = new Microsoft.Graph.Models.User { Id = newManagerId.ToString() },
            Id = "rr-42",
            UserPrincipalName = newEmail
        };
        
        var phoneNumber = phoneNumberExists
            ? new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now.AddDays(-10), null, 42, null)
            : null;
        
        var newPhoneNumber = new PhoneNumber(newMobile, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id, 10, DateTime.Now, null, 42, null);

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = companyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var newCompany = new Company
        {
            Name = newCompanyName,
            Id = newCompanyId,
            Created = DateTime.Now,
            CreatedById = 1,
            Disabled = false
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = departmentName,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = locationName,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser).Returns([
            ("firstName", (newFirstName, null, null)),
            ("lastName", (newLastName, null, null)),
            ("title", (newTitle, null, null)),
            ("managerId", (null, newManagerId, null)),
            ("disabled", (null, null, false))
        ]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((true, newEmail));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).Returns(new CompanyUpdateItem("companyId", null, newCompanyName));
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).ReturnsNull();
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).ReturnsNull();
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(newMobile);
        _phoneNumberService.NeedsPhoneNumberUpdate(phoneNumberExists ? phoneNumber : null, newMobile).Returns((true, newMobile));

        _companyService.AddCompany(newCompanyName).Returns(newCompany);
        _pureserviceUserService.UpdateBasicProperties(pureserviceUser.Id, Arg.Any<List<(string, (string?, int?, bool?))>>()).Returns(true);
        _pureserviceUserService.UpdateUsername(pureserviceUser.Id, credential.Id, newEmail).Returns(true);
        _emailAddressService.UpdateEmailAddress(emailAddress.Id, entraUser.Mail, pureserviceUser.Id).Returns(true);
        _phoneNumberService.AddNewPhoneNumberAndLinkToUser(newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(newPhoneNumber);
        _pureserviceUserService.RegisterPhoneNumberAsDefault(pureserviceUser.Id, newPhoneNumber.Id).Returns(true);
        _phoneNumberService.UpdatePhoneNumber(phoneNumberExists ? phoneNumber!.Id : 0, newMobile, PhoneNumberType.Mobile, pureserviceUser.Id).Returns(true);
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, phoneNumberExists ? phoneNumber : null, [],
            pureserviceManagerUser, companies, departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(1, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        Assert.Equal(2, companies.Count);
        Assert.Single(departments);
        Assert.Single(locations);

        await _pureserviceUserService.Received(1).UpdateBasicProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<(string, (string?, int?, bool?))>>(bui =>
            bui.Count == 5 &&
            bui.Exists(b => b.Item1 == "firstName" && b.Item2.Item1 == newFirstName) &&
            bui.Exists(b => b.Item1 == "lastName" && b.Item2.Item1 == newLastName) &&
            bui.Exists(b => b.Item1 == "title" && b.Item2.Item1 == newTitle) &&
            bui.Exists(b => b.Item1 == "managerId" && b.Item2.Item2 == newManagerId) &&
            bui.Exists(b => b.Item1 == "disabled" && b.Item2.Item3 == false)));
        
        await _companyService.Received(1).AddCompany(Arg.Is(newCompanyName));
        
        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 1 &&
            cui.Exists(c => c.PropertyName == "companyId" && c.Id == newCompanyId)));
        
        await _emailAddressService.Received(1).UpdateEmailAddress(Arg.Is(emailAddress.Id), Arg.Is(entraUser.Mail), Arg.Is(pureserviceUser.Id));
        
        if (phoneNumberExists)
        {
            Assert.NotNull(phoneNumber);
            await _phoneNumberService.Received(1).UpdatePhoneNumber(Arg.Is(phoneNumber.Id), Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            
            await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
            await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        else
        {
            Assert.Null(phoneNumber);
            await _phoneNumberService.Received(1).AddNewPhoneNumberAndLinkToUser(Arg.Is(newMobile), Arg.Is(PhoneNumberType.Mobile), Arg.Is(pureserviceUser.Id));
            await _pureserviceUserService.Received(1).RegisterPhoneNumberAsDefault(Arg.Is(pureserviceUser.Id), Arg.Is(newPhoneNumber.Id));
            
            await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        }
        
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task UpdateUser_Should_Not_Do_Anything_When_User_Doesnt_Need_Update()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        
        const int companyId = 2;
        const int departmentId = 3;
        const int locationId = 4;
        
        var pureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = true,
            Id = 42,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = "rr-42",
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId
        };
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };

        var emailAddress = new EmailAddress
        {
            Email = email,
            Id = 43,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var credential = new Credential
        {
            Username = email,
            Created = DateTime.Now.AddDays(-5),
            Id = 44
        };

        var companies = new List<Company>
        {
            new Company
            {
                Name = entraUser.CompanyName,
                Id = companyId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1,
                Disabled = false
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment()
            {
                Name = entraUser.Department,
                Id = departmentId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation()
            {
                Name = entraUser.OfficeLocation,
                Id = locationId,
                Created = DateTime.Now.AddDays(-10),
                CreatedById = 1
            }
        };

        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(5).Returns((false, 0, null));
        _pureserviceUserService.NeedsBasicUpdate(pureserviceUser, entraUser).Returns([]);
        _pureserviceUserService.NeedsUsernameUpdate(credential, entraUser).Returns((false, null));
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).ReturnsNull();
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).ReturnsNull();
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, credential, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(1, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);

        Assert.Single(companies);
        Assert.Single(departments);
        Assert.Single(locations);
        
        await _pureserviceUserService.DidNotReceive().UpdateCompanyProperties(Arg.Any<int>(), Arg.Any<List<CompanyUpdateItem>>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceUserService.DidNotReceive().UpdateUsername(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
    
    // CreateUser
    [Theory]
    [InlineData(5, 5, 5)]
    [InlineData(5, 3, 5)]
    [InlineData(1, 1, 1)]
    public async Task CreateUser_Needs_To_Wait_When_Request_Limit_Is_Reached(int expectedRequestCount, int requestCountLastMinute, int maxRequestsPerMinute)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = new CompanyLocation
        {
            Name = locationName,
            Id = locationId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var needsToWait = expectedRequestCount + requestCountLastMinute > maxRequestsPerMinute;
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((needsToWait, requestCountLastMinute, null));
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(0, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.DidNotReceive().AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        _graphService.DidNotReceive().GetCustomSecurityAttribute(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<string>(), Arg.Any<string>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _emailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateNewUser(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().UpdateDepartmentAndLocation(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task CreateUser_Should_Not_Do_Anything_When_PhysicalAddress_Is_Not_Created()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = new CompanyLocation
        {
            Name = locationName,
            Id = locationId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((false, 0, null));
        _physicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway").ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.Received(1).AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is("Norway"));
        
        _graphService.DidNotReceive().GetCustomSecurityAttribute(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<string>(), Arg.Any<string>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _emailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateNewUser(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().UpdateDepartmentAndLocation(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task CreateUser_Should_Not_Do_Anything_When_PhoneNumber_Is_Not_Created_When_Its_Expected_To_Get_Created()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const string mobile = "+4781549300";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = new CompanyLocation
        {
            Name = locationName,
            Id = locationId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };

        var newPhysicalAddress = new PhysicalAddress(null, null, null, "Norway", 10, DateTime.Now, null, null, 42, null);
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((false, 0, null));
        _physicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway").Returns(newPhysicalAddress);
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(mobile);
        _phoneNumberService.AddNewPhoneNumber(mobile, PhoneNumberType.Mobile).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.Received(1).AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is("Norway"));
        _graphService.Received(1).GetCustomSecurityAttribute(Arg.Is(entraUser), Arg.Is("IDM"), Arg.Is("Mobile"));
        await _phoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(mobile), Arg.Is(PhoneNumberType.Mobile));
        
        await _emailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateNewUser(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().UpdateDepartmentAndLocation(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task CreateUser_Should_Not_Do_Anything_When_EmailAddress_Is_Not_Created()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const string mobile = "+4781549300";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = new CompanyLocation
        {
            Name = locationName,
            Id = locationId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };

        var newPhysicalAddress = new PhysicalAddress(null, null, null, "Norway", 10, DateTime.Now, null, null, 42, null);
        var newPhoneNumber = new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, null, 11, DateTime.Now, null, 42, null);
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((false, 0, null));
        _physicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway").Returns(newPhysicalAddress);
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(mobile);
        _phoneNumberService.AddNewPhoneNumber(mobile, PhoneNumberType.Mobile).Returns(newPhoneNumber);
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.Received(1).AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is("Norway"));
        _graphService.Received(1).GetCustomSecurityAttribute(Arg.Is(entraUser), Arg.Is("IDM"), Arg.Is("Mobile"));
        await _phoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(mobile), Arg.Is(PhoneNumberType.Mobile));
        await _emailAddressService.Received(1).AddNewEmailAddress(Arg.Is(entraUser.UserPrincipalName));
        
        await _pureserviceUserService.DidNotReceive().CreateNewUser(Arg.Any<Microsoft.Graph.Models.User>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().UpdateDepartmentAndLocation(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
    
    [Fact]
    public async Task CreateUser_Should_Not_Do_Anything_When_User_Is_Not_Created()
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const string mobile = "+4781549300";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = "Biz",
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = new CompanyLocation
        {
            Name = locationName,
            Id = locationId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };

        var newPhysicalAddress = new PhysicalAddress(null, null, null, "Norway", 10, DateTime.Now, null, null, 42, null);
        var newPhoneNumber = new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, null, 11, DateTime.Now, null, 42, null);
        var newEmailAddress = new EmailAddress
        {
            Email = email,
            Id = 12,
            Created = DateTime.Now,
            CreatedById = 42
        };
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((false, 0, null));
        _physicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway").Returns(newPhysicalAddress);
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(mobile);
        _phoneNumberService.AddNewPhoneNumber(mobile, PhoneNumberType.Mobile).Returns(newPhoneNumber);
        _emailAddressService.AddNewEmailAddress(entraUser.UserPrincipalName).Returns(newEmailAddress);
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserErrorCount);
        Assert.Equal(0, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.Received(1).AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is("Norway"));
        _graphService.Received(1).GetCustomSecurityAttribute(Arg.Is(entraUser), Arg.Is("IDM"), Arg.Is("Mobile"));
        await _phoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(mobile), Arg.Is(PhoneNumberType.Mobile));
        await _emailAddressService.Received(1).AddNewEmailAddress(Arg.Is(entraUser.UserPrincipalName));
        await _pureserviceUserService.Received(1).CreateNewUser(Arg.Is(entraUser), Arg.Any<int?>(), Arg.Is(companyId), Arg.Is(newPhysicalAddress.Id), Arg.Is(newPhoneNumber.Id), Arg.Is(newEmailAddress.Id));
        
        await _pureserviceUserService.DidNotReceive().UpdateDepartmentAndLocation(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>());
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateUser_Should_Create_User_But_Not_Update_Department_And_Location(bool hasLocation)
    {
        const string firstName = "Ragnvald";
        const string lastName = "Rumpelo";
        const string title = "Supperådgiver";
        const string email = "ragnvald.rumpelo@foo.biz";
        const string mobile = "+4781549300";

        const int companyId = 2;
        const int departmentId = 3;
        const string departmentName = "Bar";
        const int locationId = 4;
        const string locationName = "Biz";
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = firstName,
            Surname = lastName,
            JobTitle = title,
            CompanyName = "Foo",
            Department = "Bar",
            OfficeLocation = hasLocation ? "Biz" : null,
            Mail = email,
            AccountEnabled = true,
            Id = "rr-42",
            UserPrincipalName = email
        };
        
        var department = new CompanyDepartment
        {
            Name = departmentName,
            Id = departmentId,
            Created = DateTime.Now.AddDays(-10),
            CreatedById = 1
        };
        
        var location = hasLocation
            ? new CompanyLocation
                {
                    Name = locationName,
                    Id = locationId,
                    Created = DateTime.Now.AddDays(-10),
                    CreatedById = 1
                }
            : null;

        var newPhysicalAddress = new PhysicalAddress(null, null, null, "Norway", 10, DateTime.Now, null, null, 42, null);
        var newPhoneNumber = new PhoneNumber(mobile, mobile, PhoneNumberType.Mobile, null, 11, DateTime.Now, null, 42, null);
        var newEmailAddress = new EmailAddress
        {
            Email = email,
            Id = 12,
            Created = DateTime.Now,
            CreatedById = 42
        };

        var newPureserviceUser = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Title = title,
            Disabled = false,
            Id = 42,
            Created = DateTime.Now,
            CreatedById = 1,
            FlushNotifications = true,
            HighlightNotifications = true,
            ImportUniqueKey = entraUser.Id,
            IsAnonymized = false,
            IsSuperuser = false,
            Role = UserRole.Enduser,
            Unavailable = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = hasLocation ? locationId : null
        };
        
        var synchronizationResult = new SynchronizationResult();

        _pureserviceCaller.NeedsToWait(Arg.Any<int>()).Returns((false, 0, null));
        _physicalAddressService.AddNewPhysicalAddress(null, null, null, "Norway").Returns(newPhysicalAddress);
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").Returns(mobile);
        _phoneNumberService.AddNewPhoneNumber(mobile, PhoneNumberType.Mobile).Returns(newPhoneNumber);
        _emailAddressService.AddNewEmailAddress(entraUser.UserPrincipalName).Returns(newEmailAddress);
        _pureserviceUserService.CreateNewUser(entraUser, null, companyId, newPhysicalAddress.Id, newPhoneNumber.Id, newEmailAddress.Id).Returns(newPureserviceUser);
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateUser(entraUser, null, companyId, department, location, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(0, synchronizationResult.CompanyMissingInPureserviceCount);
        Assert.Equal(0, synchronizationResult.UserMissingCredentialsCount);
        Assert.Equal(0, synchronizationResult.UserDisabledCount);
        Assert.Equal(0, synchronizationResult.UserMissingCompanyNameCount);
        Assert.Equal(0, synchronizationResult.UserMissingEmailAddressCount);
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserUsernameUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);
        Assert.Equal(1, synchronizationResult.UserCreatedCount);
        
        await _physicalAddressService.Received(1).AddNewPhysicalAddress(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Is("Norway"));
        _graphService.Received(1).GetCustomSecurityAttribute(Arg.Is(entraUser), Arg.Is("IDM"), Arg.Is("Mobile"));
        await _phoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(mobile), Arg.Is(PhoneNumberType.Mobile));
        await _emailAddressService.Received(1).AddNewEmailAddress(Arg.Is(entraUser.UserPrincipalName));
        await _pureserviceUserService.Received(1).CreateNewUser(Arg.Is(entraUser), Arg.Any<int?>(), Arg.Is(companyId), Arg.Is(newPhysicalAddress.Id), Arg.Is(newPhoneNumber.Id), Arg.Is(newEmailAddress.Id));
        
        if (hasLocation)
        {
            await _pureserviceUserService.Received(1).UpdateDepartmentAndLocation(Arg.Is(newPureserviceUser.Id), Arg.Is(departmentId), Arg.Is(locationId));
            return;
        }
        
        await _pureserviceUserService.Received(1).UpdateDepartmentAndLocation(Arg.Is(newPureserviceUser.Id), Arg.Is(departmentId), Arg.Is((int?)null));
    }
}