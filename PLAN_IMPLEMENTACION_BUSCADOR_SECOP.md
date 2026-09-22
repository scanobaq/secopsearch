# Plan de Implementación — Buscador de Procesos SECOP II
## Bot de Telegram + .NET Core + Clean Architecture + Supabase

---

## Contexto del proyecto

Sistema inteligente que monitorea SECOP II continuamente, cruza los procesos de contratación con el perfil de un grupo empresarial colombiano y notifica a través de Telegram qué procesos vale la pena atender y qué hacer con cada uno.

**El cliente:** Grupo empresarial con 4 unidades de negocio y $14.000 millones en capacidad financiera consolidada:
- Empresa de Logística y Eventos (RUP $7.000M)
- Empresa de Publicidad y Tecnología (RUP $4.000M)
- Empresa de Asesorías Jurídicas y Tributarias (RUP $800M)
- ESAL + líneas complementarias

**Frontend:** Bot de Telegram — sin interfaz web por ahora.

**Principio rector:** Construir lo que funciona hoy. No optimizar lo que no se ha medido.

---

## Stack tecnológico

| Componente | Tecnología |
|---|---|
| Lenguaje | C# .NET 8 |
| Arquitectura | Clean Architecture |
| Base de datos | PostgreSQL + pgvector (Supabase) |
| ORM | Entity Framework Core + Npgsql |
| Embeddings | OpenAI text-embedding-3-small |
| Análisis de pliegos PDF | Anthropic Claude API |
| Fuente de datos | SECOP II API (datos.gov.co — Socrata SODA) |
| Frontend | Bot de Telegram (Telegram.Bot SDK) |
| Worker periódico | .NET BackgroundService |
| Servidor | Railway o Render |
| Mensajería interna | MediatR |

---

## Credenciales y cuentas requeridas

Antes de escribir código, tener listas estas cuentas y guardar las keys en un archivo `.env` local:

```env
# Telegram
TELEGRAM_BOT_TOKEN=obtenido con @BotFather

# Supabase
SUPABASE_CONNECTION_STRING=postgresql://...
SUPABASE_URL=https://xxx.supabase.co
SUPABASE_KEY=eyJ...

# OpenAI (embeddings)
OPENAI_API_KEY=sk-...

# Anthropic (lectura de pliegos)
ANTHROPIC_API_KEY=sk-ant-...

# SECOP II
SECOP_APP_TOKEN=token de datos.gov.co

# SendGrid (alertas email — opcional fase 2)
SENDGRID_API_KEY=SG....
```

**Cómo obtener cada uno:**
- **Telegram Bot:** Abrir Telegram → buscar @BotFather → /newbot → seguir instrucciones → copiar el token
- **Supabase:** supabase.com → New Project → Settings → Database → Connection string
- **OpenAI:** platform.openai.com → API Keys → Create new secret key
- **Anthropic:** console.anthropic.com → API Keys → Create Key
- **datos.gov.co:** www.datos.gov.co → perfil → Developer Settings → Create New App Token

---

## Estructura del proyecto (Clean Architecture)

```
Secop.Buscador/
│
├── src/
│   ├── Secop.Domain/
│   ├── Secop.Application/
│   ├── Secop.Infrastructure/
│   ├── Secop.Api/
│   └── Secop.Worker/
│
├── tests/
│   ├── Secop.Domain.Tests/
│   ├── Secop.Application.Tests/
│   └── Secop.Infrastructure.Tests/
│
├── .env
├── docker-compose.yml
└── README.md
```

### Secop.Domain (sin dependencias externas)

```
Secop.Domain/
├── Entities/
│   ├── Proceso.cs
│   ├── Proveedor.cs
│   ├── Empresa.cs
│   ├── Puntaje.cs
│   ├── Decision.cs
│   └── Alerta.cs
├── Enums/
│   ├── EtiquetaProceso.cs         // Proponer | Analizar | Descartar
│   ├── ModalidadContrato.cs       // LicitacionPublica | Abreviada | ConcursoMeritos | etc.
│   └── EstadoProceso.cs           // Activo | Desierto | Adjudicado | Cancelado
└── ValueObjects/
    ├── CodigoUnspsc.cs
    └── CapacidadFinanciera.cs
```

**Entidades clave:**

