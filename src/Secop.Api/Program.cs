using Secop.Application.UseCases.Procesos.SincronizarProcesos;
using Secop.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "SecopSearch API",
        Version     = "v1",
        Description = "API de administración y diagnóstico del sistema SecopSearch. " +
                      "Permite gestionar proveedores, disparar sincronizaciones con SECOP II " +
                      "y consultar procesos de licitación compatibles con el grupo empresarial."
    });
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// MediatR: escanea todos los handlers del assembly de Application
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SincronizarProcesosHandler).Assembly));

// Infrastructure: DbContext, repositorios, servicios externos
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
