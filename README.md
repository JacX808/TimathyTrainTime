# TimathyTrainTime

A .NET 9 Web API that ingests live UK rail **TRUST Train Movements** and exposes clean endpoints for querying trains, movements, and current positions. Built with C# on ASP.NET Core, documented via Swagger, and backed by a relational DB (SQL Server recommended for local dev).

---

## Features

* **Always-on ingestion** of `TRAIN_MVT_ALL_TOC` (Network Rail TRUST) via an ActiveMQ/OpenWire client.
* **Durable processing** with idempotent keys to avoid duplicate events.
* **REST endpoints** for:

  * latest position per train,
  * movement history per train (time-bounded),
  * train IDs (optionally filtered by date).
* **Swagger UI** for quick exploration.
* **EF Core** data access with detailed debugging (dev-only).

---

## Project Layout (high level)

```
TTT/
  ├─ Controllers/
  │    └─ TrainsController.cs            # read-only endpoints
  ├─ Data/                               # EF Core DbContext + entities
  │    ├─ TttDbContext.cs
  │    ├─ TrainRun.cs
  │    ├─ MovementEvent.cs
  │    └─ CurrentTrainPosition.cs
  ├─ OpenRail/                           # ingestion + options
  │    ├─ NrodOptions.cs
  │    └─ MovementsIngestionService.cs   # BackgroundService (always-on consumer)
  ├─ Program.cs                          # DI, EF provider, Swagger
  └─ appsettings*.json
```

---

## Tech Stack

* **Runtime:** .NET 9 (ASP.NET Core)
* **API:** Minimal hosting, Swagger (Swashbuckle)
* **Messaging:** Apache.NMS ActiveMQ (OpenWire)
* **Database:** SQL Server (EF Core)
* **Tests:** NUnit + EF InMemory/SQLite (for unit/integration tests)

---

## API (current shape)

> Tip: Dates in query strings use ISO `yyyy-MM-dd` (e.g., `2025-11-25`).
> With `[ApiController]`, invalid formats return HTTP 400 automatically.

* **Get train IDs (optionally by service date)**
  `GET /api/trains?date=2025-11-25`
  → `["A1", "B2", "C3", ...]`

* **Get latest position for a train**
  `GET /api/trains/{trainId}/position`
  → `{ trainId, locStanox, reportedAt, direction, line, variationStatus }`

* **Get movement history for a train**
  `GET /api/trains/{trainId}/movements?from=2025-11-25T09:00:00Z&to=...`
  → ordered list of movement events with planned/actual timestamps & status

---

## Configuration

**User-Secrets / Environment variables**

```text
# Database (SQL Server)
DB_HOST=localhost
DB_PORT=1433
DB_USERNAME=sa
DB_PASSWORD=yourStrong(!)Password
DB_NAME=ttt

# OpenRail (NROD)
OpenRail__ConnectUrl=tcp://publicdatafeeds.networkrail.co.uk:61619?transport.useInactivityMonitor=false
OpenRail__NR_USERNAME=your_nrod_username
OpenRail__NR_PASSWORD=your_nrod_password
OpenRail__Topics__0=TRAIN_MVT_ALL_TOC
OpenRail__UseDurableSubscription=true
OpenRail__ClientId=ttt-nrod-dev
```

**Connection string (example):**

```
Server=localhost,1433;Database=ttt;User Id=sa;Password=yourStrong(!)Password;Encrypt=True;TrustServerCertificate=True;
```

---

## Running locally

1. **Start SQL Server (Docker)**

```bash
docker compose up -d
# service name: sqlserver (mapped to localhost:1433)
```

2. **Apply EF migrations** (if using EF migrations)

```bash
dotnet ef database update -p TTT -s TTT
```

3. **Run the API**

```bash
dotnet run --project TTT
# Swagger at http://localhost:5xyz/swagger
```

---

## Development notes

* EF Core detailed errors & sensitive data logging are **enabled in Development** only.
* The consumer runs as a `BackgroundService`; it reconnects and continues ingesting.
* Movement deduplication relies on a unique index:

  ```
  (TrainId, ActualTimestampMs, LocStanox, EventType)
  ```

---

## Install

> **TODO:** add a full “Install & Deploy” section tailored to this repo
> (DB bootstrap, initial schema/migrations, secrets setup, containerized API, CI/CD notes).

---

## Testing

* **Unit tests:** use EF InMemory and NUnit.
* **Date parsing:** controllers validate `DateOnly` as ISO `yyyy-MM-dd`; wrong formats return 400.
* **Integration tests:** WebApplicationFactory + in-memory DB recommended.

---

## License

MIT (or TBD—update as appropriate).

---

## Contributing

Issues and PRs are welcome. Please include a brief description and, where relevant, unit tests covering your change.
