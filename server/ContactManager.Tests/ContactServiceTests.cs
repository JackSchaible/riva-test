namespace ContactManager.Tests;

using System.Data;
using Extensions;
using Models.Data;
using Moq;
using Moq.Dapper;
using Services;
using Services.DbConnectionFactory;

[TestFixture]
public class ContactServiceTests
{
    private Mock<IDbConnection> _dbMock;
    private Mock<IDbConnectionFactory> _factoryMock;
    private ContactService _contactService;

    [SetUp]
    public void Setup()
    {
        _dbMock = new Mock<IDbConnection>();
        _factoryMock = new Mock<IDbConnectionFactory>();
        _factoryMock
            .Setup(factory => factory.CreateConnection())
            .Returns(_dbMock.Object);

        _contactService = new ContactService(_factoryMock.Object);
    }

    #region Constructor Tests

    [Test]
    public void Constructor_WithNullConnectionFactory_ThrowsArgumentNullException()
    {
        // ReSharper disable once ObjectCreationAsStatement
        Assert.Throws<ArgumentNullException>(() => new ContactService(null!));
    }

    [Test]
    public void Constructor_WithValidConnectionFactory_CreatesInstance()
    {
        ContactService service = new(_factoryMock.Object);
        Assert.That(service, Is.Not.Null);
    }

    #endregion

    #region GetAll Tests

    [Test]
    public async Task GetAll_ShouldReturnAllContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _dbMock.SetupContactQuery(contacts);

