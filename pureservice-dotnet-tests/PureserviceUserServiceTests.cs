using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using pureservice_dotnet.Models;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet_tests;

public class PureserviceUserServiceTests
{
    private readonly PureserviceUserService _service;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    public PureserviceUserServiceTests()
    {
        _pureserviceCaller = Substitute.For<IPureserviceCaller>();
        
        _service = new PureserviceUserService(Substitute.For<ILogger<PureserviceUserService>>(),
            Substitute.For<IMetricsService>(), _pureserviceCaller);
    }

    // CreateNewUser
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateNewUser_Should_Return_UserList_With_One_User(bool allProperties)
    {
        int? managerId = allProperties ? 1337 : null;
        
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = allProperties ? "Elev" : "Supperådgiver",
            Manager = allProperties ? new Microsoft.Graph.Models.User { Id = managerId.ToString() } : null,
            AccountEnabled = allProperties,
            Id = "69"
        };

        const int companyId = 42;
        const int emailAddressId = 9;
        
        var departmentName = allProperties ? "IT" : null;
        var locationName = allProperties ? "Oslo" : null;
        int? physicalAddressId = allProperties ? 7 : null;
        int? phoneNumberId = allProperties ? 8 : null;

        var newPureserviceUser = new User
        {
            Id = 42,
            FirstName = entraUser.GivenName,
            MiddleName = "",
            LastName = entraUser.Surname,
            FullName = entraUser.DisplayName,
            Title = entraUser.JobTitle,
            ManagerId = managerId,
            Disabled = !entraUser.AccountEnabled.GetValueOrDefault(allProperties),
            CompanyId = companyId,
            CompanyDepartmentId = null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1
        };

        _pureserviceCaller.PostAsync<UserList>(Arg.Is("user"), Arg.Any<UserList>())
            .Returns(new UserList([newPureserviceUser], new Linked()));
        
        var userList = await _service.CreateNewUser(entraUser, managerId, companyId, departmentName, locationName,
            physicalAddressId, phoneNumberId, emailAddressId);
        
