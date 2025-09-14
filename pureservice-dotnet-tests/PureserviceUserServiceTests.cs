using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
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
    public async Task CreateNewUser_Should_Return_New_User(bool allProperties)
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
        const int physicalAddressId = 7;
        const int phoneNumberId = 8;

        var newPureserviceUser = new User
        {
            Id = 42,
            FirstName = entraUser.GivenName,
            LastName = entraUser.Surname,
            Title = entraUser.JobTitle,
            ManagerId = managerId,
            Disabled = !entraUser.AccountEnabled.GetValueOrDefault(allProperties),
            Company = new Company
            {
                Name = "",
                Created = DateTime.Now,
                Id = companyId,
                CreatedById = 1,
                Departments = [],
                Disabled = false,
                Locations = []
            },
            CompanyId = companyId,
            CompanyDepartmentId = null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
            Created = DateTime.Now,
            CreatedById = 1
        };

        _pureserviceCaller.PostAsync<User>(Arg.Is<string>(s => s.StartsWith("user")), Arg.Any<object>())
            .Returns(newPureserviceUser);
        
        var userList = await _service.CreateNewUser(entraUser, managerId, companyId, physicalAddressId, phoneNumberId, emailAddressId);
        
        Assert.NotNull(userList);
    }
    
    [Fact]
    public async Task CreateNewUser_Should_Return_Null_When_User_Not_Created()
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
        const int physicalAddressId = 7;
        const int phoneNumberId = 8;

        _pureserviceCaller.PostAsync<User>(Arg.Is<string>(s => s.StartsWith("user")), Arg.Any<object>())
            .ReturnsNull();
        
        var userList = await _service.CreateNewUser(entraUser, null, companyId, physicalAddressId, phoneNumberId, emailAddressId);
        
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
            LastName = "Bar",
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
                    LastName = "Bar Manager",
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
            LastName = "Bar",
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
            LastName = "Bar Manager",
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
        Assert.Equal(5, result.Count);
        
        // firstName
        Assert.Equal(("firstName", (entraUser.GivenName, null, null)), result[0]);
        
        // lastName
        Assert.Equal(("lastName", (entraUser.Surname, null, null)), result[1]);
        
        // title
        Assert.Equal(("title", (entraUser.JobTitle, null, null)), result[2]);
        
        // managerId
        Assert.Equal(("managerId", (null, pureserviceManagerUser.Id, null)), result[3]);
        
        // disabled
        Assert.Equal(("disabled", (null, null, !entraUser.AccountEnabled)), result[4]);
    }
    
    // NeedsCompanyUpdate
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_Null_When_Company_Update_Not_Needed(bool hasCompany)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = hasCompany ? companyId : null,
            CompanyDepartmentId = null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
            Department = null
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100], "departments"),
                    Locations = new LinkIds([101], "locations")
                }
            }
        };
        
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies);
        Assert.Null(result);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_Company_When_Company_Update_Needed_With_Existing_Company(bool hasCompany)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int newCompanyId = 44;
        const string newCompanyName = "Boz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = hasCompany ? companyId : null,
            CompanyDepartmentId = null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
            Department = null
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100], "departments"),
                    Locations = new LinkIds([101], "locations")
                }
            },
            new Company
            {
                Id = newCompanyId,
                Name = newCompanyName,
                Links = new Links
                {
                    Departments = new LinkIds([102], "departments"),
                    Locations = new LinkIds([103], "locations")
                }
            }
        };
        
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies);
        Assert.NotNull(result);
        Assert.Equal("companyId", result.PropertyName);
        Assert.Equal(newCompanyId, result.Id);
        Assert.Null(result.NameToCreate);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsCompanyUpdate_Should_Return_CompanyUpdateItem_When_Company_Update_Needed_With_NonExisting_Company(bool hasCompany)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const string newCompanyName = "Boz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = hasCompany ? companyId : null,
            CompanyDepartmentId = null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
            Department = null
        };

        var companies = new List<Company>
        {
            new Company
            {
                Id = companyId,
                Name = companyName,
                Links = new Links
                {
                    Departments = new LinkIds([100], "departments"),
                    Locations = new LinkIds([101], "locations")
                }
            }
        };
        
        var result = _service.NeedsCompanyUpdate(pureserviceUser, entraUser, companies);
        Assert.NotNull(result);
        Assert.Equal("companyId", result.PropertyName);
        Assert.Null(result.Id);
        Assert.Equal(newCompanyName, result.NameToCreate);
    }
    
    // NeedsDepartmentUpdate
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsDepartmentUpdate_Should_Return_Null_When_Department_Update_Not_Needed(bool hasDepartment)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = hasDepartment ? departmentId : null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
            Department = hasDepartment ? departmentName : null,
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
                    Locations = new LinkIds([101], "locations")
                }
            }
        };

        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = departmentName
            }
        };
        
        var result = _service.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments);
        Assert.Null(result);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsDepartmentUpdate_Should_Return_Department_When_Department_Update_Needed_With_Existing_Department(bool hasDepartment)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";
        
        const int newDepartmentId = 45;
        const string newDepartmentName = "Boz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = hasDepartment ? departmentId : null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
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
                    Departments = new LinkIds([100, departmentId, newDepartmentId], "departments"),
                    Locations = new LinkIds([101], "locations")
                }
            }
        };

        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = departmentName
            },
            new CompanyDepartment
            {
                Id = newDepartmentId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = newDepartmentName
            }
        };
        
        var result = _service.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments);
        Assert.NotNull(result);
        Assert.Equal("companyDepartmentId", result.PropertyName);
        Assert.Equal(newDepartmentId, result.Id);
        Assert.Null(result.NameToCreate);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsDepartmentUpdate_Should_Return_CompanyUpdateItem_When_Department_Update_Needed_With_NonExisting_Department(bool hasDepartment)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";
        
        const string newDepartmentName = "Boz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = hasDepartment ? departmentId : null,
            CompanyLocationId = null,
            Department = null,
            Location = null,
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
            OfficeLocation = null,
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
                    Locations = new LinkIds([101], "locations")
                }
            }
        };

        var departments = new List<CompanyDepartment>
        {
            new CompanyDepartment
            {
                Id = departmentId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = departmentName
            }
        };
        
        var result = _service.NeedsDepartmentUpdate(pureserviceUser, entraUser, companies, departments);
        Assert.NotNull(result);
        Assert.Equal("companyDepartmentId", result.PropertyName);
        Assert.Null(result.Id);
        Assert.Equal(newDepartmentName, result.NameToCreate);
    }
    
    // NeedsLocationUpdate
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsLocationUpdate_Should_Return_Null_When_Location_Update_Not_Needed(bool hasLocation)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";

        const int locationId = 45;
        const string locationName = "Boz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = hasLocation ? locationId : null,
            Department = null,
            Location = null,
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
            OfficeLocation = hasLocation ? locationName : null,
            Department = departmentName,
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

        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = locationName
            }
        };
        
        var result = _service.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations);
        Assert.Null(result);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsLocationUpdate_Should_Return_Location_When_Location_Update_Needed_With_Existing_Location(bool hasLocation)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";

        const int locationId = 45;
        const string locationName = "Boz";
        
        const int newLocationId = 46;
        const string newLocationName = "Buz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = hasLocation ? locationId : null,
            Department = null,
            Location = null,
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
            OfficeLocation = newLocationName,
            Department = departmentName
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
                    Locations = new LinkIds([101, locationId, newLocationId], "locations")
                }
            }
        };

        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = locationName
            },
            new CompanyLocation
            {
                Id = newLocationId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = newLocationName
            }
        };
        
        var result = _service.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations);
        Assert.NotNull(result);
        Assert.Equal("companyLocationId", result.PropertyName);
        Assert.Equal(newLocationId, result.Id);
        Assert.Null(result.NameToCreate);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NeedsLocationUpdate_Should_Return_CompanyUpdateItem_When_Location_Update_Needed_With_NonExisting_Location(bool hasLocation)
    {
        const int companyId = 43;
        const string companyName = "Baz";
        
        const int departmentId = 44;
        const string departmentName = "IT";

        const int locationId = 45;
        const string locationName = "Boz";
        
        const string newLocationName = "Buz";
        
        var pureserviceUser = new User
        {
            Id = 42,
            FirstName = "Foo",
            LastName = "Bar",
            Title = "Biz",
            ManagerId = null,
            Disabled = false,
            CompanyId = companyId,
            CompanyDepartmentId = departmentId,
            CompanyLocationId = hasLocation ? locationId : null,
            Department = null,
            Location = null,
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
            OfficeLocation = newLocationName,
            Department = departmentName
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

        var locations = new List<CompanyLocation>
        {
            new CompanyLocation
            {
                Id = locationId,
                CompanyId = companyId,
                Created = DateTime.Now,
                CreatedById = 1,
                Name = locationName
            }
        };
        
        var result = _service.NeedsLocationUpdate(pureserviceUser, entraUser, companies, locations);
        Assert.NotNull(result);
        Assert.Equal("companyLocationId", result.PropertyName);
        Assert.Null(result.Id);
        Assert.Equal(newLocationName, result.NameToCreate);
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
        var updateProperties = new List<CompanyUpdateItem>();
        
        var result = await _service.UpdateCompanyProperties(userId, updateProperties);
        Assert.False(result);
    }

    [Theory]
    [InlineData("companyId", 44)]
    [InlineData("companyDepartmentId", 45)]
    [InlineData("companyLocationId", 46)]
    public async Task UpdateCompanyProperties_Should_Return_True_When_Update_Is_Needed(string propertyName, int id)
    {
        const int userId = 42;
        var updateProperties =
            new List<CompanyUpdateItem>
            {
                new CompanyUpdateItem(propertyName, id)
            };
        
        _pureserviceCaller.PatchAsync(Arg.Is<string>($"user/{userId}"), Arg.Any<Dictionary<string, int>>())
            .Returns(true);
        
        var result = await _service.UpdateCompanyProperties(userId, updateProperties);
        Assert.True(result);
    }
}