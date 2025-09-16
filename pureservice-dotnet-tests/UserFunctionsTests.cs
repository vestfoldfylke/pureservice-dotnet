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
    private readonly IPureserviceUserService _pureserviceUserService;
    
    public UserFunctionsTests()
    {
        _graphService = Substitute.For<IGraphService>();
        _pureserviceCaller = Substitute.For<IPureserviceCaller>();
        _companyService = Substitute.For<IPureserviceCompanyService>();
        _emailAddressService = Substitute.For<IPureserviceEmailAddressService>();
        _phoneNumberService = Substitute.For<IPureservicePhoneNumberService>();
        _pureserviceUserService = Substitute.For<IPureserviceUserService>();
        
        var physicalAddressService = Substitute.For<IPureservicePhysicalAddressService>();

        _service = new UserFunctions(_graphService, Substitute.For<ILogger<UserFunctions>>(), Substitute.For<IMetricsService>(), _pureserviceCaller, _companyService,
            _emailAddressService, _phoneNumberService, physicalAddressService, _pureserviceUserService);
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
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).Returns(new CompanyUpdateItem("companyId", companyId));
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", departmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", locationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        // check that _service.UpdateUser does not throw
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);

        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate only has 1 item (since department and location should not be updated when company is updated)
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 1 &&
            cui.Exists(c => c.PropertyName == "companyId" && c.Id == companyId)));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
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
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).Returns(new CompanyUpdateItem("companyDepartmentId", departmentId));
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).Returns(new CompanyUpdateItem("companyLocationId", locationId));
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        // check that _service.UpdateUser does not throw
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(0, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(1, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);

        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate has 2 items
        await _pureserviceUserService.Received(1).UpdateCompanyProperties(Arg.Is(pureserviceUser.Id), Arg.Is<List<CompanyUpdateItem>>(cui =>
            cui.Count == 2 &&
            cui.Exists(c => c.PropertyName == "companyDepartmentId" && c.Id == departmentId) &&
            cui.Exists(c => c.PropertyName == "companyLocationId" && c.Id == locationId)));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
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
        _pureserviceUserService.NeedsCompanyUpdate(pureserviceUser, entraUser, companies).ReturnsNull();
        _pureserviceUserService.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments).ReturnsNull();
        _pureserviceUserService.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations).ReturnsNull();
        _graphService.GetCustomSecurityAttribute(entraUser, "IDM", "Mobile").ReturnsNull();
        _phoneNumberService.NeedsPhoneNumberUpdate(null, null).Returns((false, null));
        
        // check that _service.UpdateUser does not throw
        var exception = await Record.ExceptionAsync(async () => await _service.UpdateUser(pureserviceUser, entraUser, emailAddress, null, [], null, companies,
            departments, locations, synchronizationResult));
        Assert.Null(exception);
        
        Assert.Equal(1, synchronizationResult.UserHandledCount);
        Assert.Equal(1, synchronizationResult.UserUpToDateCount);
        Assert.Equal(0, synchronizationResult.UserBasicPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserCompanyPropertiesUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserEmailAddressUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserPhoneNumberUpdatedCount);
        Assert.Equal(0, synchronizationResult.UserErrorCount);

        // NOTE: There should be only one call to UpdateCompanyProperties where propertiesToUpdate has 2 items
        await _pureserviceUserService.DidNotReceive().UpdateCompanyProperties(Arg.Any<int>(), Arg.Any<List<CompanyUpdateItem>>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _emailAddressService.DidNotReceive().UpdateEmailAddress(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<int>());
        await _pureserviceUserService.DidNotReceive().RegisterPhoneNumberAsDefault(Arg.Any<int>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().AddNewPhoneNumberAndLinkToUser(Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _phoneNumberService.DidNotReceive().UpdatePhoneNumber(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PhoneNumberType>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddCompany(Arg.Any<string>());
        await _companyService.DidNotReceive().AddDepartment(Arg.Any<string>(), Arg.Any<int>());
        await _companyService.DidNotReceive().AddLocation(Arg.Any<string>(), Arg.Any<int>());
    }
}