using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using ContactManager.Models.Data.Requests;
using ContactManager.Models.Data.Responses;
using Microsoft.Extensions.Configuration;
using ContactManager.IntegrationTests.Infrastructure;

namespace ContactManager.IntegrationTests;

public class ContactControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContactControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        WebApplicationFactory<Program> factory1 = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("ConnectionStrings:DefaultConnection",
                        "Server=localhost,1434;Database=ContactManager;User=sa;Password=TestPassword123!;TrustServerCertificate=True;"),
                    new KeyValuePair<string, string?>("AllowedOrigins:0", "http://localhost:5173"),
                    new KeyValuePair<string, string?>("AllowedOrigins:1", "http://localhost:5174")
                ]);
            });
        });

        _client = factory1.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task InitializeAsync()
    {
        await DockerTestHelper.StartTestDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await DockerTestHelper.StopTestDatabaseAsync();
        _client.Dispose();
    }

    [Fact]
    public async Task Ping_ShouldReturnPong()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/contact/ping");
        string content = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Equal("\"Pong\"", content);
    }

    [Fact]
    public async Task GetAllContacts_ShouldReturnSuccessResponse()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/contact");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<List<ContactResponse>>? apiResponse =
            JsonSerializer.Deserialize<ApiResponse<List<ContactResponse>>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.IsType<List<ContactResponse>>(apiResponse.Data);
            Assert.Equal(5, apiResponse.Data.Count); // There should be 5 contacts from the setup script
        });
    }

    [Fact]
    public async Task SearchContacts_WithValidQuery_ShouldReturnMatchingContacts()
    {
        const string searchQuery = "john";

        HttpResponseMessage response = await _client.GetAsync($"/api/contact/search?query={searchQuery}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<List<ContactResponse>>? apiResponse =
            JsonSerializer.Deserialize<ApiResponse<List<ContactResponse>>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.IsType<List<ContactResponse>>(apiResponse.Data);
            Assert.All(apiResponse.Data, contact =>
            {
                bool containsInFirstName = contact.FirstName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                bool containsInLastName = contact.LastName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
                bool containsInEmail = contact.Email.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);

                Assert.True(containsInFirstName || containsInLastName || containsInEmail,
                    $"Search query '{searchQuery}' should be found in at least one field (FirstName: '{contact.FirstName}', LastName: '{contact.LastName}', Email: '{contact.Email}')");
            });
        });
    }

    [Fact]
    public async Task SearchContacts_WithEmptyQuery_ShouldReturnBadRequest()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/contact/search?query=");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.NotNull(apiResponse.Errors);
            Assert.NotEmpty(apiResponse.Errors);
            Assert.Single(apiResponse.Errors);
            Assert.Contains("Search query is required", apiResponse.Errors[0]);
        });
    }

    [Fact]
    public async Task SearchContacts_WithShortQuery_ShouldReturnBadRequest()
    {
        const string shortQuery = "ab"; // Less than 3 characters

        HttpResponseMessage response = await _client.GetAsync($"/api/contact/search?query={shortQuery}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.NotNull(apiResponse.Errors);
            Assert.Single(apiResponse.Errors);
            Assert.Contains("Search query must be between 3 and 100 characters", apiResponse.Errors[0]);
        });
    }

    [Fact]
    public async Task CreateContact_WithValidData_ShouldCreateContactAndReturnCreated()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Gon",
            LastName = "Freecss",
            Email = "gon.freecss@hotmail.com",
            Phone = "(780) 555-0123"
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? apiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(createRequest.FirstName, apiResponse.Data.FirstName);
            Assert.Equal(createRequest.LastName, apiResponse.Data.LastName);
            Assert.Equal(createRequest.Email, apiResponse.Data.Email);
            Assert.Equal(createRequest.Phone, apiResponse.Data.Phone);
            Assert.True(apiResponse.Data.Id > 0);
        });

        if (apiResponse?.Data != null) await _client.DeleteAsync($"/api/contact/{apiResponse.Data.Id}");
    }

    [Fact]
    public async Task CreateContact_WithInvalidEmail_ShouldReturnBadRequest()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Killua",
            LastName = "Zoldyck",
            Email = "invalid-email", // Invalid email format
            Phone = "(780) 555-0124"
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.NotNull(apiResponse.Errors);
            Assert.NotEmpty(apiResponse.Errors);
            Assert.Single(apiResponse.Errors);
            Assert.Contains("Please provide a valid email address", apiResponse.Errors[0]);
        });
    }

    [Fact]
    public async Task CreateContact_WithMissingRequiredFields_ShouldReturnBadRequest()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = string.Empty, // Missing required field
            LastName = "Bernezzo",
            Email = "firo.bernezzo@hotmail.com",
            Phone = "(780) 555-0125"
        };

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.NotNull(apiResponse.Errors);
            Assert.NotEmpty(apiResponse.Errors);
            Assert.Equal(2, apiResponse.Errors.Count);
            Assert.Contains("First name is required", apiResponse.Errors[0]);
            Assert.Contains("First name must be between 1 and 64 characters", apiResponse.Errors[1]);
        });
    }

    [Fact]
    public async Task GetContact_WithValidId_ShouldReturnContact()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Kurapika",
            LastName = "Kurta",
            Email = "kurapika.kurta@hotmail.com",
            Phone = "(780) 555-0126"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string createJsonResponse = await createResponse.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? createApiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(createJsonResponse, _jsonOptions);
        int contactId = createApiResponse!.Data!.Id;

        HttpResponseMessage response = await _client.GetAsync($"/api/contact/{contactId}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? apiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(contactId, apiResponse.Data.Id);
            Assert.Equal(createRequest.FirstName, apiResponse.Data.FirstName);
            Assert.Equal(createRequest.LastName, apiResponse.Data.LastName);
            Assert.Equal(createRequest.Email, apiResponse.Data.Email);
            Assert.Equal(createRequest.Phone, apiResponse.Data.Phone);
        });

        // Cleanup
        await _client.DeleteAsync($"/api/contact/{contactId}");
    }

    [Fact]
    public async Task GetContact_WithInvalidId_ShouldReturnBadRequest()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/contact/-1");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.Equal("Contact ID must be a positive integer", apiResponse.Message);
        });
    }

    [Fact]
    public async Task GetContact_WithNonExistentId_ShouldReturnNotFound()
    {
        const int nonExistentId = 99999; // Assuming this ID does not exist
        HttpResponseMessage response = await _client.GetAsync($"/api/contact/{nonExistentId}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Success);
        Assert.Equal($"Contact with ID {nonExistentId} was not found", apiResponse.Message);
    }

    [Fact]
    public async Task UpdateContact_WithValidData_ShouldUpdateContactAndReturnSuccess()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Isaac",
            LastName = "Dian",
            Email = "isaac.dian@hotmail.com",
            Phone = "(780) 555-0127"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string createJsonResponse = await createResponse.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? createApiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(createJsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.NotNull(createApiResponse);
            Assert.True(createApiResponse.Success);
            Assert.NotNull(createApiResponse.Data);
        });

        if (createApiResponse?.Data == null)
        {
            throw new InvalidOperationException("Failed to create contact for update test");
        }

        int contactId = createApiResponse.Data.Id;

        UpdateContactRequest updateRequest = new()
        {
            FirstName = "Miria",
            LastName = "Harvent",
            Email = "miria.harvent@hotmail.com",
            Phone = "(780) 555-9999"
        };

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/contact/{contactId}", updateRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? apiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.NotNull(apiResponse.Data);
            Assert.Equal(updateRequest.FirstName, apiResponse.Data.FirstName);
            Assert.Equal(updateRequest.LastName, apiResponse.Data.LastName);
            Assert.Equal(updateRequest.Email, apiResponse.Data.Email);
            Assert.Equal(updateRequest.Phone, apiResponse.Data.Phone);
        });
        await _client.DeleteAsync($"/api/contact/{contactId}");
    }

    [Fact]
    public async Task UpdateContact_WithInvalidId_ShouldReturnBadRequest()
    {
        UpdateContactRequest updateRequest = new()
        {
            FirstName = "Leorio",
            LastName = "Paradinight",
            Email = "leorio.paradinight@hotmail.com",
            Phone = "(780) 555-0128"
        };

        HttpResponseMessage response = await _client.PutAsJsonAsync("/api/contact/-1", updateRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.Equal("Contact ID must be a positive integer", apiResponse.Message);
        });
    }

    [Fact]
    public async Task UpdateContact_WithNonExistentId_ShouldReturnNotFound()
    {
        UpdateContactRequest updateRequest = new()
        {
            FirstName = "Edward",
            LastName = "Elric",
            Email = "edward.elric@hotmail.com",
            Phone = "(780) 555-0129"
        };

        const int nonExistentId = 99999;
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/contact/{nonExistentId}", updateRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.Contains($"Contact with ID {nonExistentId} was not found", apiResponse.Message);
        });
    }

    [Fact]
    public async Task UpdateContact_WithInvalidData_ShouldReturnBadRequest()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Ichigo",
            LastName = "Kurosaki",
            Email = "ichigo.kurosaki@hotmail.com",
            Phone = "(780) 555-0130"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string createJsonResponse = await createResponse.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? createApiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(createJsonResponse, _jsonOptions);
        int contactId = createApiResponse!.Data!.Id;

        UpdateContactRequest updateRequest = new()
        {
            FirstName = string.Empty,
            LastName = "Kamado",
            Email = "tanjiro.kamado@hotmail.com",
            Phone = "(780) 555-0131"
        };

        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/contact/{contactId}", updateRequest);
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.NotNull(apiResponse.Errors);
            Assert.NotEmpty(apiResponse.Errors);
            Assert.Equal(2, apiResponse.Errors.Count);
            Assert.Contains("First name is required", apiResponse.Errors[0]);
            Assert.Contains("First name must be between 1 and 64 characters", apiResponse.Errors[1]);
        });
        await _client.DeleteAsync($"/api/contact/{contactId}");
    }

    [Fact]
    public async Task DeleteContact_WithValidId_ShouldDeleteContactAndReturnSuccess()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Luffy",
            LastName = "Monkey",
            Email = "luffy.monkey@hotmail.com",
            Phone = "(780) 555-0132"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string createJsonResponse = await createResponse.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? createApiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(createJsonResponse, _jsonOptions);
        int contactId = createApiResponse!.Data!.Id;

        HttpResponseMessage response = await _client.DeleteAsync($"/api/contact/{contactId}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.True(apiResponse.Success);
            Assert.Equal("Contact deleted successfully", apiResponse.Message);
        });

        HttpResponseMessage getResponse = await _client.GetAsync($"/api/contact/{contactId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteContact_WithInvalidId_ShouldReturnBadRequest()
    {
        HttpResponseMessage response = await _client.DeleteAsync("/api/contact/-1");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(apiResponse);
            Assert.False(apiResponse.Success);
            Assert.Equal("Contact ID must be a positive integer", apiResponse.Message);
        });
    }

    [Fact]
    public async Task DeleteContact_WithNonExistentId_ShouldReturnNotFound()
    {
        const int nonExistentId = 99999;
        HttpResponseMessage response = await _client.DeleteAsync($"/api/contact/{nonExistentId}");
        string jsonResponse = await response.Content.ReadAsStringAsync();
        ApiResponse<object>? apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, _jsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(apiResponse);
        Assert.False(apiResponse.Success);
        Assert.Equal($"Contact with ID {nonExistentId} was not found", apiResponse.Message);
    }

    [Fact]
    public async Task ContactWorkflow_CreateGetUpdateDelete_ShouldWorkEndToEnd()
    {
        CreateContactRequest createRequest = new()
        {
            FirstName = "Goku",
            LastName = "Son",
            Email = "goku.son@hotmail.com",
            Phone = "(780) 555-0133"
        };

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/contact", createRequest);
        string createJsonResponse = await createResponse.Content.ReadAsStringAsync();
        ApiResponse<ContactResponse>? createApiResponse =
            JsonSerializer.Deserialize<ApiResponse<ContactResponse>>(createJsonResponse, _jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.NotNull(createApiResponse?.Data);
        });

        if (createApiResponse?.Data == null)
        {
            throw new InvalidOperationException("Failed to create contact for workflow test");
        }

        int contactId = createApiResponse.Data.Id;

        HttpResponseMessage getResponse = await _client.GetAsync($"/api/contact/{contactId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        UpdateContactRequest updateRequest = new()
        {
            FirstName = "Natsu",
            LastName = "Dragneel",
            Email = "natsu.dragneel@hotmail.com",
            Phone = "(780) 555-9998"
        };

        HttpResponseMessage updateResponse = await _client.PutAsJsonAsync($"/api/contact/{contactId}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync($"/api/contact/{contactId}");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        HttpResponseMessage verifyResponse = await _client.GetAsync($"/api/contact/{contactId}");
        Assert.Equal(HttpStatusCode.NotFound, verifyResponse.StatusCode);
    }
}