```csharp
// Proceso.cs
public class Proceso
{
    public string Id { get; private set; }
    public string Titulo { get; private set; }
    public string Objeto { get; private set; }
    public decimal Presupuesto { get; private set; }
    public DateTime FechaCierre { get; private set; }
    public DateTime FechaPublicacion { get; private set; }
    public ModalidadContrato Modalidad { get; private set; }
    public EstadoProceso Estado { get; private set; }
    public string NombreEntidad { get; private set; }
    public string NitEntidad { get; private set; }
    public string DepartamentoEntidad { get; private set; }
    public string UrlProceso { get; private set; }
    public float[] Embedding { get; private set; }

    public bool EstaVigente() => FechaCierre > DateTime.UtcNow;
    public int DiasHabilesRestantes() { /* lógica */ }
    public bool EsDesierto() => Estado == EstadoProceso.Desierto;
}

// Proveedor.cs
public class Proveedor
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; }
    public string Nit { get; private set; }
    public DateTime RupVigencia { get; private set; }
    public decimal CapacidadFinanciera { get; private set; }
    public List<string> CodigosUnspsc { get; private set; }
    public List<string> ExperienciaDescripcion { get; private set; }
    public float[] Embedding { get; private set; }
    public bool RupVigente() => RupVigencia > DateTime.UtcNow;
}

// Puntaje.cs
public class Puntaje
{
    public string ProcesoId { get; private set; }
    public Guid ProveedorId { get; private set; }
    public float PuntajeTotal { get; private set; }       // 0 a 100
    public float PuntajeSimilitud { get; private set; }   // 35 puntos max
    public float PuntajeRequisitos { get; private set; }  // 25 puntos max
    public float PuntajeTiempo { get; private set; }      // 20 puntos max
    public float PuntajeCompetencia { get; private set; } // 12 puntos max
    public float PuntajeEntidad { get; private set; }     // 8 puntos max
    public EtiquetaProceso Etiqueta { get; private set; } // Proponer|Analizar|Descartar
    public List<string> Advertencias { get; private set; } // señales de proceso dirigido
    public DateTime CalculadoEn { get; private set; }
}
```

---

### Secop.Application (casos de uso e interfaces)

```
Secop.Application/
├── Interfaces/
│   ├── IProcesoRepository.cs
│   ├── IProveedorRepository.cs
│   ├── IEmbeddingService.cs
│   ├── IScoringService.cs
│   ├── IAlertaService.cs
│   ├── ISecopApiClient.cs
│   └── IClaudeService.cs
├── UseCases/
│   ├── Procesos/
│   │   ├── SincronizarProcesos/
│   │   │   ├── SincronizarProcesosCommand.cs
│   │   │   └── SincronizarProcesosHandler.cs
│   │   ├── BuscarProcesosCompatibles/
│   │   │   ├── BuscarProcesosQuery.cs
│   │   │   └── BuscarProcesosHandler.cs
│   │   └── ObtenerDetalleProceso/
│   │       ├── ObtenerDetalleQuery.cs
│   │       └── ObtenerDetalleHandler.cs
│   ├── Puntajes/
│   │   ├── CalcularPuntaje/
│   │   │   ├── CalcularPuntajeCommand.cs
│   │   │   └── CalcularPuntajeHandler.cs
│   │   └── RecalcularPuntajes/
│   │       ├── RecalcularPuntajesCommand.cs
│   │       └── RecalcularPuntajesHandler.cs
│   ├── Decisiones/
│   │   ├── RegistrarDecision/
│   │   │   ├── RegistrarDecisionCommand.cs
│   │   │   └── RegistrarDecisionHandler.cs
│   └── Alertas/
│       └── EnviarAlertasProvider/
│           ├── EnviarAlertasCommand.cs
│           └── EnviarAlertasHandler.cs
└── DTOs/
    ├── ProcesoDto.cs
    ├── PuntajeDto.cs
    └── DecisionDto.cs
```

**Interfaces clave:**

