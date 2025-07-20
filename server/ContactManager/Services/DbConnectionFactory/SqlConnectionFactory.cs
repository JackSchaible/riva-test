namespace ContactManager.Services.DbConnectionFactory;

using System.Data;
using Microsoft.Data.SqlClient;

public class SqlConnectionFactory(IConfiguration config) : IDbConnectionFactory
{
    private readonly string _connectionString = config.GetConnectionString("DefaultConnection")
                                                ?? throw new ArgumentNullException($"config", $"Connection string 'DefaultConnection' not found.");

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}