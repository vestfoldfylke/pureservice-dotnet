using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using pureservice_dotnet.Functions;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;

namespace pureservice_dotnet_tests;

public class TicketFunctionsTests
{
    private readonly TicketFunctions _service;
    private readonly IPureserviceEmailAddressService _pureserviceEmailAddressService;
    private readonly IPureservicePhoneNumberService _pureservicePhoneNumberService;
    private readonly IPureserviceTicketService _pureserviceTicketService;
    private readonly IPureserviceUserService _pureserviceUserService;
    
    private readonly string _ticketTypeFallbackName;
    private readonly string _ticketPriorityFallbackName;
    private readonly string _ticketStatusFallbackName;
    private readonly string _ticketSourceFallbackName;
    private readonly string _ticketAssignedDepartmentFallbackName;

    public TicketFunctionsTests()
    {
        _pureserviceEmailAddressService = Substitute.For<IPureserviceEmailAddressService>();
        _pureservicePhoneNumberService = Substitute.For<IPureservicePhoneNumberService>();
        _pureserviceTicketService = Substitute.For<IPureserviceTicketService>();
        _pureserviceUserService = Substitute.For<IPureserviceUserService>();
        
        var ticketFunctionsConfiguration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        _service = new TicketFunctions(ticketFunctionsConfiguration, Substitute.For<ILogger<TicketFunctions>>(), _pureserviceEmailAddressService, _pureservicePhoneNumberService,
            _pureserviceTicketService, _pureserviceUserService);
        
        _ticketTypeFallbackName = ticketFunctionsConfiguration["Pureservice_Ticket_FallbackTypeName"] ?? throw new InvalidOperationException("Pureservice_Ticket_FallbackTypeName configuration value is not set");
        _ticketPriorityFallbackName = ticketFunctionsConfiguration["Pureservice_Ticket_FallbackPriorityName"] ?? throw new InvalidOperationException("Pureservice_Ticket_FallbackPriorityName configuration value is not set");
        _ticketStatusFallbackName = ticketFunctionsConfiguration["Pureservice_Ticket_FallbackStatusName"] ?? throw new InvalidOperationException("Pureservice_Ticket_FallbackStatusName configuration value is not set");
        _ticketSourceFallbackName = ticketFunctionsConfiguration["Pureservice_Ticket_FallbackSourceName"] ?? throw new InvalidOperationException("Pureservice_Ticket_FallbackSourceName configuration value is not set");
        _ticketAssignedDepartmentFallbackName = ticketFunctionsConfiguration["Pureservice_Ticket_FallbackAssignedDepartmentName"] ?? throw new InvalidOperationException("Pureservice_Ticket_FallbackAssignedDepartmentName configuration value is not set");
    }
    