```csharp
// IEmbeddingService.cs
public interface IEmbeddingService
{
    Task<float[]> GenerarEmbeddingAsync(string texto);
    Task<float> CalcularSimilitudAsync(float[] v1, float[] v2);
}

// ISecopApiClient.cs
public interface ISecopApiClient
{
    Task<List<ProcesoDto>> ObtenerProcesosRecientesAsync(DateTime desde);
    Task<List<ProcesoDto>> ObtenerProcesosDesiertoAsync();
    Task<List<ProcesoDto>> ObtenerProcesosPorModalidadAsync(string modalidad);
    Task<List<ProcesoDto>> ObtenerProcesosConPaginacionAsync(int limit, int offset);
}

// IScoringService.cs
public interface IScoringService
{
    Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud);
}

// IAlertaService.cs
public interface IAlertaService
{
    Task EnviarAlertaProcesoAsync(long telegramChatId, Puntaje puntaje, Proceso proceso);
    Task EnviarAlertaRupPorVencerAsync(long telegramChatId, Proveedor proveedor);
    Task EnviarAlertaDesiertoAsync(long telegramChatId, Proceso proceso);
}
```

---

### Secop.Infrastructure (implementaciones)

```
Secop.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── ProcesoConfiguration.cs
│   │   ├── ProveedorConfiguration.cs
│   │   └── PuntajeConfiguration.cs
│   ├── Repositories/
│   │   ├── ProcesoRepository.cs
│   │   └── ProveedorRepository.cs
│   └── Migrations/
├── ExternalServices/
│   ├── OpenAiEmbeddingService.cs      // implementa IEmbeddingService
│   ├── SecopApiClient.cs              // implementa ISecopApiClient
│   ├── ClaudeService.cs               // implementa IClaudeService
│   └── TelegramAlertaService.cs       // implementa IAlertaService
├── Scoring/
│   └── ScoringService.cs              // implementa IScoringService
└── DependencyInjection.cs             // registro de todos los servicios
```

**AppDbContext con pgvector:**

```csharp
// AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<Proceso> Procesos { get; set; }
    public DbSet<Proveedor> Proveedores { get; set; }
    public DbSet<Puntaje> Puntajes { get; set; }
    public DbSet<Decision> Decisiones { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Habilitar pgvector
        modelBuilder.HasPostgresExtension("vector");

        // Mapear columna embedding como vector
        modelBuilder.Entity<Proceso>()
            .Property(p => p.Embedding)
            .HasColumnType("vector(1536)");

        modelBuilder.Entity<Proveedor>()
            .Property(p => p.Embedding)
            .HasColumnType("vector(1536)");
    }
}
```

**SecopApiClient — consumo de datos.gov.co:**

```csharp
// SecopApiClient.cs
public class SecopApiClient : ISecopApiClient
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://www.datos.gov.co/resource";
    private const string DatasetProcesos = "p6dx-8zbt";

    public async Task<List<ProcesoDto>> ObtenerProcesosRecientesAsync(DateTime desde)
    {
        var fechaFiltro = desde.ToString("yyyy-MM-ddTHH:mm:ss");
        var url = $"{BaseUrl}/{DatasetProcesos}.json" +
                  $"?$where=fecha_de_ultima_publicaci > '{fechaFiltro}'" +
                  $"&$limit=1000" +
                  $"&$order=fecha_de_ultima_publicaci DESC";

        return await _http.GetFromJsonAsync<List<ProcesoDto>>(url)
               ?? new List<ProcesoDto>();
    }

    public async Task<List<ProcesoDto>> ObtenerProcesosConPaginacionAsync(int limit, int offset)
    {
        var url = $"{BaseUrl}/{DatasetProcesos}.json" +
                  $"?$limit={limit}&$offset={offset}" +
                  $"&$order=fecha_de_ultima_publicaci DESC";

        return await _http.GetFromJsonAsync<List<ProcesoDto>>(url)
               ?? new List<ProcesoDto>();
    }
}
```

**OpenAiEmbeddingService:**

```csharp
// OpenAiEmbeddingService.cs
public class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;

    public async Task<float[]> GenerarEmbeddingAsync(string texto)
    {
        var body = new { input = texto, model = "text-embedding-3-small" };
        var response = await _http.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings", body);
        var result = await response.Content
            .ReadFromJsonAsync<OpenAiEmbeddingResponse>();
        return result!.Data[0].Embedding;
    }

    public Task<float> CalcularSimilitudAsync(float[] v1, float[] v2)
    {
        // Similitud coseno
        float dot = 0, mag1 = 0, mag2 = 0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot  += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        return Task.FromResult(dot / (MathF.Sqrt(mag1) * MathF.Sqrt(mag2)));
    }
}
```

