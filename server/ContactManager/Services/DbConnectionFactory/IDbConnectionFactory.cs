namespace ContactManager.Services.DbConnectionFactory;

using System.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}