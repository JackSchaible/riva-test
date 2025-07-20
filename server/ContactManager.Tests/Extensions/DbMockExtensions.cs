namespace ContactManager.Tests.Extensions;

using System.Data;
using Dapper;
using Models.Data;
using Moq;
using Moq.Dapper;

public static class DbMockExtensions
{
    /// <summary>
    /// Sets up the database mock to return the specified contacts for Query&lt;Contact&gt; calls
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="contacts">The contacts to return</param>
    public static void SetupContactQuery(this Mock<IDbConnection> dbMock, List<Contact> contacts)
    {
        dbMock.SetupDapper(c => c.Query<Contact>(
            It.IsAny<string>(),
            null,
            null,
            true,
            null,
            null))
            .Returns(contacts);
    }

    /// <summary>
    /// Sets up the database mock to return the specified contacts for Query&lt;Contact&gt; calls with parameters
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="contacts">The contacts to return</param>
    public static void SetupContactQueryWithParams(this Mock<IDbConnection> dbMock, List<Contact> contacts)
    {
        dbMock.SetupDapper(c => c.Query<Contact>(
            It.IsAny<string>(),
            It.IsAny<DynamicParameters>(),
            null,
            true,
            null,
            null))
            .Returns(contacts);
    }

    /// <summary>
    /// Sets up the database mock to return a specific contact for QuerySingleOrDefault&lt;Contact&gt; calls
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="contact">The contact to return, or null if not found</param>
    public static void SetupContactQuerySingle(this Mock<IDbConnection> dbMock, Contact? contact)
    {
        dbMock.SetupDapper(c => c.QuerySingleOrDefault<Contact>(
            It.IsAny<string>(),
            It.IsAny<DynamicParameters>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<int?>(),
            It.IsAny<CommandType>()))
            .Returns(contact);
    }

    /// <summary>
    /// Sets up the database mock to return a specific ID for QuerySingle&lt;int&gt; calls (for Create operations)
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="newId">The ID to return for new records</param>
    public static void SetupCreateContactId(this Mock<IDbConnection> dbMock, int newId)
    {
        dbMock.SetupDapper(c => c.QuerySingle<int>(
            It.IsAny<string>(),
            It.IsAny<Contact>(),
            null,
            null,
            null))
            .Returns(newId);
    }

    /// <summary>
    /// Sets up the database mock to return the specified number of affected rows for Execute calls
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="affectedRows">The number of affected rows to return</param>
    public static void SetupExecuteResult(this Mock<IDbConnection> dbMock, int affectedRows)
    {
        dbMock.SetupDapper(c => c.Execute(
            It.IsAny<string>(),
            It.IsAny<object>(),
            null,
            null,
            null))
            .Returns(affectedRows);
    }

    /// <summary>
    /// Sets up basic database infrastructure mocks to prevent null reference exceptions
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    public static void SetupBasicInfrastructure(this Mock<IDbConnection> dbMock)
    {
        Mock<IDbCommand>? commandMock = new();
        Mock<IDataReader>? readerMock = new();

        dbMock.Setup(c => c.CreateCommand()).Returns(commandMock.Object);
        commandMock.Setup(c => c.ExecuteReader()).Returns(readerMock.Object);
        commandMock.Setup(c => c.ExecuteReader(It.IsAny<CommandBehavior>())).Returns(readerMock.Object);

        // Setup basic reader properties
        readerMock.Setup(r => r.Read()).Returns(false);
        readerMock.Setup(r => r.NextResult()).Returns(false);
    }

    /// <summary>
    /// Sets up common database mock scenarios for contact operations
    /// </summary>
    /// <param name="dbMock">The database connection mock</param>
    /// <param name="contacts">The contacts to return for query operations</param>
    /// <param name="newContactId">The ID to return for new contact creation</param>
    public static void SetupContactData(this Mock<IDbConnection> dbMock, List<Contact> contacts, int newContactId = 1)
    {
        // Setup basic infrastructure first
        dbMock.SetupBasicInfrastructure();

        // Setup for GetAll and Search operations
        dbMock.SetupContactQuery(contacts);
        dbMock.SetupContactQueryWithParams(contacts);

        // Setup for Update operations (finding existing contact)
        if (contacts.Any())
        {
            dbMock.SetupContactQuerySingle(contacts.First());
        }

        // Setup for Create operations
        dbMock.SetupCreateContactId(newContactId);

        // Setup for Update and Delete operations (1 row affected)
        dbMock.SetupExecuteResult(1);
    }
}