        Assert.NotNull(userList);
        Assert.Single(userList.Users);
    }
    
    [Fact]
    public async Task CreateNewUser_Should_Return_UserList_With_Zero_Users()
    {
        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Supperådgiver",
            AccountEnabled = true,
            Id = "69"
        };

        const int companyId = 42;
        const int emailAddressId = 9;
        const string departmentName = "IT";
        const string locationName = "Oslo";
        
        int? physicalAddressId = 7;
        int? phoneNumberId = 8;

        _pureserviceCaller.PostAsync<UserList>(Arg.Is("user"), Arg.Any<UserList>())
            .Returns(new UserList([], new Linked()));
        
        var userList = await _service.CreateNewUser(entraUser, null, companyId, departmentName, locationName,
            physicalAddressId, phoneNumberId, emailAddressId);
        
        Assert.Null(userList);
    }
    
    // NeedsBasicUpdate
    [Theory]
    [InlineData(1337, "Ragnvald Rumpelo")]
    [InlineData(null, null)]
    public void NeedsBasicUpdate_Should_Return_Empty_List_When_Update_Not_Needed(int? managerId, string? managerFullname)
    {
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = managerId,
            Disabled = false,
            Location = "",
            Department = "",
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = managerFullname
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Biz",
            AccountEnabled = true
        };
        
        var pureserviceManagerUser = !managerId.HasValue
            ? null
            : new User
                {
                    Id = managerId.Value,
                    FirstName = "Foo Manager",
                    MiddleName = "",
                    LastName = "Bar Manager",
                    FullName = "Foo Manager Bar Manager",
                    Title = "Biz Manager",
                    Disabled = false,
                    Location = "",
                    Department = "",
                    Links = new Links(),
                    Created = DateTime.Now,
                    CreatedById = 1
                };

        // all properties are equal, so no update needed
        var result = _service.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser);
        Assert.Empty(result);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsBasicUpdate_Should_Return_NonEmpty_List_When_Update_Is_Needed(bool active)
    {
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = active,
            Location = "",
            Department = "",
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = null
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo 2",
            Surname = "Bar 2",
            DisplayName = "Foo 2 Bar 2",
            JobTitle = "Biz 2",
            AccountEnabled = active
        };
        
        var pureserviceManagerUser = new User
        {
            Id = 1337,
            FirstName = "Foo Manager",
            MiddleName = "",
            LastName = "Bar Manager",
            FullName = "Foo Manager Bar Manager",
            Title = "Biz Manager",
            Disabled = false,
            Location = "",
            Department = "",
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1
        };

        // all properties needs update
        var result = _service.NeedsBasicUpdate(pureserviceUser, entraUser, pureserviceManagerUser);
        Assert.Equal(6, result.Count);
        
        // firstName
        Assert.Equal(("firstName", (entraUser.GivenName, null, null)), result[0]);
        
        // lastName
        Assert.Equal(("lastName", (entraUser.Surname, null, null)), result[1]);
        
        // fullName
        Assert.Equal(("fullName", (entraUser.DisplayName, null, null)), result[2]);
        
        // title
        Assert.Equal(("title", (entraUser.JobTitle, null, null)), result[3]);
        
        // managerId
        Assert.Equal(("managerId", (null, pureserviceManagerUser.Id, null)), result[4]);
        
        // disabled
        Assert.Equal(("disabled", (null, null, !entraUser.AccountEnabled)), result[5]);
    }
    
    // NeedsCompanyUpdate
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_Empty_List_When_Update_Not_Needed(bool hasCompany)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        const int departmentId = 44;
        const string departmentName = "IT";
        const int locationId = 45;
        const string locationName = "Oslo";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = hasCompany ? companyId : null,
            CompanyDepartmentId = hasCompany ? departmentId : null,
            CompanyLocationId = hasCompany ? locationId : null,
            Department = departmentName,
            Location = locationName,
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = null
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Biz",
            AccountEnabled = true,
            CompanyName = hasCompany ? companyName : null,
            OfficeLocation = hasCompany ? locationName : null,
            Department = hasCompany ? departmentName : null
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100, departmentId], "departments"),
                    Locations = new LinkIds([101, locationId], "locations")
                }
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Name = departmentName
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Name = locationName
            }
        };
        
        // all properties are equal, so no update needed
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, departments, locations);
        Assert.Empty(result);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_NonEmpty_List_When_Company_Is_Empty_And_Needs_Update(bool hasCompany)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        const int departmentId = 44;
        const string departmentName = "IT";
        const int locationId = 45;
        const string locationName = "Oslo";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = hasCompany ? companyId : null,
            CompanyDepartmentId = null, //hasCompany ? departmentId : null,
            CompanyLocationId = null, //hasCompany ? locationId : null,
            Department = departmentName,
            Location = locationName,
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = null
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Biz",
            AccountEnabled = true,
            CompanyName = hasCompany ? null : companyName,
            OfficeLocation = null, //hasCompany ? companyLocation : null,
            Department = null //hasCompany ? departmentName : null
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100, departmentId], "departments"),
                    Locations = new LinkIds([101, locationId], "locations")
                }
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Name = departmentName
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Name = locationName
            }
        };
        
        // all properties are equal, so no update needed
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, departments, locations);
        Assert.Single(result);
        
        Assert.Equal(("companyId", hasCompany ? null : companyId), result[0]);
    }
    
    [Fact]
    public void NeedsCompanyUpdate_Should_Return_NonEmpty_List_When_Company_Needs_Change_With_Department_And_Location()
    {
        // current state
        const int companyId = 43;
        const string companyName = "Baz";
        const int departmentId = 44;
        const string departmentName = "IT";
        const int locationId = 45;
        const string locationName = "Oslo";
        
        // new state
        const int newCompanyId = 46;
        const string newCompanyName = "Qux";
        const int newDepartmentId = 47;
        const string newDepartmentName = "HR";
        const int newLocationId = 48;
        const string newLocationName = "Bergen";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId,
            Department = departmentName,
            Location = locationName,
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = null
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Biz",
            AccountEnabled = true,
            CompanyName = newCompanyName,
            OfficeLocation = newLocationName,
            Department = newDepartmentName
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100, departmentId], "departments"),
                    Locations = new LinkIds([101, locationId], "locations")
                }
            },
            new Company
            {
                Id = newCompanyId,
                Name = newCompanyName,
                Links = new Links
                {
                    Departments = new LinkIds([100, newDepartmentId], "departments"),
                    Locations = new LinkIds([101, newLocationId], "locations")
                }
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Name = departmentName
            },
            new CompanyDepartment
            {
                Id = newDepartmentId,
                CompanyId = newCompanyId,
                Name = newDepartmentName
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Name = locationName
            },
            new CompanyLocation
            {
                Id = newLocationId,
                CompanyId = newCompanyId,
                Name = newLocationName
            }
        };
        
        // all properties are equal, so no update needed
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, departments, locations);
        Assert.Equal(3, result.Count);
        
        Assert.Equal(("companyId", newCompanyId), result[0]);
        Assert.Equal(("companyDepartmentId", newDepartmentId), result[1]);
        Assert.Equal(("companyLocationId", newLocationId), result[2]);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_NonEmpty_List_When_Department_Or_Location_Needs_Update(bool departmentNeedsUpdate)
    {
        // current state
        const int companyId = 43;
        const string companyName = "Baz";
        const int departmentId = 44;
        const string departmentName = "IT";
        const int locationId = 45;
        const string locationName = "Oslo";
        
        // new state
        const int newDepartmentId = 46;
        const string newDepartmentName = "HR";
        const int newLocationId = 47;
        const string newLocationName = "Bergen";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            MiddleName = "",
            LastName = "Bar",
            FullName = "Foo Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = locationId,
            Department = departmentName,
            Location = locationName,
            Links = new Links(),
            Created = DateTime.Now,
            CreatedById = 1,
            ManagerFullName = null
        };

        var entraUser = new Microsoft.Graph.Models.User
        {
            GivenName = "Foo",
            Surname = "Bar",
            DisplayName = "Foo Bar",
            JobTitle = "Biz",
            AccountEnabled = true,
            CompanyName = companyName,
            OfficeLocation = departmentNeedsUpdate ? locationName : newLocationName,
            Department = departmentNeedsUpdate ? newDepartmentName : departmentName
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100, departmentId, newDepartmentId], "departments"),
                    Locations = new LinkIds([101, locationId, newLocationId], "locations")
                }
            }
        };
        
        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Name = departmentName
            },
            new CompanyDepartment
            {
                Id = newDepartmentId,
                CompanyId = companyId,
                Name = newDepartmentName
            }
        };
        
        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Name = locationName
            },
            new CompanyLocation
            {
                Id = newLocationId,
                CompanyId = companyId,
                Name = newLocationName
            }
        };
        
        // all properties are equal, so no update needed
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies, departments, locations);
        Assert.Single(result);
        
        if (departmentNeedsUpdate)
        {
            Assert.Equal(("companyDepartmentId", newDepartmentId), result[0]);
            return;
        }
        
        Assert.Equal(("companyLocationId", newLocationId), result[0]);
    }
    
    // UpdateBasicProperties
    [Fact]
    public async Task UpdateBasicProperties_Should_Return_False_When_Update_Is_Not_Needed()
    {
        const int userId = 42;
        var updateProperties = new List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)>();
        
        var result = await _service.UpdateBasicProperties(userId, updateProperties);
        Assert.False(result);
    }
    
    [Theory]
    [InlineData(null)]
    [InlineData(1337)]
    public async Task UpdateBasicProperties_Should_Return_True_When_Update_Is_Needed(int? managerId)
    {
        const int userId = 42;
        var updateProperties =
            new List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)>
            {
                ("firstName", ("Foo", null, null)),
                ("lastName", ("Bar", null, null)),
                ("fullName", ("Foo Bar", null, null)),
                ("title", ("Biz", null, null)),
                ("managerId", (null, managerId, null)),
                ("disabled", (null, null, true))
            };
        
        _pureserviceCaller.PatchAsync(Arg.Is<string>($"user/{userId}"), Arg.Any<Dictionary<string, object?>>())
            .Returns(true);
        
        var result = await _service.UpdateBasicProperties(userId, updateProperties);
        Assert.True(result);
    }
    
    // UpdateCompanyProperties
    [Fact]
    public async Task UpdateCompanyProperties_Should_Return_False_When_Update_Is_Not_Needed()
    {
        const int userId = 42;
        var updateProperties = new List<(string PropertyName, int? Id)>();
        
        var result = await _service.UpdateCompanyProperties(userId, updateProperties);
        Assert.False(result);
    }
    
    [Theory]
    [InlineData(null, 45, 46)]
    [InlineData(44, 45, 46)]
    [InlineData(44, null, 46)]
    [InlineData(44, 45, null)]
    [InlineData(null, null, null)]
    public async Task UpdateCompanyProperties_Should_Return_True_When_Update_Is_Needed(int? companyId, int? departmentId = null, int? locationId = null)
    {
        const int userId = 42;
        var updateProperties =
            new List<(string PropertyName, int? Id)>
            {
                ("companyId", companyId),
                ("companyDepartmentId", departmentId),
                ("companyLocationId", locationId)
            };
        
        _pureserviceCaller.PatchAsync(Arg.Is<string>($"user/{userId}"), Arg.Any<Dictionary<string, object?>>())
            .Returns(true);
        
        var result = await _service.UpdateCompanyProperties(userId, updateProperties);
        Assert.True(result);
    }
}