    // CreateTicket
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"TicketMetaData\":{\"Subject\":\"Test\"}}")]
    public async Task CreateTicket_Should_Return_BadRequest_When_Missing_Or_Invalid_Payload(string? payload)
    {
        var request = CreateHttpRequest(payload);
        
        var result = await _service.CreateTicket(request);
        
        Assert.IsType<BadRequestObjectResult>(result);
        
        await _pureserviceUserService.DidNotReceive().GetUserByEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_BadRequest_When_Disabled_User_Failed_To_ReEnable()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = true,
            Id = 1
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceUserService.UpdateBasicProperties(Arg.Is(user.Id), Arg.Is<List<(string, (string?, int?, bool?))>>([("disabled", (null, null, false))])).Returns(false);
        
        var result = await _service.CreateTicket(request);
        
        Assert.IsType<BadRequestObjectResult>(result);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceUserService.Received(1).UpdateBasicProperties(Arg.Is(user.Id), Arg.Is<List<(string PropertyName, (string? StringValue, int? IntValue, bool? BoolValue) PropertyValue)>>(l =>
            l.Count == 1 && l[0].PropertyName == "disabled" && l[0].PropertyValue.StringValue == null && l[0].PropertyValue.IntValue == null && l[0].PropertyValue.BoolValue == false));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_BadRequest_When_User_Doesnt_Exist_And_Failed_To_Add_New_PhoneNumber()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).ReturnsNull();
        _pureservicePhoneNumberService.AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile)).ReturnsNull();
        
        var result = await _service.CreateTicket(request);
        
        Assert.IsType<BadRequestObjectResult>(result);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureservicePhoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_BadRequest_When_User_Doesnt_Exist_And_Failed_To_Add_New_EmailAddress()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var phoneNumber = new PhoneNumber(
            payload.User.PhoneNumber,
            payload.User.PhoneNumber,
            PhoneNumberType.Mobile,
            1,
            2,
            DateTime.Now,
            null,
            42,
            null);

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).ReturnsNull();
        _pureservicePhoneNumberService.AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile)).Returns(phoneNumber);
        _pureserviceEmailAddressService.AddNewEmailAddress(Arg.Is(payload.User.EmailAddress)).ReturnsNull();
        
        var result = await _service.CreateTicket(request);
        
        Assert.IsType<BadRequestObjectResult>(result);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureservicePhoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile));
        await _pureserviceEmailAddressService.Received(1).AddNewEmailAddress(Arg.Is(payload.User.EmailAddress));
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_BadRequest_When_User_Doesnt_Exist_And_Failed_To_Add_New_User()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var phoneNumber = new PhoneNumber(
            payload.User.PhoneNumber,
            payload.User.PhoneNumber,
            PhoneNumberType.Mobile,
            1,
            2,
            DateTime.Now,
            null,
            42,
            null);

        var emailAddress = new EmailAddress
        {
            Email = payload.User.EmailAddress,
            Id = 3,
            UserId = 1
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).ReturnsNull();
        _pureservicePhoneNumberService.AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile)).Returns(phoneNumber);
        _pureserviceEmailAddressService.AddNewEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(emailAddress);
        _pureserviceUserService.CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Is(phoneNumber.Id), Arg.Is(emailAddress.Id), Arg.Any<string>()).ReturnsNull();
        
        var result = await _service.CreateTicket(request);
        
        Assert.IsType<BadRequestObjectResult>(result);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureservicePhoneNumberService.Received(1).AddNewPhoneNumber(Arg.Is(payload.User.PhoneNumber), Arg.Is(PhoneNumberType.Mobile));
        await _pureserviceEmailAddressService.Received(1).AddNewEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceUserService.Received(1).CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Is(phoneNumber.Id), Arg.Is(emailAddress.Id), Arg.Any<string>());
        
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Throw_When_Enabled_User_And_Invalid_TicketType_And_Fallback()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).ReturnsNull();
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName)).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Throw_When_Enabled_User_And_Invalid_Priority_And_Fallback()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).ReturnsNull();
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName)).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Throw_When_Enabled_User_And_Invalid_Status_And_Fallback()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).ReturnsNull();
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName)).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Throw_When_Enabled_User_And_Invalid_Source_And_Fallback()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        var ticketStatus = new TicketStatus
        {
            Name = payload.TicketMetaData.StatusName,
            UserDisplayName = payload.TicketMetaData.StatusName,
            Index = 1,
            Default = false,
            Disabled = false,
            IsGlobal = false,
            CoreStatus = 42,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 4
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).Returns(ticketStatus);
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName)).ReturnsNull();
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName)).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Any<string>());
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Throw_When_Enabled_User_And_Invalid_AssignedDepartment_And_Fallback()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        var ticketStatus = new TicketStatus
        {
            Name = payload.TicketMetaData.StatusName,
            UserDisplayName = payload.TicketMetaData.StatusName,
            Index = 1,
            Default = false,
            Disabled = false,
            IsGlobal = false,
            CoreStatus = 42,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 4
        };

        var ticketSource = new TicketSource
        {
            Name = payload.TicketMetaData.SourceName,
            Default = true,
            DefaultSelfservice = true,
            DefaultSelfserviceLocation = true,
            Index = 1,
            Disabled = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 5
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).Returns(ticketStatus);
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName)).Returns(ticketSource);
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName)).ReturnsNull();
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName)).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName));
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName));
        await _pureserviceTicketService.DidNotReceive().CreateTicket(Arg.Any<TicketPayload>());
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_BadRequest_When_Enabled_User_And_All_TicketMetaData_Valid_And_CreateTicket_Failed()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        var ticketStatus = new TicketStatus
        {
            Name = payload.TicketMetaData.StatusName,
            UserDisplayName = payload.TicketMetaData.StatusName,
            Index = 1,
            Default = false,
            Disabled = false,
            IsGlobal = false,
            CoreStatus = 42,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 4
        };

        var ticketSource = new TicketSource
        {
            Name = payload.TicketMetaData.SourceName,
            Default = true,
            DefaultSelfservice = true,
            DefaultSelfserviceLocation = true,
            Index = 1,
            Disabled = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 5
        };

        var ticketDepartment = new TicketDepartment
        {
            Name = payload.TicketMetaData.AssignedDepartmentName,
            Disabled = false,
            Type = 1,
            TicketCategoryRequiredType = 0,
            ChangeCategoryRequiredType = 0,
            Id = 6
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).Returns(ticketStatus);
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName)).Returns(ticketSource);
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName)).Returns(ticketDepartment);
        _pureserviceTicketService.CreateTicket(Arg.Any<TicketPayload>()).ReturnsNull();
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(request));
        Assert.Null(exception);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName));
        await _pureserviceTicketService.Received(1).CreateTicket(Arg.Any<TicketPayload>());
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName));
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_Successful_When_Enabled_User_And_All_TicketMetaData_Valid_And_CreateTicket_Successful()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        var ticketStatus = new TicketStatus
        {
            Name = payload.TicketMetaData.StatusName,
            UserDisplayName = payload.TicketMetaData.StatusName,
            Index = 1,
            Default = false,
            Disabled = false,
            IsGlobal = false,
            CoreStatus = 42,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 4
        };

        var ticketSource = new TicketSource
        {
            Name = payload.TicketMetaData.SourceName,
            Default = true,
            DefaultSelfservice = true,
            DefaultSelfserviceLocation = true,
            Index = 1,
            Disabled = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 5
        };

        var ticketDepartment = new TicketDepartment
        {
            Name = payload.TicketMetaData.AssignedDepartmentName,
            Disabled = false,
            Type = 1,
            TicketCategoryRequiredType = 0,
            ChangeCategoryRequiredType = 0,
            Id = 6
        };

        var ticket = new Ticket
        {
            RequestNumber = 42,
            EmailAddress = payload.User.EmailAddress,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Subject = payload.TicketMetaData.Subject,
            Description = payload.TicketMetaData.Description
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).Returns(ticketStatus);
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName)).Returns(ticketSource);
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName)).Returns(ticketDepartment);
        _pureserviceTicketService.CreateTicket(Arg.Any<TicketPayload>()).Returns(ticket);

        IActionResult? result = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            result = await _service.CreateTicket(request);
        });
        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.IsType<JsonResult>(result);
        
        var jsonResult = result as JsonResult;
        Assert.NotNull(jsonResult);
        Assert.NotNull(jsonResult.Value);
        
        var createdTicket = jsonResult.Value as Ticket;
        Assert.NotNull(createdTicket);
        Assert.Equal(ticket.RequestNumber, createdTicket.RequestNumber);
        Assert.Equal(ticket.EmailAddress, createdTicket.EmailAddress);
        Assert.Equal(ticket.RequestTypeId, createdTicket.RequestTypeId);
        Assert.Equal(ticket.Subject, createdTicket.Subject);
        Assert.Equal(ticket.Description, createdTicket.Description);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName));
        await _pureserviceTicketService.Received(1).CreateTicket(Arg.Any<TicketPayload>());
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
        
        await _pureserviceTicketService.DidNotReceive().GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName));
        await _pureserviceTicketService.DidNotReceive().GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName));
    }
    
    [Fact]
    public async Task CreateTicket_Should_Return_Successful_When_Enabled_User_And_All_TicketMetaData_Invalid_And_All_Fallback_Valid_And_CreateTicket_Successful()
    {
        var payload = ValidPayload();
        var request = CreateHttpRequest(JsonSerializer.Serialize(payload));

        var user = new User
        {
            FirstName = payload.User.Name,
            LastName = "",
            Title = "",
            Disabled = false,
            Id = 1
        };

        var ticketType = new TicketType
        {
            Name = payload.TicketMetaData.TicketTypeName,
            Default = true,
            Disabled = false,
            Index = 1,
            IsGlobal = false,
            Id = 2
        };

        var ticketPriority = new TicketPriority
        {
            Name = payload.TicketMetaData.PriorityName,
            Color = "",
            Default = true,
            Index = 1,
            Disabled = false,
            IsGlobal = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 3
        };

        var ticketStatus = new TicketStatus
        {
            Name = payload.TicketMetaData.StatusName,
            UserDisplayName = payload.TicketMetaData.StatusName,
            Index = 1,
            Default = false,
            Disabled = false,
            IsGlobal = false,
            CoreStatus = 42,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 4
        };

        var ticketSource = new TicketSource
        {
            Name = payload.TicketMetaData.SourceName,
            Default = true,
            DefaultSelfservice = true,
            DefaultSelfserviceLocation = true,
            Index = 1,
            Disabled = false,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Id = 5
        };

        var ticketDepartment = new TicketDepartment
        {
            Name = payload.TicketMetaData.AssignedDepartmentName,
            Disabled = false,
            Type = 1,
            TicketCategoryRequiredType = 0,
            ChangeCategoryRequiredType = 0,
            Id = 6
        };

        var ticket = new Ticket
        {
            RequestNumber = 42,
            EmailAddress = payload.User.EmailAddress,
            RequestTypeId = payload.TicketMetaData.RequestTypeId,
            Subject = payload.TicketMetaData.Subject,
            Description = payload.TicketMetaData.Description
        };

        _pureserviceUserService.GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress)).Returns(user);
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName)).ReturnsNull();
        _pureserviceTicketService.GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName)).Returns(ticketType);
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName)).ReturnsNull();
        _pureserviceTicketService.GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName)).Returns(ticketPriority);
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName)).ReturnsNull();
        _pureserviceTicketService.GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName)).Returns(ticketStatus);
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName)).ReturnsNull();
        _pureserviceTicketService.GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName)).Returns(ticketSource);
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName)).ReturnsNull();
        _pureserviceTicketService.GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName)).Returns(ticketDepartment);
        _pureserviceTicketService.CreateTicket(Arg.Any<TicketPayload>()).Returns(ticket);

        IActionResult? result = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            result = await _service.CreateTicket(request);
        });
        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.IsType<JsonResult>(result);
        
        var jsonResult = result as JsonResult;
        Assert.NotNull(jsonResult);
        Assert.NotNull(jsonResult.Value);
        
        var createdTicket = jsonResult.Value as Ticket;
        Assert.NotNull(createdTicket);
        Assert.Equal(ticket.RequestNumber, createdTicket.RequestNumber);
        Assert.Equal(ticket.EmailAddress, createdTicket.EmailAddress);
        Assert.Equal(ticket.RequestTypeId, createdTicket.RequestTypeId);
        Assert.Equal(ticket.Subject, createdTicket.Subject);
        Assert.Equal(ticket.Description, createdTicket.Description);
        
        await _pureserviceUserService.Received(1).GetUserByEmailAddress(Arg.Is(payload.User.EmailAddress));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(payload.TicketMetaData.TicketTypeName));
        await _pureserviceTicketService.Received(1).GetTicketTypeByName(Arg.Is(_ticketTypeFallbackName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(payload.TicketMetaData.PriorityName));
        await _pureserviceTicketService.Received(1).GetTicketPriorityByName(Arg.Is(_ticketPriorityFallbackName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(payload.TicketMetaData.StatusName));
        await _pureserviceTicketService.Received(1).GetTicketStatusByName(Arg.Is(_ticketStatusFallbackName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(payload.TicketMetaData.SourceName));
        await _pureserviceTicketService.Received(1).GetTicketSourceByName(Arg.Is(_ticketSourceFallbackName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(payload.TicketMetaData.AssignedDepartmentName));
        await _pureserviceTicketService.Received(1).GetTicketDepartmentByName(Arg.Is(_ticketAssignedDepartmentFallbackName));
        await _pureserviceTicketService.Received(1).CreateTicket(Arg.Any<TicketPayload>());
        
        await _pureservicePhoneNumberService.DidNotReceive().AddNewPhoneNumber(Arg.Any<string>(), Arg.Any<PhoneNumberType>());
        await _pureserviceEmailAddressService.DidNotReceive().AddNewEmailAddress(Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().CreateManualUser(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>());
        await _pureserviceUserService.DidNotReceive().UpdateBasicProperties(Arg.Any<int>(), Arg.Any<List<(string, (string?, int?, bool?))>>());
    }
    
    private static HttpRequest CreateHttpRequest(string? body)
    {
        var context = new DefaultHttpContext();
        var request = context.Request;

        request.Method = HttpMethods.Post;
        request.ContentType = "application/json";

        var json = body ?? string.Empty;
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        request.Body.Position = 0;

        return request;
    }

    private static CreateTicketPayload ValidPayload() => new()
    {
            OriginatingReference = "Test1234",
            TicketMetaData = new CreateTicketMetaData
            {
                AssignedDepartmentName = "IT Support",
                Description = "Test Description",
                PriorityName = "High",
                RequestTypeId = 1,
                SourceName = "Email",
                StatusName = "Open",
                Subject = "Test Subject",
                TicketTypeName = "Test Ticket Type"
            },
            User = new CreateTicketUser
            {
                EmailAddress = "foo@bar.biz",
                Name = "Foo Bar",
                PhoneNumber = "12345678"
            }
        };
}