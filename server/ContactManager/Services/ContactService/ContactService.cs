namespace ContactManager.Services;

using System.Data;
using Dapper;
using DbConnectionFactory;
using Models.Data;

public class ContactService(IDbConnectionFactory connectionFactory) : IContactService
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory
        ?? throw new ArgumentNullException($"connectionFactory");
    private const string GetAllQuery =
        "SELECT Id, FirstName, LastName, Email, Phone FROM Contacts ORDER BY FirstName, LastName";

    private const string SearchQueryTemplate = """
                                                       SELECT Id, FirstName, LastName, Email, Phone 
                                                       FROM Contacts 
                                                       WHERE {0}
                                                       ORDER BY FirstName, LastName
                                               """;

    private const string CreateQuery = """
                                               INSERT INTO Contacts (FirstName, LastName, Email, Phone) 
                                               VALUES (@FirstName, @LastName, @Email, @Phone);
                                               SELECT CAST(SCOPE_IDENTITY() as int);
                                       """;

    private const string GetByIdQuery = "SELECT Id, FirstName, LastName, Email, Phone FROM Contacts WHERE Id = @Id";

    private const string UpdateQuery = """
                                               UPDATE Contacts 
                                               SET FirstName = @FirstName, LastName = @LastName, Email = @Email, Phone = @Phone 
                                               WHERE Id = @Id
                                       """;

    private const string DeleteQuery = "DELETE FROM Contacts WHERE Id = @Id";


    public async Task<List<Contact>> GetAllAsync()
    {
        using IDbConnection connection = _connectionFactory.CreateConnection();
        IEnumerable<Contact> result = connection.Query<Contact>(GetAllQuery);
        return await Task.FromResult(result.ToList());
    }

    public async Task<List<Contact>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllAsync();
        }

        // Split the query by spaces and create search terms
        string[] searchTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> whereConditions = new();
        DynamicParameters parameters = new();

        for (int i = 0; i < searchTerms.Length; i++)
        {
            string paramName = $"searchTerm{i}";
            string searchTerm = $"%{searchTerms[i]}%";

            whereConditions.Add(
                $"(FirstName LIKE @{paramName} OR LastName LIKE @{paramName} OR Email LIKE @{paramName})");
            parameters.Add(paramName, searchTerm);
        }

        string sql = string.Format(SearchQueryTemplate, string.Join(" AND ", whereConditions));

        using IDbConnection connection = _connectionFactory.CreateConnection();
        IEnumerable<Contact> result = connection.Query<Contact>(sql, parameters);
        return await Task.FromResult(result.ToList());
    }

    public async Task<Contact> CreateAsync(Contact contact)
    {
        using IDbConnection connection = _connectionFactory.CreateConnection();
        int id = connection.QuerySingle<int>(CreateQuery, contact);

        contact.Id = id;

        return await Task.FromResult(contact);
    }

    public async Task<Contact?> UpdateAsync(int id, Contact contact)
    {
        using IDbConnection connection = _connectionFactory.CreateConnection();

        Contact? existingContact = connection.QuerySingleOrDefault<Contact>(GetByIdQuery, new { Id = id });

        if (existingContact == null)
        {
            return null;
        }

        var parameters = new
        {
            Id = id,
            contact.FirstName,
            contact.LastName,
            contact.Email,
            contact.Phone
        };

        connection.Execute(UpdateQuery, parameters);

        contact.Id = id;
        return await Task.FromResult(contact);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using IDbConnection connection = _connectionFactory.CreateConnection();
        int rowsAffected = connection.Execute(DeleteQuery, new { Id = id });
        return await Task.FromResult(rowsAffected > 0);
    }
}