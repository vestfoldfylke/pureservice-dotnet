using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using pureservice_dotnet.Models;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet_tests;

public class PureserviceTicketServiceTests
{
    private readonly PureserviceTicketService _service;
    private readonly IPureserviceCaller _pureserviceCaller;
    
    public PureserviceTicketServiceTests()
    {
        _pureserviceCaller = Substitute.For<IPureserviceCaller>();
        _service = new PureserviceTicketService(Substitute.For<ILogger<PureserviceTicketService>>(),
            Substitute.For<IMetricsService>(), _pureserviceCaller);
    }

    [Theory]
    [InlineData("RequestType")]
    [InlineData("User")]
    [InlineData("TicketType")]
    [InlineData("Priority")]
    [InlineData("Status")]
    [InlineData("Source")]
    [InlineData("AssignedDepartment")]
    public async Task CreateTicket_Should_Throw_When_Required_Link_Is_Missing(string linkName)
    {
        var ticketPayload = new TicketPayload
        {
            Description = "This is a test ticket",
            OriginatingReference = "TestRef123",
            Subject = "Test Ticket",
            Links = new Links
            {
                RequestType = linkName == "RequestType" ? null : new Link(42, ""),
                User = linkName == "User" ? null : new Link(43, ""),
                TicketType = linkName == "TicketType" ? null : new Link(44, ""),
                Priority = linkName == "Priority" ? null : new Link(45, ""),
                Status = linkName == "Status" ? null : new Link(46, ""),
                Source = linkName == "Source" ? null : new Link(47, ""),
                AssignedDepartment = linkName == "AssignedDepartment" ? null : new Link(48, "")
            }
        };
        
        var exception = await Record.ExceptionAsync(async () => await _service.CreateTicket(ticketPayload));
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains($"{linkName} link is required", exception.Message);

        await _pureserviceCaller.DidNotReceive().PostAsync<Ticket>(Arg.Any<string>(), Arg.Any<Dictionary<string, object?>>());
    }
}