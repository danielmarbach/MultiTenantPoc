var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", "Your_password123", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword);

var ravenDb = builder.AddContainer("ServiceControl-RavenDB", "particular/servicecontrol-ravendb")
    .WithHttpEndpoint(8080, 8080)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Management Studio");

var audit = builder.AddContainer("ServiceControl-Audit", "particular/servicecontrol-audit")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", sql.Resource.ConnectionStringExpression)
    .WithEnvironment("RAVENDB_CONNECTIONSTRING", ravenDb.GetEndpoint("http"))
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(44444, 44444)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("api/configuration")
    .WaitFor(sql)
    .WaitFor(ravenDb);

var serviceControl = builder.AddContainer("ServiceControl", "particular/servicecontrol")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", sql.Resource.ConnectionStringExpression)
    .WithEnvironment("RAVENDB_CONNECTIONSTRING", ravenDb.GetEndpoint("http"))
    .WithEnvironment("REMOTEINSTANCES", $"[{{\"api_uri\":\"{audit.GetEndpoint("http")}\"}}]")
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(33333, 33333)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("api/configuration")
    .WaitFor(sql)
    .WaitFor(ravenDb);

var monitoring = builder.AddContainer("ServiceControl-Monitoring", "particular/servicecontrol-monitoring")
    .WithEnvironment("TRANSPORTTYPE", "SQLServer")
    .WithEnvironment("CONNECTIONSTRING", sql.Resource.ConnectionStringExpression)
    .WithArgs("--setup-and-run")
    .WithHttpEndpoint(33633, 33633)
    .WithUrlForEndpoint("http", url => url.DisplayLocation = UrlDisplayLocation.DetailsOnly)
    .WithHttpHealthCheck("connection")
    .WaitFor(sql);

var servicePulse = builder.AddContainer("ServicePulse", "particular/servicepulse")
    .WithEnvironment("ENABLE_REVERSE_PROXY", "false")
    .WithHttpEndpoint(9090, 9090)
    .WithUrlForEndpoint("http", url => url.DisplayText = "ServicePulse")
    .WaitFor(serviceControl)
    .WaitFor(audit)
    .WaitFor(monitoring);

builder.AddProject<Projects.MultiTenantPoc>("multitenant-poc")
    .WithReference(sql)
    .WithEnvironment("Poc__SqlTransport__ConnectionString", sql.Resource.ConnectionStringExpression)
    .WaitFor(sql)
    .WaitFor(servicePulse);

builder.Build().Run();