**ScoringService — lógica del puntaje 0–100:**

```csharp
// ScoringService.cs
public class ScoringService : IScoringService
{
    public Task<Puntaje> CalcularAsync(Proveedor proveedor, Proceso proceso, float similitud)
    {
        var advertencias = new List<string>();

        // Componente 1: Similitud semántica (35 puntos)
        float pSimilitud = similitud * 35f;

        // Componente 2: Cumplimiento de requisitos (25 puntos)
        float pRequisitos = 0;
        if (proveedor.RupVigente()) pRequisitos += 15;
        if (proveedor.CapacidadFinanciera >= proceso.Presupuesto * 0.15m) pRequisitos += 10;

        // Si RUP vencido → puntaje total = 0 (inhabilitante automático)
        if (!proveedor.RupVigente())
        {
            advertencias.Add("RUP vencido — propuesta sería rechazada automáticamente");
            return Task.FromResult(Puntaje.Inhabilitado(proceso.Id, proveedor.Id, advertencias));
        }

        // Componente 3: Tiempo disponible (20 puntos)
        var dias = proceso.DiasHabilesRestantes();
        float pTiempo = dias switch
        {
            >= 15 => 20,
            >= 10 => 15,
            >= 5  => 8,
            _     => 2
        };

        // Componente 4: Competencia estimada (12 puntos) — simplificado v1
        float pCompetencia = proceso.EsDesierto() ? 12 : 6;

        // Componente 5: Historial de la entidad (8 puntos) — simplificado v1
        float pEntidad = 4; // valor neutro hasta tener historial real

        // Señales de proceso dirigido
        if (dias <= 3)
            advertencias.Add("Plazo muy corto — posible proceso dirigido");

        float total = pSimilitud + pRequisitos + pTiempo + pCompetencia + pEntidad;

        var etiqueta = total switch
        {
            >= 70 => EtiquetaProceso.Proponer,
            >= 40 => EtiquetaProceso.Analizar,
            _     => EtiquetaProceso.Descartar
        };

        return Task.FromResult(new Puntaje(
            proceso.Id, proveedor.Id, total,
            pSimilitud, pRequisitos, pTiempo, pCompetencia, pEntidad,
            etiqueta, advertencias));
    }
}
```

---

### Secop.Worker (alertas RUP periódicas)

```
Secop.Worker/
├── Workers/
│   └── RupAlertaWorker.cs        // corre una vez al día
└── Program.cs
```

---

### Secop.Api (endpoints REST para Telegram y consultas)

```
Secop.Api/
├── Controllers/
│   ├── ProcesosController.cs
│   └── ProveedoresController.cs
├── Program.cs
└── appsettings.json
```

---

## Bot de Telegram — comandos y flujo

### Comandos que debe responder el bot

```
/start          → Mensaje de bienvenida y registro del chat
/procesos       → Lista los procesos con etiqueta PROPONER del día
/desiertos      → Lista procesos desiertos detectados hoy
/alertas        → Configurar qué alertas quiere recibir
/empresa [n]    → Ver procesos filtrados para una empresa específica
/proceso [id]   → Ver detalle de un proceso específico con acciones
/estado         → Estado del sistema (último sync, RUP vigencias)
/ayuda          → Lista de comandos disponibles
```

### Estructura del TelegramBot handler

```
Secop.Infrastructure/
└── ExternalServices/
    └── Telegram/
        ├── TelegramBotService.cs         // inicializa y escucha updates
        ├── Handlers/
        │   ├── StartCommandHandler.cs
        │   ├── ProcesosCommandHandler.cs
        │   ├── DesiertoCommandHandler.cs
        │   └── ProcesoDetalleHandler.cs
        └── Formatters/
            └── ProcesoMessageFormatter.cs  // formatea el mensaje con emojis
```

### Formato del mensaje de alerta en Telegram

```
🟢 PROPONER — Puntaje: 84/100

📋 Suministro de equipos tecnológicos para
   oficinas administrativas

🏛 Entidad: MINISTERIO DE EDUCACIÓN NACIONAL
💰 Presupuesto: $180.000.000
📅 Cierre: 15 de abril (8 días hábiles)
🏷 Modalidad: Selección Abreviada
🏢 Empresa recomendada: Publicidad y Tecnología

⚡ Acción inmediata:
• Leer el pliego hoy
• Verificar UNSPSC en el RUP
• Asignar responsable antes del 13 de abril

🔗 Ver proceso en SECOP II

[📄 Ver pliego] [✅ Voy a proponer] [❌ Descartar]
```

