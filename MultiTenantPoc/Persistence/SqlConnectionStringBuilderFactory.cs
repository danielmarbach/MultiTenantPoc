using Microsoft.Data.SqlClient;

namespace MultiTenantPoc;

public static class SqlConnectionStringBuilderFactory
{
    public static string ForDatabase(string connectionString, string database)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = database
        };

        return builder.ConnectionString;
    }
}
