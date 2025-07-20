namespace ContactManager.Tests;

using Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Models.Data;
using Models.Data.Requests;
using Models.Data.Responses;
using Moq;
using Services;

[TestFixture]
public class ContactControllerTests
{
    private Mock<IContactService> _contactServiceMock;
    private Mock<ILogger<ContactController>> _loggerMock;
    private ContactController _controller;

    [SetUp]
    public void Setup()
    {
        _contactServiceMock = new Mock<IContactService>();
        _loggerMock = new Mock<ILogger<ContactController>>();
        _controller = new ContactController(_contactServiceMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Test]
    public void Constructor_WithNullContactService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            ContactController unused = new(null!, _loggerMock.Object);
        });
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            ContactController unused = new(_contactServiceMock.Object, null!);
        });
    }

    [Test]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        ContactController controller = new ContactController(_contactServiceMock.Object, _loggerMock.Object);

        Assert.That(controller, Is.Not.Null);
    }

    #endregion

    #region GetAllContacts Tests

    [Test]
    public async Task GetAllContacts_WhenContactsExist_ReturnsOkWithContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _contactServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(contacts);

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.GetAllContacts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;
            Assert.That(okResult?.StatusCode, Is.EqualTo(StatusCodes.Status200OK));

            ApiResponse<List<ContactResponse>>? response = okResult?.Value as ApiResponse<List<ContactResponse>>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Has.Count.EqualTo(contacts.Count));
            Assert.That(response.Message, Contains.Substring($"Retrieved {contacts.Count} contacts"));
        }
    }

    [Test]
    public async Task GetAllContacts_WhenNoContacts_ReturnsOkWithEmptyList()
    {
        _contactServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Contact>());

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.GetAllContacts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<List<ContactResponse>>? response = okResult?.Value as ApiResponse<List<ContactResponse>>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Is.Empty);
            Assert.That(response.Message, Contains.Substring("Retrieved 0 contacts"));
        }
    }

    [Test]
    public async Task GetAllContacts_WhenServiceThrows_ReturnsInternalServerError()
    {
        _contactServiceMock.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.GetAllContacts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));

            ApiResponse<object>? response = objectResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Contains.Substring("error occurred"));
        }
    }

    #endregion

    #region SearchContacts Tests

    [Test]
    public async Task SearchContacts_WithValidQuery_ReturnsOkWithResults()
    {
        const string query = "John";
        List<Contact> contacts = BogusContacts.GetContacts().Take(2).ToList();
        _contactServiceMock.Setup(s => s.SearchAsync(query)).ReturnsAsync(contacts);

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.SearchContacts(query);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<List<ContactResponse>>? response = okResult?.Value as ApiResponse<List<ContactResponse>>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Has.Count.EqualTo(contacts.Count));
            Assert.That(response.Message, Contains.Substring($"Found {contacts.Count} contacts"));
        }
    }

    [Test]
    public async Task SearchContacts_WithNullQuery_ReturnsOkWithAllContacts()
    {
        List<Contact> contacts = BogusContacts.GetContacts();
        _contactServiceMock.Setup(s => s.SearchAsync("")).ReturnsAsync(contacts);

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.SearchContacts();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<List<ContactResponse>>? response = okResult?.Value as ApiResponse<List<ContactResponse>>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Has.Count.EqualTo(contacts.Count));
        }
    }

    [Test]
    public async Task SearchContacts_WithTooLongQuery_ReturnsBadRequest()
    {
        string longQuery = new('A', 101);

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.SearchContacts(longQuery);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors, Contains.Item("Search query must not exceed 100 characters"));
        }
    }

    [Test]
    public async Task SearchContacts_WhenServiceThrows_ReturnsInternalServerError()
    {
        const string query = "John";
        _contactServiceMock.Setup(s => s.SearchAsync(It.IsAny<string>())).ThrowsAsync(new Exception("Search error"));

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.SearchContacts(query);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }
    }

    #endregion

    #region GetContact Tests

    [Test]
    public async Task GetContact_WithValidId_ReturnsOkWithContact()
    {
        const int contactId = 1;
        List<Contact> contacts = BogusContacts.GetContacts();
        Contact targetContact = contacts.First();
        targetContact.Id = contactId;

        _contactServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(contacts);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.GetContact(contactId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<ContactResponse>? response = okResult?.Value as ApiResponse<ContactResponse>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data?.Id, Is.EqualTo(contactId));
        }
    }

    [Test]
    public async Task GetContact_WithInvalidId_ReturnsBadRequest()
    {
        const int invalidId = 0;

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.GetContact(invalidId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Contains.Substring("positive integer"));
        }
    }

    [Test]
    public async Task GetContact_WithNegativeId_ReturnsBadRequest()
    {
        const int negativeId = -1;

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.GetContact(negativeId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
        }
    }

    [Test]
    public async Task GetContact_WithNonExistentId_ReturnsNotFound()
    {
        const int nonExistentId = 999;
        List<Contact> contacts = BogusContacts.GetContacts();
        _contactServiceMock.Setup(s => s.GetAllAsync()).ReturnsAsync(contacts);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.GetContact(nonExistentId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;

            ApiResponse<object>? response = notFoundResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Contains.Substring($"Contact with ID {nonExistentId} was not found"));
        }
    }

    #endregion

    #region CreateContact Tests

    [Test]
    public async Task CreateContact_WithValidRequest_ReturnsCreatedResult()
    {
        CreateContactRequest request = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "123-456-7890"
        };

        Contact createdContact = new()
        {
            Id = 1,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone
        };

        _contactServiceMock.Setup(s => s.CreateAsync(It.IsAny<Contact>())).ReturnsAsync(createdContact);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
            CreatedAtActionResult? createdResult = result.Result as CreatedAtActionResult;
            Assert.That(createdResult?.StatusCode, Is.EqualTo(StatusCodes.Status201Created));

            ApiResponse<ContactResponse>? response = createdResult?.Value as ApiResponse<ContactResponse>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data?.Id, Is.EqualTo(createdContact.Id));
            Assert.That(response.Data?.FirstName, Is.EqualTo(request.FirstName));
        }
    }

    [Test]
    public async Task CreateContact_WithInvalidRequest_ReturnsBadRequest()
    {
        CreateContactRequest request = new CreateContactRequest
        {
            FirstName = "", // Invalid - required
            LastName = "Doe",
            Email = "invalid-email", // Invalid format
            Phone = "123-456-7890"
        };

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors, Is.Not.Empty);
        }
    }

    [Test]
    public async Task CreateContact_WithModelStateErrors_ReturnsBadRequest()
    {
        CreateContactRequest request = new CreateContactRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "123-456-7890"
        };

        // Manually add model state errors to simulate framework validation failures
        _controller.ModelState.AddModelError("FirstName", "Custom model error");

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors, Contains.Item("Custom model error"));
        }
    }

    [Test]
    public async Task CreateContact_WhenServiceThrows_ReturnsInternalServerError()
    {
        CreateContactRequest request = new CreateContactRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            Phone = "123-456-7890"
        };

        _contactServiceMock.Setup(s => s.CreateAsync(It.IsAny<Contact>())).ThrowsAsync(new Exception("Create error"));

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }
    }

    #endregion

    #region UpdateContact Tests

    [Test]
    public async Task UpdateContact_WithValidRequest_ReturnsOkWithUpdatedContact()
    {
        const int contactId = 1;
        UpdateContactRequest request = new UpdateContactRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "987-654-3210"
        };

        Contact updatedContact = new Contact
        {
            Id = contactId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone
        };

        _contactServiceMock.Setup(s => s.UpdateAsync(contactId, It.IsAny<Contact>())).ReturnsAsync(updatedContact);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.UpdateContact(contactId, request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<ContactResponse>? response = okResult?.Value as ApiResponse<ContactResponse>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data?.Id, Is.EqualTo(contactId));
            Assert.That(response.Data?.FirstName, Is.EqualTo(request.FirstName));
        }
    }

    [Test]
    public async Task UpdateContact_WithInvalidId_ReturnsBadRequest()
    {
        const int invalidId = 0;
        UpdateContactRequest request = new UpdateContactRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "987-654-3210"
        };

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.UpdateContact(invalidId, request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
        }
    }

    [Test]
    public async Task UpdateContact_WithNonExistentContact_ReturnsNotFound()
    {
        const int contactId = 999;
        UpdateContactRequest request = new UpdateContactRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com",
            Phone = "987-654-3210"
        };

        _contactServiceMock.Setup(s => s.UpdateAsync(contactId, It.IsAny<Contact>())).ReturnsAsync((Contact?)null);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.UpdateContact(contactId, request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;

            ApiResponse<object>? response = notFoundResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Contains.Substring($"Contact with ID {contactId} was not found"));
        }
    }

    [Test]
    public async Task UpdateContact_WithInvalidRequest_ReturnsBadRequest()
    {
        const int contactId = 1;
        UpdateContactRequest request = new UpdateContactRequest
        {
            FirstName = "", // Invalid - required
            LastName = "Smith",
            Email = "invalid-email", // Invalid format
            Phone = "987-654-3210"
        };

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.UpdateContact(contactId, request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors, Is.Not.Empty);
        }
    }

    #endregion

    #region DeleteContact Tests

    [Test]
    public async Task DeleteContact_WithValidId_ReturnsOkResult()
    {
        const int contactId = 1;
        _contactServiceMock.Setup(s => s.DeleteAsync(contactId)).ReturnsAsync(true);

        ActionResult<ApiResponse<object>> result = await _controller.DeleteContact(contactId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<object>? response = okResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Contains.Substring("deleted successfully"));
        }
    }

    [Test]
    public async Task DeleteContact_WithInvalidId_ReturnsBadRequest()
    {
        const int invalidId = 0;

        ActionResult<ApiResponse<object>> result = await _controller.DeleteContact(invalidId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;

            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
        }
    }

    [Test]
    public async Task DeleteContact_WithNonExistentId_ReturnsNotFound()
    {
        const int contactId = 999;
        _contactServiceMock.Setup(s => s.DeleteAsync(contactId)).ReturnsAsync(false);

        ActionResult<ApiResponse<object>> result = await _controller.DeleteContact(contactId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<NotFoundObjectResult>());
            NotFoundObjectResult? notFoundResult = result.Result as NotFoundObjectResult;

            ApiResponse<object>? response = notFoundResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Contains.Substring($"Contact with ID {contactId} was not found"));
        }
    }

    [Test]
    public async Task DeleteContact_WhenServiceThrows_ReturnsInternalServerError()
    {
        const int contactId = 1;
        _contactServiceMock.Setup(s => s.DeleteAsync(contactId)).ThrowsAsync(new Exception("Delete error"));

        ActionResult<ApiResponse<object>> result = await _controller.DeleteContact(contactId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<ObjectResult>());
            ObjectResult? objectResult = result.Result as ObjectResult;
            Assert.That(objectResult?.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        }
    }

    #endregion

    #region Integration and Edge Case Tests

    [Test]
    public async Task CreateContact_WithMaxLengthValues_ReturnsCreatedResult()
    {
        CreateContactRequest request = new CreateContactRequest
        {
            FirstName = new string('A', 64), // Max length
            LastName = new string('B', 64),  // Max length
            Email = new string('c', 243) + "@example.com", // Max length (255 total)
            Phone = new string('1', 256) // Max length
        };

        Contact createdContact = new Contact
        {
            Id = 1,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone
        };

        _contactServiceMock.Setup(s => s.CreateAsync(It.IsAny<Contact>())).ReturnsAsync(createdContact);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<CreatedAtActionResult>());
            CreatedAtActionResult? createdResult = result.Result as CreatedAtActionResult;

            ApiResponse<ContactResponse>? response = createdResult?.Value as ApiResponse<ContactResponse>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
        }
    }

    [Test]
    public async Task CreateContact_WithNullOptionalFields_ReturnsFailedResult()
    {
        CreateContactRequest request = new()
        {
            FirstName = "John",
            LastName = "Doe",
            Email = null!,
            Phone = null!
        };

        Contact createdContact = new Contact
        {
            Id = 1,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Phone = request.Phone
        };

        _contactServiceMock.Setup(s => s.CreateAsync(It.IsAny<Contact>())).ReturnsAsync(createdContact);

        ActionResult<ApiResponse<ContactResponse>> result = await _controller.CreateContact(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
            BadRequestObjectResult? badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            
            if (badRequestResult == null) return;
            
            Assert.That(badRequestResult.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
            
            ApiResponse<object>? response = badRequestResult?.Value as ApiResponse<object>;
            Assert.That(response, Is.Not.Null);

            if (response == null) return;

            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors, Contains.Item("Email is required"));
            Assert.That(response.Errors, Contains.Item("Phone number is required"));
        }
    }

    [Test]
    public async Task SearchContacts_WithEmptyResults_ReturnsOkWithEmptyList()
    {
        const string query = "NonExistentName";
        _contactServiceMock.Setup(s => s.SearchAsync(query)).ReturnsAsync(new List<Contact>());

        ActionResult<ApiResponse<List<ContactResponse>>> result = await _controller.SearchContacts(query);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
            OkObjectResult? okResult = result.Result as OkObjectResult;

            ApiResponse<List<ContactResponse>>? response = okResult?.Value as ApiResponse<List<ContactResponse>>;
            Assert.That(response, Is.Not.Null);
            
            if (response == null) return;
            
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Is.Empty);
            Assert.That(response.Message, Contains.Substring("Found 0 contacts"));
        }
    }

    #endregion
}