### Implementación del bot

```csharp
// TelegramBotService.cs
public class TelegramBotService : IAlertaService
{
    private readonly ITelegramBotClient _bot;
    private readonly IMediator _mediator;

    // Enviar alerta de proceso
    public async Task EnviarAlertaProcesoAsync(
        long chatId, Puntaje puntaje, Proceso proceso)
    {
        var emoji = puntaje.Etiqueta switch
        {
            EtiquetaProceso.Proponer  => "🟢",
            EtiquetaProceso.Analizar  => "🟡",
            EtiquetaProceso.Descartar => "🔴",
            _ => "⚪"
        };

        var mensaje = ProcesoMessageFormatter.Formatear(emoji, puntaje, proceso);

        var teclado = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📄 Ver pliego", $"pliego_{proceso.Id}"),
                InlineKeyboardButton.WithCallbackData("✅ Voy a proponer", $"proponer_{proceso.Id}"),
                InlineKeyboardButton.WithCallbackData("❌ Descartar", $"descartar_{proceso.Id}")
            }
        });

        await _bot.SendTextMessageAsync(
            chatId, mensaje,
            parseMode: ParseMode.Markdown,
            replyMarkup: teclado);
    }

    // Alerta RUP por vencer
    public async Task EnviarAlertaRupPorVencerAsync(long chatId, Proveedor proveedor)
    {
        var dias = (proveedor.RupVigencia - DateTime.Today).Days;
        var mensaje = $"⚠️ *ALERTA RUP*\n\n" +
                      $"El RUP de *{proveedor.Nombre}* vence en *{dias} días*.\n" +
                      $"Renuévalo antes del {proveedor.RupVigencia:dd/MM/yyyy} " +
                      $"para no perder procesos activos.";

        await _bot.SendTextMessageAsync(chatId, mensaje, parseMode: ParseMode.Markdown);
    }
}
```

---

## Base de datos — esquema SQL

```sql
-- Habilitar extensión vectorial
CREATE EXTENSION IF NOT EXISTS vector;

-- Tabla de proveedores (empresas del grupo)
CREATE TABLE proveedores (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre                TEXT NOT NULL,
    nit                   TEXT NOT NULL UNIQUE,
    rup_vigencia          DATE NOT NULL,
    capacidad_financiera  DECIMAL(18,2) NOT NULL,
    codigos_unspsc        TEXT[] NOT NULL,
    experiencia           TEXT[],
    embedding             vector(1536),
    telegram_chat_id      BIGINT,
    creado_en             TIMESTAMP DEFAULT NOW(),
    actualizado_en        TIMESTAMP DEFAULT NOW()
);

-- Tabla de procesos SECOP II
CREATE TABLE procesos (
    id                    TEXT PRIMARY KEY,
    titulo                TEXT NOT NULL,
    objeto                TEXT,
    presupuesto           DECIMAL(18,2),
    fecha_cierre          TIMESTAMP,
    fecha_publicacion     TIMESTAMP,
    modalidad             TEXT,
    estado                TEXT,
    nombre_entidad        TEXT,
    nit_entidad           TEXT,
    departamento_entidad  TEXT,
    url_proceso           TEXT,
    embedding             vector(1536),
    sincronizado_en       TIMESTAMP DEFAULT NOW()
);

-- Índice para búsqueda vectorial eficiente
CREATE INDEX ON procesos USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- Tabla de puntajes calculados
CREATE TABLE puntajes (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    proceso_id            TEXT REFERENCES procesos(id),
    proveedor_id          UUID REFERENCES proveedores(id),
    puntaje_total         FLOAT NOT NULL,
    puntaje_similitud     FLOAT,
    puntaje_requisitos    FLOAT,
    puntaje_tiempo        FLOAT,
    puntaje_competencia   FLOAT,
    puntaje_entidad       FLOAT,
    etiqueta              TEXT NOT NULL,  -- Proponer | Analizar | Descartar
    advertencias          TEXT[],
    calculado_en          TIMESTAMP DEFAULT NOW(),
    UNIQUE(proceso_id, proveedor_id)
);

-- Tabla de decisiones del usuario (para aprendizaje)
CREATE TABLE decisiones (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    proceso_id            TEXT REFERENCES procesos(id),
    proveedor_id          UUID REFERENCES proveedores(id),
    accion                TEXT NOT NULL, -- propuso | descarto | gano | perdio
    razon_descarte        TEXT,
    resultado             TEXT,
    registrado_en         TIMESTAMP DEFAULT NOW()
);

-- Tabla de alertas enviadas
CREATE TABLE alertas (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    proceso_id            TEXT REFERENCES procesos(id),
    proveedor_id          UUID REFERENCES proveedores(id),
    tipo                  TEXT NOT NULL, -- proceso | rup | desierto | adenda
    enviada_en            TIMESTAMP DEFAULT NOW(),
    telegram_message_id   INTEGER
);
```