        List<Contact> result = await _contactService.GetAllAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(contacts.Count));
        }

        for (int i = 0; i < contacts.Count; i++)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(result[i].Id, Is.EqualTo(contacts[i].Id));
                Assert.That(result[i].FirstName, Is.EqualTo(contacts[i].FirstName));
                Assert.That(result[i].LastName, Is.EqualTo(contacts[i].LastName));
                Assert.That(result[i].Email, Is.EqualTo(contacts[i].Email));
                Assert.That(result[i].Phone, Is.EqualTo(contacts[i].Phone));
            }
        }
    }

    [Test]
    public async Task GetAll_WhenNoContacts_ReturnsEmptyList()
    {
        _dbMock.SetupContactQuery([]);

        List<Contact> result = await _contactService.GetAllAsync();
        
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        }
    }

    [Test]
    public async Task GetAll_CallsCreateConnection()
    {
        _dbMock.SetupContactQuery([]);

        await _contactService.GetAllAsync();

        _factoryMock.Verify(f => f.CreateConnection(), Times.Once);
    }

    #endregion

    #region Search Tests

    [Test]
    public async Task Search_WithNullQuery_ReturnsAllContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _dbMock.SetupContactQuery(contacts);

        List<Contact> result = await _contactService.SearchAsync(null!);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(contacts.Count));
        });
    }

    [Test]
    public async Task Search_WithEmptyQuery_ReturnsAllContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _dbMock.SetupContactQuery(contacts);

        List<Contact> result = await _contactService.SearchAsync("");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(contacts.Count));
        });
    }

    [Test]
    public async Task Search_WithWhitespaceQuery_ReturnsAllContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _dbMock.SetupContactQuery(contacts);

        List<Contact> result = await _contactService.SearchAsync("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(contacts.Count));
        });
    }

    [Test]
    public async Task Search_WithValidQuery_ReturnsFilteredContacts()
    {
        List<Contact> allContacts = BogusContacts.GetContacts();
        string firstName = allContacts.First().FirstName;

        List<Contact> expectedContacts = allContacts
            .Where(c => c.FirstName.Contains(firstName, StringComparison.OrdinalIgnoreCase) ||
                        c.LastName.Contains(firstName, StringComparison.OrdinalIgnoreCase) ||
                        c.Email.Contains(firstName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _dbMock.SetupContactQueryWithParams(expectedContacts);

        List<Contact> result = await _contactService.SearchAsync(firstName);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(expectedContacts.Count));
        });
    }

    [Test]
    public async Task Search_WithMultipleTerms_ReturnsFilteredContacts()
    {
        List<Contact> allContacts = BogusContacts.GetContacts();
        Contact firstContact = allContacts.First();
        string firstTerm = firstContact.FirstName;
        string secondTerm = firstContact.LastName;

        List<Contact> expectedContacts = allContacts
            .Where(c => c.FirstName.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.LastName.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.Email.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.FirstName.Contains(secondTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.LastName.Contains(secondTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.Email.Contains(secondTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _dbMock.SetupContactQueryWithParams(expectedContacts);

        List<Contact> result = await _contactService.SearchAsync($"{firstTerm} {secondTerm}");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(expectedContacts.Count));
        });
    }

    [Test]
    public async Task Search_WithExtraSpaces_HandlesCorrectly()
    {
        List<Contact> allContacts = BogusContacts.GetContacts();
        Contact firstContact = allContacts.First();
        string firstTerm = firstContact.FirstName;
        string secondTerm = firstContact.LastName;

        List<Contact> expectedContacts = allContacts
            .Where(c => c.FirstName.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.LastName.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.Email.Contains(firstTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.FirstName.Contains(secondTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.LastName.Contains(secondTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.Email.Contains(secondTerm, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _dbMock.SetupContactQueryWithParams(expectedContacts);

        List<Contact> result = await _contactService.SearchAsync($"     {firstTerm}  {secondTerm}   ");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(expectedContacts.Count));
        });
    }

    [Test]
    public async Task Search_WithSpecialCharacters_ReturnsFilteredContacts()
    {
        List<Contact> allContacts = BogusContacts.GetContacts(1);
        Contact firstContact = allContacts.First();
        string email = firstContact.Email;

        List<Contact> expectedContacts = allContacts
            .Where(c => c.Email.Contains(email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _dbMock.SetupContactQueryWithParams(expectedContacts);

        List<Contact> result = await _contactService.SearchAsync(email);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(expectedContacts.Count));
        });
    }

    [Test]
    public async Task Search_WhenNoMatches_ReturnsEmptyList()
    {
        _dbMock.SetupContactQueryWithParams([]);

        List<Contact> result = await _contactService.SearchAsync("nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.Empty);
        });
    }

    [Test]
    public async Task Search_CallsCreateConnection()
    {
        _dbMock.SetupContactQueryWithParams([]);

        await _contactService.SearchAsync("test");

        _factoryMock.Verify(f => f.CreateConnection(), Times.Once);
    }

    #endregion

    #region Create Tests

    [Test]
    public async Task Create_WithValidContact_ReturnsContactWithId()
    {
        Contact newContact = new()
        {
            FirstName = "New",
            LastName = "User",
            Email = "new.user@example.com",
            Phone = "123-456-7890"
        };
        const int expectedId = 42;
        _dbMock.SetupCreateContactId(expectedId);

        Contact result = await _contactService.CreateAsync(newContact);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(expectedId));
            Assert.That(result.FirstName, Is.EqualTo(newContact.FirstName));
            Assert.That(result.LastName, Is.EqualTo(newContact.LastName));
            Assert.That(result.Email, Is.EqualTo(newContact.Email));
            Assert.That(result.Phone, Is.EqualTo(newContact.Phone));
        });
    }

    [Test]
    public async Task Create_WithEmptyStringProperties_ReturnsContactWithId()
    {
        Contact newContact = new()
        {
            FirstName = "",
            LastName = "",
            Email = "",
            Phone = ""
        };
        const int expectedId = 1;
        _dbMock.SetupCreateContactId(expectedId);

        Contact result = await _contactService.CreateAsync(newContact);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(expectedId));
            Assert.That(result.FirstName, Is.EqualTo(""));
            Assert.That(result.LastName, Is.EqualTo(""));
            Assert.That(result.Email, Is.EqualTo(""));
            Assert.That(result.Phone, Is.EqualTo(""));
        });
    }

    [Test]
    public async Task Create_WithNullStringProperties_ReturnsContactWithId()
    {
        Contact newContact = new()
        {
            FirstName = null!,
            LastName = null!,
            Email = null!,
            Phone = null!
        };
        const int expectedId = 1;
        _dbMock.SetupCreateContactId(expectedId);

        Contact result = await _contactService.CreateAsync(newContact);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(expectedId));
        });
    }

    [Test]
    public async Task Create_CallsCreateConnection()
    {
        Contact newContact = new() { FirstName = "Test", LastName = "User", Email = "test@example.com", Phone = "123-456-7890" };
        _dbMock.SetupCreateContactId(1);

        await _contactService.CreateAsync(newContact);

        _factoryMock.Verify(f => f.CreateConnection(), Times.Once);
    }

    #endregion

    #region Update Tests

    [Test]
    public async Task Update_WithExistingContact_ReturnsUpdatedContact()
    {
        const int contactId = 1;
        Contact existingContact = new()
        {
            Id = contactId,
            FirstName = "Original",
            LastName = "Name",
            Email = "original@example.com",
            Phone = "111-111-1111"
        };
        Contact updatedContact = new()
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "222-222-2222"
        };

        _dbMock.SetupContactQuerySingle(existingContact);

        Contact? result = await _contactService.UpdateAsync(contactId, updatedContact);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            if (result == null) return;

            Assert.That(result.Id, Is.EqualTo(contactId));
            Assert.That(result.FirstName, Is.EqualTo(updatedContact.FirstName));
            Assert.That(result.LastName, Is.EqualTo(updatedContact.LastName));
            Assert.That(result.Email, Is.EqualTo(updatedContact.Email));
            Assert.That(result.Phone, Is.EqualTo(updatedContact.Phone));
        });
    }

    [Test]
    public async Task Update_WithNonExistentContact_ReturnsNull()
    {
        const int contactId = 999;
        Contact updatedContact = new()
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "222-222-2222"
        };

        _dbMock.SetupContactQuerySingle(null);

        Contact? result = await _contactService.UpdateAsync(contactId, updatedContact);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Update_WithZeroId_ChecksForContact()
    {
        const int contactId = 0;
        Contact updatedContact = new()
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "222-222-2222"
        };

        _dbMock.SetupContactQuerySingle(null);

        Contact? result = await _contactService.UpdateAsync(contactId, updatedContact);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Update_WithNegativeId_ChecksForContact()
    {
        const int contactId = -1;
        Contact updatedContact = new()
        {
            FirstName = "Updated",
            LastName = "Name",
            Email = "updated@example.com",
            Phone = "222-222-2222"
        };

        _dbMock.SetupContactQuerySingle(null);

        Contact? result = await _contactService.UpdateAsync(contactId, updatedContact);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Update_WithEmptyStringProperties_UpdatesContact()
    {
        const int contactId = 1;
        Contact existingContact = new()
        {
            Id = contactId,
            FirstName = "Original",
            LastName = "Name",
            Email = "original@example.com",
            Phone = "111-111-1111"
        };
        Contact updatedContact = new()
        {
            FirstName = "",
            LastName = "",
            Email = "",
            Phone = ""
        };

        _dbMock.SetupContactQuerySingle(existingContact);

        Contact? result = await _contactService.UpdateAsync(contactId, updatedContact);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);

            if (result == null) return;

            Assert.That(result.Id, Is.EqualTo(contactId));
            Assert.That(result.FirstName, Is.EqualTo(""));
            Assert.That(result.LastName, Is.EqualTo(""));
            Assert.That(result.Email, Is.EqualTo(""));
            Assert.That(result.Phone, Is.EqualTo(""));
        });
    }

    [Test]
    public async Task Update_CallsCreateConnection()
    {
        const int contactId = 1;
        Contact existingContact = new()
        {
            Id = contactId,
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            Phone = "123-456-7890"
        };
        Contact updatedContact = new() { FirstName = "Updated", LastName = "User", Email = "updated@example.com", Phone = "987-654-3210" };

        _dbMock.SetupContactQuerySingle(existingContact);

        await _contactService.UpdateAsync(contactId, updatedContact);

        _factoryMock.Verify(f => f.CreateConnection(), Times.Once);
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task Delete_WithExistingContact_ReturnsTrue()
    {
        const int contactId = 1;
        _dbMock.SetupExecuteResult(1);

        bool result = await _contactService.DeleteAsync(contactId);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Delete_WithNonExistentContact_ReturnsFalse()
    {
        const int contactId = 999;
        _dbMock.SetupExecuteResult(0);

        bool result = await _contactService.DeleteAsync(contactId);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Delete_WithZeroId_ReturnsFalse()
    {
        const int contactId = 0;
        _dbMock.SetupExecuteResult(0);

        bool result = await _contactService.DeleteAsync(contactId);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Delete_WithNegativeId_ReturnsFalse()
    {
        const int contactId = -1;
        _dbMock.SetupExecuteResult(0);

        bool result = await _contactService.DeleteAsync(contactId);

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Delete_WhenMultipleRowsAffected_ReturnsTrue()
    {
        const int contactId = 1;
        _dbMock.SetupExecuteResult(2);

        bool result = await _contactService.DeleteAsync(contactId);

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Delete_CallsCreateConnection()
    {
        const int contactId = 1;
        _dbMock.SetupExecuteResult(1);

        await _contactService.DeleteAsync(contactId);

        _factoryMock.Verify(f => f.CreateConnection(), Times.Once);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task AllMethods_CallConnectionFactory()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        Contact testContact = BogusContacts.GetContacts(1).First();

        _dbMock.SetupContactQuery(contacts);
        await _contactService.GetAllAsync();

        _dbMock.SetupContactQueryWithParams(contacts);
        await _contactService.SearchAsync("test");

        _dbMock.SetupContactQuerySingle(contacts.FirstOrDefault());
        await _contactService.CreateAsync(testContact);

        _dbMock.SetupCreateContactId(42);
        await _contactService.UpdateAsync(1, testContact);

        _dbMock.SetupExecuteResult(1);
        await _contactService.DeleteAsync(1);

        _factoryMock.Verify(f => f.CreateConnection(), Times.Exactly(5));
    }

    [Test]
    public async Task Search_NullAndEmptyQueries_BothCallGetAll()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _dbMock.SetupContactQuery(contacts);

        await _contactService.SearchAsync(null!);
        await _contactService.SearchAsync("");
        await _contactService.SearchAsync("   ");

        _factoryMock.Verify(f => f.CreateConnection(), Times.Exactly(3));
    }

    #endregion
}
