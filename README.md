# job-service-c-sharp

> Scheduled job runner skeleton for C# / .NET 10. Connects to `backend-c-sharp`
> to consume SQL Server, Redis, and RabbitMQ.

A clean, idiomatic boilerplate for running cron-scheduled jobs in C# — no HTTP
layer, no auth, no audit, no PDF, no storage. Just jobs.

## Stack

- **.NET 10 Worker** (no ASP.NET)
- **SQL Server** via `Microsoft.Data.SqlClient` (no EF Core)
- **Redis** via `StackExchange.Redis`
- **RabbitMQ** via `RabbitMQ.Client`
- **Cron** via `Cronos`
- **Logging** via `Serilog`
- **Tests** via xunit + Moq + coverlet

## Architecture

```
src/
├── Program.cs                          # Worker bootstrap (DI + shutdown)
├── Core/
│   ├── BaseJob.cs                      # Abstract class (Name, Schedule, HandleAsync)
│   ├── Scheduler.cs                    # IHostedService: cron loop + per-job timeout
│   ├── ICronAdapter.cs                 # Cronos wrapper (testable)
│   ├── JobContext.cs                   # Logger passed to HandleAsync
│   ├── JobResult.cs                    # Status, DurationMs, Error
│   └── JobStatus.cs                    # Success | Failed | Cancelled | Timeout
├── Infrastructure/
│   ├── Database/SqlProvider.cs         # Singleton connection factory (Microsoft.Data.SqlClient)
│   ├── Redis/RedisProvider.cs          # Singleton ConnectionMultiplexer
│   ├── Messaging/RabbitMqProvider.cs   # Publisher + check()
│   └── Health/DefaultHealthChecker.cs  # SQL/Redis/Rabbit status check
├── Jobs/
│   ├── HealthCheckJob.cs               # Example: status a cada minuto
│   └── RegisterJobs.cs                 # DI extension
├── Shared/
│   ├── Config/EnvValidator.cs          # AppSettings + env loaders
│   ├── Errors/AppError.cs              # Hierarchy
│   └── Utils/Logger.cs | Shutdown.cs | Signals.cs
├── appsettings.json
└── appsettings.Development.json
```

## Quick start

### 1. Subir infra local (SQL Server + Redis + RabbitMQ)

```bash
make infra-up
```

### 2. Configurar `.env`

```bash
cp .env.example .env
# editar DATABASE_URL, RABBIT_URL, etc.
```

### 3. Rodar em dev

```bash
make dev
```

### 4. Adicionar um job

```bash
# 1. Criar src/Jobs/CleanupJob.cs:
cat > src/Jobs/CleanupJob.cs <<'EOF'
using JobService.Core;
using JobService.Shared.Config;
using Microsoft.Extensions.Options;

namespace JobService.Jobs;

public sealed class CleanupJob : BaseJob
{
    public override string Name => "cleanup";
    public override string Schedule => "0 3 * * *";
    public override string Description => "Remove registros antigos";

    public override async Task HandleAsync(JobContext context, CancellationToken ct)
    {
        context.Logger.Information("Running cleanup...");
        // await repo.DeleteOldRecords();
        await Task.CompletedTask;
    }
}
EOF

# 2. Registrar em src/Jobs/RegisterJobs.cs (no método AddJobs):
services.AddSingleton<CleanupJob>(...);

# 3. Resolver em src/Jobs/RegisterJobs.cs (no método ResolveJobs):
var cleanup = sp.GetRequiredService<CleanupJob>();
return new BaseJob[] { healthCheck, cleanup };
```

## Comandos

```bash
make dev          # hot-reload
make test         # 116 testes
make coverage     # XPlat Code Coverage
make lint         # dotnet format --verify-no-changes
make check        # lint + test + coverage
make build        # Release
make docker       # build image
make run          # one-shot
make infra-up     # docker compose up (SQL+Redis+Rabbit)
make infra-down   # docker compose down
make sonar        # SonarQube scan
make clean
```

## Configuração (env vars)

| Var | Default | Descrição |
|---|---|---|
| `ENVIRONMENT` | `local` | dev / staging / production |
| `LOG_LEVEL` | `Information` | Serilog level |
| `SHUTDOWN_TIMEOUT_SECONDS` | `30` | Max wait for cleanup on SIGTERM |
| `JOB_EXECUTION_TIMEOUT_SECONDS` | `300` | Per-job timeout |
| `DATABASE_URL` | (required) | SQL Server connection string |
| `DATABASE_COMMAND_TIMEOUT_SECONDS` | `10` | SELECT timeout |
| `REDIS_HOST` | `localhost` | URL (`redis://...`) ou hostname |
| `REDIS_PORT` | `6379` | (ignored if URL) |
| `REDIS_PASSWORD` | (empty) | |
| `REDIS_DB` | `0` | |
| `MESSAGING_ENABLED` | `false` | Enable RabbitMQ publisher |
| `RABBIT_URL` | `amqp://guest:guest@localhost:5672/` | |
| `RABBIT_USER` / `RABBIT_PASSWORD` | `guest` | |
| `HEALTH_CHECK_CRON` | `*/1 * * * *` | 5-field cron |
| `HEALTH_CHECK_ENABLED` | `true` | Disable health check |

## Princípios

- **S** Single Responsibility — cada job tem um único propósito
- **O** Open/Closed — adicionar um job = criar uma classe + 1 linha de registro
- **L** Liskov Substitution — todo `BaseJob` é intercambiável
- **I** Interface Segregation — dependências injetadas via construtor
- **D** Dependency Inversion — jobs dependem de `IHealthChecker`, `ISqlProvider`, etc., não de implementações
- **DRY** — lógica compartilhada fica na `BaseJob`
- **Clean Code** — nomes expressivos, funções curtas, sem side-effects

## Testes

116 testes, ~97% line coverage (100% nos arquivos testáveis — paths que requerem
SQL Server / Redis real são cobertos por integração).

```bash
make test
```

## CI

GitHub Actions roda em push/PR para `develop` e `main`:
- `dotnet format --verify-no-changes`
- `dotnet build -c Release`
- `dotnet test --collect:"XPlat Code Coverage"`
- Coverage gate: 100% line + branch (excluindo `Program.cs` entry point)

## License

This is an open-source boilerplate. Use freely.