---

## Endpoints de la API SECOP II a consumir

| Dataset | Endpoint | Para qué |
|---|---|---|
| Procesos | `https://www.datos.gov.co/resource/p6dx-8zbt.json` | Core del sistema |
| Contratos | `https://www.datos.gov.co/resource/jbjy-vk9h.json` | Contratos por vencer |
| AMP/TVEC | `https://www.datos.gov.co/resource/rgxm-mmea.json` | Órdenes marco |
| Proveedores | `https://www.datos.gov.co/resource/qmzu-gj57.json` | Validar competencia |
| SECOP Integrado | `https://www.datos.gov.co/resource/rpmr-utcd.json` | Histórico completo |

**Header requerido en todas las llamadas:**
```
X-App-Token: {SECOP_APP_TOKEN}
```

---

## Fases de implementación

### Fase 1 — Fundamentos (semana 1–2)
**Objetivo:** El proyecto compila, conecta a la BD y lee datos de SECOP II.

- [ ] Crear solución .NET con los 5 proyectos
- [ ] Instalar paquetes NuGet:
  - `Telegram.Bot` (bot de Telegram)
  - `MediatR` (mediator pattern)
  - `Npgsql.EntityFrameworkCore.PostgreSQL` (EF Core + pgvector)
  - `Pgvector.EntityFrameworkCore` (soporte vectorial)
  - `Microsoft.Extensions.Http` (HttpClient factory)
- [ ] Configurar AppDbContext con pgvector
- [ ] Crear migraciones y tablas en Supabase
- [ ] Implementar SecopApiClient básico
- [ ] Probar conexión a datos.gov.co y ver respuesta JSON
- [ ] Mapear DTO de respuesta SECOP → entidad Proceso

### Fase 2 — Motor de embeddings y scoring (semana 2–3)
**Objetivo:** El sistema puede comparar un perfil con procesos y dar un puntaje.

- [ ] Implementar OpenAiEmbeddingService
- [ ] Registrar embeddings de los 4 proveedores del grupo en BD
- [ ] Implementar ScoringService con los 5 componentes del puntaje
- [ ] Implementar BuscarProcesosCompatiblesHandler
- [ ] Prueba unitaria: dado un proveedor y un proceso, el puntaje es correcto
- [ ] Prueba de integración: dado el perfil de Logística, encuentra procesos de eventos

### Fase 3 — Alertas RUP (semana 3)
**Objetivo:** La sincronización de procesos se activa manualmente y las alertas RUP se ejecutan periódicamente.

- [ ] RupAlertaWorker: alerta 30 días antes del vencimiento del RUP
- [ ] Logs de cada ejecución en consola y en BD

### Fase 4 — Bot de Telegram (semana 3–4)
**Objetivo:** El cliente recibe alertas y puede consultar procesos desde Telegram.

- [ ] Inicializar TelegramBotClient con webhook o polling
- [ ] Implementar /start y registro del chatId
- [ ] Implementar envío de alerta cuando puntaje >= 70
- [ ] Implementar envío de alerta de procesos desiertos
- [ ] Implementar botones inline: Proponer / Analizar / Descartar
- [ ] Guardar decisión del usuario en tabla decisiones
- [ ] Implementar /procesos — lista del día
- [ ] Implementar /estado — último sync y alertas RUP
- [ ] Implementar /empresa [nombre] — filtrar por empresa del grupo

### Fase 5 — Lectura de pliegos con Claude (semana 4–5)
**Objetivo:** Al tocar "Ver pliego", el bot resume automáticamente los requisitos clave.

