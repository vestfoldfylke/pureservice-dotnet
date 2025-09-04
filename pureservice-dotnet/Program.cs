using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using pureservice_dotnet.Services;
using Vestfold.Extensions.Authentication;
using Vestfold.Extensions.Logging;
using Vestfold.Extensions.Metrics;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddVestfoldAuthentication();
builder.Services.AddVestfoldMetrics();
builder.Logging.AddVestfoldLogging();

builder.Services.AddSingleton<IPureserviceCaller, PureserviceCaller>();
builder.Services.AddSingleton<IPureservicePhoneNumberService, PureservicePhoneNumberService>();
builder.Services.AddSingleton<IPureserviceUserService, PureserviceUserService>();
builder.Services.AddSingleton<IFintService, FintService>();
builder.Services.AddSingleton<IGraphService, GraphService>();

// Configure the service container to collect Prometheus metrics from all registered HttpClients
builder.Services.UseHttpClientMetrics();

builder.Build().Run();