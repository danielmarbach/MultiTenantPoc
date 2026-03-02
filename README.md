# Multi-tenant NServiceBus PoC (.NET 10)

Single ASP.NET Core project hosting multiple NServiceBus endpoints in one process.

## What it demonstrates

- Tenant-specific main endpoints for bulk ingestion.
- Tenant-specific partition endpoints (`p0`, `p1`, `p2`) for deterministic business-id routing.
- Database-per-tenant (`NsbPoc_<tenant>` by default).
- Schema-per-partition (`p0`, `p1`, `p2`) plus `dbo` for tenant main endpoint.
- `AddNServiceBusEndpoint` multi-host setup with endpoint identifiers.
- Minimal API sending via the tenant-keyed `IMessageSession`.
- Console logging with scopes and endpoint-based colors.
- EF Core auto-creates each tenant database and partition schemas on startup.
- OpenAPI document in development at `/openapi/v1.json`.
- Swagger UI in development at `/swagger`.

## Architecture diagrams

### High-level architecture

The PoC runs as a single host process. The API routes requests by tenant and partition into in-process NServiceBus endpoints. Endpoints persist into tenant-isolated SQL databases and report operational telemetry to the ServiceControl stack.

![High-level architecture](./HierarchyAndFlow.svg)

```mermaid
flowchart LR
    C[Clients / API Consumers] --> A[Application Layer<br/>ASP.NET Core + Tenant Routing]
    A --> M[Messaging Layer<br/>In-process Tenant Endpoints]
    M --> D[Persistence Layer<br/>SQL Server per Tenant]
    M --> O[Operations Layer<br/>ServiceControl / Audit / Monitoring / Pulse]
```

### System context (host, data, and operations)

This view shows how the API host, routing, SQL data layer, ServiceControl stack, and Aspire AppHost orchestration fit together.

```mermaid
flowchart LR
    U[Clients / API Consumers] --> API[MultiTenant API Host]
    API --> ROUTER[Tenant + Partition Routing]
    ROUTER --> MAIN[Tenant Main Endpoints]
    ROUTER --> PART[Tenant Partition Endpoints]

    subgraph APP[Single Application Process]
        API
        ROUTER
        MAIN
        PART
    end

    MAIN --> SQL[(SQL Server<br/>Tenant Databases)]
    PART --> SQL

    MAIN -. error/audit/metrics .-> OPS[ServiceControl Stack]
    PART -. error/audit/metrics .-> OPS

    subgraph OBS[Operations / Observability]
        OPS[ServiceControl + Audit + Monitoring]
        SP[ServicePulse]
        R[(RavenDB)]
        SP --> OPS
        OPS --> R
    end

    AH[.NET Aspire AppHost] --> APP
    AH --> SQL
    AH --> OBS
```

### Host with tenant endpoint topology

Each tenant has a main endpoint plus partition endpoints. Every endpoint has its own queue and writes to the tenant's database.

![Tenant endpoint layering](./TenantLayering.svg)

```mermaid
flowchart LR
    subgraph HOST["Host Process (.NET App)"]
        direction LR

        subgraph T1["Tenant A"]
            direction TB
            A_Q_MAIN[[Queue: tenant-a-main]] --> A_MAIN[Main Endpoint]
            A_Q_P0[[Queue: tenant-a-p0]] --> A_P0[Partition Endpoint p0]
            A_Q_P1[[Queue: tenant-a-p1]] --> A_P1[Partition Endpoint p1]
            A_Q_P2[[Queue: tenant-a-p2]] --> A_P2[Partition Endpoint p2]

            A_MAIN --> A_DB[(Tenant A Database)]
            A_P0 --> A_DB
            A_P1 --> A_DB
            A_P2 --> A_DB
        end

        subgraph T2["Tenant B"]
            direction TB
            B_Q_MAIN[[Queue: tenant-b-main]] --> B_MAIN[Main Endpoint]
            B_Q_P0[[Queue: tenant-b-p0]] --> B_P0[Partition Endpoint p0]
            B_Q_P1[[Queue: tenant-b-p1]] --> B_P1[Partition Endpoint p1]
            B_Q_P2[[Queue: tenant-b-p2]] --> B_P2[Partition Endpoint p2]

            B_MAIN --> B_DB[(Tenant B Database)]
            B_P0 --> B_DB
            B_P1 --> B_DB
            B_P2 --> B_DB
        end

        subgraph TN["Tenant N"]
            direction TB
            N_Q_MAIN[[Queue: tenant-n-main]] --> N_MAIN[Main Endpoint]
            N_Q_P0[[Queue: tenant-n-p0]] --> N_P0[Partition Endpoint p0]
            N_Q_P1[[Queue: tenant-n-p1]] --> N_P1[Partition Endpoint p1]
            N_Q_P2[[Queue: tenant-n-p2]] --> N_P2[Partition Endpoint p2]

            N_MAIN --> N_DB[(Tenant N Database)]
            N_P0 --> N_DB
            N_P1 --> N_DB
            N_P2 --> N_DB
        end
    end
```

## Configuration

Tenants and SQL transport are configured in `MultiTenantPoc/appsettings.json` under `Poc`.

## Run SQL Edge (Docker)

```bash
docker run --name sql-edge -e "ACCEPT_EULA=1" -e "MSSQL_SA_PASSWORD=Your_password123" -p 1433:1433 -d mcr.microsoft.com/azure-sql-edge:latest
```

## Run

```bash
dotnet run --project MultiTenantPoc/MultiTenantPoc.csproj
```

Then open `http://localhost:5122/swagger`.

## Run with .NET Aspire

```bash
dotnet run --project MultiTenantPoc.AppHost/MultiTenantPoc.AppHost.csproj
```

This starts SQL Server and the `MultiTenantPoc` app through the Aspire AppHost.
The AppHost uses the same SQL Edge image as the manual Docker command (`mcr.microsoft.com/azure-sql-edge:latest`).

## Test endpoints

Bulk to tenant main endpoint:

```bash
curl -X POST http://localhost:5122/api/tenant-a/bulk \
  -H "Content-Type: application/json" \
  -d '{"businessId":"import-batch-042","payload":"bulk-import"}'
```

Partitioned command to tenant partition endpoint:

```bash
curl -X POST http://localhost:5122/api/tenant-a/business \
  -H "Content-Type: application/json" \
  -d '{"businessId":"invoice-9917","payload":"process-order"}'
```

Check which partition/endpoint a business id maps to:

```bash
curl http://localhost:5122/api/tenant-a/partition/order-2026-00042
```
