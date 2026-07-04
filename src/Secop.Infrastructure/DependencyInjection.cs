using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Secop.Application.Interfaces;
using Secop.Infrastructure.ExternalServices;
using Secop.Infrastructure.ExternalServices.Telegram;
using Secop.Infrastructure.Filtros;
using Secop.Infrastructure.Persistence;
using Secop.Infrastructure.Persistence.Repositories;
using Secop.Infrastructure.Scoring;
using Telegram.Bot;

namespace Secop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Base de datos ────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.UseVector()));

        // ── Repositorios (Scoped — ciclo de vida por request) ───────────────
        services.AddScoped<IProcesoRepository, ProcesoRepository>();
        services.AddScoped<IProveedorRepository, ProveedorRepository>();
        services.AddScoped<IPuntajeRepository, PuntajeRepository>();
        services.AddScoped<IDecisionRepository, DecisionRepository>();

        // ── Scoring (Singleton — pura lógica sin estado) ────────────────────
        services.AddSingleton<IScoringService, ScoringService>();

        // ── Filtros de proceso (toggle publicitario, SPEC-03) ───────────────
        services.AddSingleton<IFiltrosProcesoPolicy, FiltrosProcesoPolicy>();

        // ── OpenAI Embeddings ────────────────────────────────────────────────
        services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>(client =>
        {
            client.DefaultRequestHeaders.Add(
                "Authorization",
                $"Bearer {configuration["OpenAI:ApiKey"]}");
        });

        // ── SECOP II API Client ──────────────────────────────────────────────
        services.AddHttpClient<ISecopApiClient, SecopApiClient>(client =>
        {
            client.DefaultRequestHeaders.Add(
                "X-App-Token",
                configuration["Secop:AppToken"]);
        });

        // ── Claude (Anthropic API) ───────────────────────────────────────────
        services.AddHttpClient<IClaudeService, ClaudeService>(client =>
        {
            client.DefaultRequestHeaders.Add("x-api-key", configuration["Anthropic:ApiKey"]);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        });

        // ── Telegram Bot ─────────────────────────────────────────────────────
        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(configuration["Telegram:BotToken"]!));

        services.AddSingleton<IAlertaService, TelegramAlertaService>();

        return services;
    }
}