- [ ] Implementar ClaudeService con Anthropic API
- [ ] Descargar PDF del pliego desde la URL del proceso
- [ ] Enviar PDF a Claude y extraer:
  - Requisitos habilitantes (RUP, experiencia, financieros)
  - Criterios de evaluación y pesos
  - Documentos obligatorios
  - Fechas clave
- [ ] Formatear resumen y enviarlo al chat de Telegram

### Fase 6 — Despliegue (semana 5)
**Objetivo:** El sistema corre en producción sin intervención manual.

- [ ] Crear Dockerfile para la aplicación
- [ ] Configurar variables de entorno en Railway o Render
- [ ] Configurar Supabase en modo producción
- [ ] Configurar Telegram webhook con URL del servidor
- [ ] Prueba end-to-end en producción
- [ ] Monitorear logs las primeras 48 horas

---

## Paquetes NuGet — lista completa

```xml
<!-- Secop.Domain — sin dependencias -->

<!-- Secop.Application -->
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />

<!-- Secop.Infrastructure -->
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.*" />
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" />
<PackageReference Include="Telegram.Bot" Version="19.*" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />

<!-- Secop.Api -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.*" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />

<!-- Secop.Worker -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.*" />

<!-- Tests -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
```

---

## Variables de entorno — appsettings structure

```json
{
  "Telegram": {
    "BotToken": ""
  },
  "OpenAI": {
    "ApiKey": "",
    "EmbeddingModel": "text-embedding-3-small"
  },
  "Anthropic": {
    "ApiKey": "",
    "Model": "claude-sonnet-4-20250514"
  },
  "Secop": {
    "AppToken": "",
    "BaseUrl": "https://www.datos.gov.co/resource",
    "DatasetProcesos": "p6dx-8zbt",
    "DatasetContratos": "jbjy-vk9h",
    "DatasetAmp": "rgxm-mmea",
    "IntervalHoras": 1
  },
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Scoring": {
    "UmbralProponer": 70,
    "UmbralAnalizar": 40,
    "PesoSimilitud": 35,
    "PesoRequisitos": 25,
    "PesoTiempo": 20,
    "PesoCompetencia": 12,
    "PesoEntidad": 8
  }
}
```

---

## Notas importantes para Claude Code

1. **Empezar siempre por Domain** — no tiene dependencias externas, es lo más fácil de construir y testear.

2. **Nunca inyectar infraestructura en Domain o Application** — si una clase de Application necesita llamar a OpenAI, debe hacerlo a través de la interfaz `IEmbeddingService`, nunca instanciando `OpenAiEmbeddingService` directamente.

3. **El puntaje de requisitos es inhabilitante** — si el RUP está vencido, el puntaje total debe ser 0 sin importar los otros componentes. Esta regla de negocio va en el Domain.

4. **Telegram polling vs webhook** — para desarrollo local usar polling (más simple). Para producción en Railway usar webhook con la URL pública del servidor.

5. **Los embeddings se calculan una sola vez** — al registrar un proveedor o al ingestar un proceso nuevo. No recalcular en cada búsqueda.

6. **La similitud mínima global para entrar a evaluación es 0.40 inclusivo** — si la similitud es menor, el proceso se descarta antes de persistir la evaluación.

7. **El campo `fecha_de_ultima_publicaci`** en la API de SECOP tiene ese nombre exacto con la `i` al final sin acento — bug conocido del dataset. Usarlo tal cual.

8. **Supabase free tier** tiene límite de conexiones concurrentes — usar connection pooling con `Pooling=true` en el connection string.

---

## Recursos de referencia

- API SECOP II Procesos: https://www.datos.gov.co/resource/p6dx-8zbt.json
- Documentación Socrata SODA: https://dev.socrata.com/docs/queries/
- Telegram Bot API: https://core.telegram.org/bots/api
- Telegram.Bot SDK .NET: https://github.com/TelegramBots/Telegram.Bot
- pgvector .NET: https://github.com/pgvector/pgvector-dotnet
- OpenAI Embeddings: https://platform.openai.com/docs/guides/embeddings
- Anthropic API: https://docs.anthropic.com/en/api/getting-started
- Supabase .NET: https://supabase.com/docs/reference/csharp/introduction
- Colección Postman SECOP II: archivo `SECOP_II_Buscador.postman_collection.json`
