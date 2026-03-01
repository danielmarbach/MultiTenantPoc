var builder = DistributedApplication.CreateBuilder(args);

var sqlEdgePassword = builder.AddParameter("sql-edge-password", "Your_password123!", secret: true);

var hostSqlConnectionString = ReferenceExpression.Create($"Server=localhost,1433;User Id=sa;Password={sqlEdgePassword};TrustServerCertificate=True;Encrypt=False;Max Pool Size=200;");
var serviceControlAuditConnectionString = ReferenceExpression.Create($"Server=sql-edge,1433;Initial Catalog=ServiceControlAudit;User Id=sa;Password={sqlEdgePassword};TrustServerCertificate=True;Encrypt=False;Max Pool Size=200;");
var serviceControlMainConnectionString = ReferenceExpression.Create($"Server=sql-edge,1433;Initial Catalog=ServiceControl;User Id=sa;Password={sqlEdgePassword};TrustServerCertificate=True;Encrypt=False;Max Pool Size=200;");
var serviceControlMonitoringConnectionString = ReferenceExpression.Create($"Server=sql-edge,1433;Initial Catalog=ServiceControlMonitoring;User Id=sa;Password={sqlEdgePassword};TrustServerCertificate=True;Encrypt=False;Max Pool Size=200;");

var sqlEdge = builder.AddContainer("sql-edge", "mcr.microsoft.com/azure-sql-edge", "latest")
    .WithEnvironment("ACCEPT_EULA", "1")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlEdgePassword)
    .WithEndpoint(port: 1433, targetPort: 1433, name: "tcp");

var sqlInit = builder.AddContainer("sql-init", "mcr.microsoft.com/mssql-tools", "latest")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlEdgePassword)
    .WithEntrypoint("/bin/sh")
    .WithArgs(
        "-c",
        "SQLCMD=/opt/mssql-tools18/bin/sqlcmd; " +
        "if [ ! -x \"$SQLCMD\" ]; then SQLCMD=/opt/mssql-tools/bin/sqlcmd; fi; " +
        "until \"$SQLCMD\" -S sql-edge,1433 -U sa -P \"$MSSQL_SA_PASSWORD\" -C -Q \"SELECT 1\"; do sleep 2; done; " +
        "\"$SQLCMD\" -S sql-edge,1433 -U sa -P \"$MSSQL_SA_PASSWORD\" -C -Q \"IF DB_ID('ServiceControl') IS NULL CREATE DATABASE [ServiceControl]; IF DB_ID('ServiceControlAudit') IS NULL CREATE DATABASE [ServiceControlAudit]; IF DB_ID('ServiceControlMonitoring') IS NULL CREATE DATABASE [ServiceControlMonitoring];\"")
    .WaitFor(sqlEdge);

var ravenDb = builder.AddContainer("ServiceControl-RavenDB", "particular/servicecontrol-ravendb")
    .WithHttpEndpoint(8080, 8080)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Management Studio");

var audit = builder.AddContainer("ServiceControl-Audit", "particular/servicecontrol-audit")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", serviceControlAuditConnectionString)
    .WithEnvironment("RAVENDB_CONNECTIONSTRING", ravenDb.GetEndpoint("http"))
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(44444, 44444)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("api/configuration")
    .WaitFor(sqlEdge)
    .WaitForCompletion(sqlInit)
    .WaitFor(ravenDb);

var serviceControl = builder.AddContainer("ServiceControl", "particular/servicecontrol")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", serviceControlMainConnectionString)
    .WithEnvironment("RAVENDB_CONNECTIONSTRING", ravenDb.GetEndpoint("http"))
    .WithEnvironment("REMOTEINSTANCES", $"[{{\"api_uri\":\"{audit.GetEndpoint("http")}\"}}]")
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(33333, 33333)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("api/configuration")
    .WaitFor(sqlEdge)
    .WaitForCompletion(sqlInit)
    .WaitFor(ravenDb)
    .WaitFor(audit);

var monitoring = builder.AddContainer("ServiceControl-Monitoring", "particular/servicecontrol-monitoring")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", serviceControlMonitoringConnectionString)
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(33633, 33633)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("connection")
    .WaitFor(sqlEdge)
    .WaitForCompletion(sqlInit);

var servicePulse = builder.AddContainer("ServicePulse", "particular/servicepulse")
    .WithEnvironment("ENABLE_REVERSE_PROXY", "false")
    .WithHttpEndpoint(9090, 9090)
    .WithUrlForEndpoint("http", url => url.DisplayText = "ServicePulse")
    .WaitFor(serviceControl)
    .WaitFor(audit)
    .WaitFor(monitoring);

builder.AddProject<Projects.MultiTenantPoc>("multitenant-poc")
    .WithEnvironment("Poc__SqlTransport__ConnectionString", hostSqlConnectionString)
    .WaitFor(sqlEdge)
    .WaitForCompletion(sqlInit)
    .WaitFor(servicePulse);

builder.Build().Run();
