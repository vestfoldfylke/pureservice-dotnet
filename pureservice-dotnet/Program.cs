using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Vestfold.Extensions.Logging;
using Vestfold.Extensions.Metrics;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddVestfoldMetrics();
builder.Logging.AddVestfoldLogging();

// Configure the service container to collect Prometheus metrics from all registered HttpClients
builder.Services.UseHttpClientMetrics();

builder.Build().Run();