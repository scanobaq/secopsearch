using Secop.Application.UseCases.Procesos.SincronizarProcesos;
using Secop.Infrastructure;
using Secop.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// MediatR: escanea todos los handlers del assembly de Application
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SincronizarProcesosHandler).Assembly));

// Infrastructure: DbContext, repositorios, servicios externos
builder.Services.AddInfrastructure(builder.Configuration);

// Workers periódicos
builder.Services.AddHostedService<RupAlertaWorker>();

var host = builder.Build();
host.Run();
