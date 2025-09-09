using System;
using Microsoft.Extensions.Logging;
using NSubstitute;
using pureservice_dotnet.Models;
using pureservice_dotnet.Models.Enums;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Metrics.Services;

namespace pureservice_dotnet_tests;

public class PureservicePhoneNumberServiceTests
{
    private readonly PureservicePhoneNumberService _service;
    
    public PureservicePhoneNumberServiceTests()
    {
        _service = new PureservicePhoneNumberService(Substitute.For<ILogger<PureservicePhoneNumberService>>(),
            Substitute.For<IMetricsService>(), Substitute.For<IPureserviceCaller>());
    }
    
    [Theory]
    [InlineData(null, null)]
    [InlineData("0118 999 881 999 119 725 3", null)]
    [InlineData(null, "0118 999 881 999 119 725 3")]
    [InlineData("0118 999 881 999 119 725 3", "0118 999 881 999 119 725 3")]
    public void NeedsPhoneNumberUpdate(string? phoneNumber, string? entraPhoneNumber)
    {
        var phoneNumberObj = phoneNumber is not null
            ? new PhoneNumber(phoneNumber, phoneNumber, PhoneNumberType.Mobile, null, 42, DateTime.Now, null, 41, null)
            : null;
        
        var result = _service.NeedsPhoneNumberUpdate(phoneNumberObj, entraPhoneNumber);
        
        if ((phoneNumber is null && entraPhoneNumber is null) || (phoneNumber is not null && entraPhoneNumber is not null))
        {
            Assert.False(result.Update);
            Assert.Equal(entraPhoneNumber, result.PhoneNumber);
            return;
        }
        
        Assert.True(result.Update);
        Assert.Equal(entraPhoneNumber, result.PhoneNumber);
    }
}