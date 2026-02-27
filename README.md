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

## Test endpoints

Bulk to tenant main endpoint:

```bash
curl -X POST http://localhost:5122/api/tenant-a/bulk \
  -H "Content-Type: application/json" \
  -d '{"businessId":"11111111-1111-1111-1111-111111111111","payload":"bulk-import"}'
```

Partitioned command to tenant partition endpoint:

```bash
curl -X POST http://localhost:5122/api/tenant-a/business \
  -H "Content-Type: application/json" \
  -d '{"businessId":"22222222-2222-2222-2222-222222222222","payload":"process-order"}'
```

Check which partition/endpoint a business id maps to:

```bash
curl http://localhost:5122/api/tenant-a/partition/22222222-2222-2222-2222-222222222222
```
