using DevWeave.Application;
using DevWeave.Infrastructure;
using DevWeave.Workers;
using DevWeave.Workers.Retention;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<QuarantineCleanupWorker>();

var host = builder.Build();
host.Run();
