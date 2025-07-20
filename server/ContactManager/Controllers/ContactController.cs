using Microsoft.AspNetCore.Mvc;
using ContactManager.Models.Data;
using ContactManager.Models.Data.Requests;
using ContactManager.Models.Data.Responses;
using ContactManager.Services;
using ContactManager.Helpers;

namespace ContactManager.Controllers;

/// <summary>
/// Controller for managing contacts
/// </summary>
/// <param name="contactService">The service for handling contact operations</param>
/// <param name="logger">Logger for logging operations</param>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ContactController(IContactService contactService, ILogger<ContactController> logger)
    : ControllerBase
{
    private readonly IContactService _contactService =
        contactService ?? throw new ArgumentNullException(nameof(contactService));
    private readonly ILogger<ContactController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets all contacts
    /// </summary>
    /// <returns>List of all contacts</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ContactResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<List<ContactResponse>>>> GetAllContacts()
    {
        try
        {
            _logger.LogInformation("Getting all contacts");

            List<Contact> contacts = await _contactService.GetAllAsync();
            List<ContactResponse> response = ContactResponse.FromContacts(contacts);

            _logger.LogInformation("Retrieved {Count} contacts", contacts.Count);

            return Ok(ApiResponse<List<ContactResponse>>.SuccessResult(
                response,
                $"Retrieved {contacts.Count} contacts successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting all contacts");
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while retrieving contacts"
            ));
        }
    }

    /// <summary>
    /// Searches for contacts based on a query string
    /// </summary>
    /// <param name="query">Search query (optional)</param>
    /// <returns>List of matching contacts</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<List<ContactResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<List<ContactResponse>>>> SearchContacts([FromQuery] string? query = null)
    {
        try
        {
            // Create and validate search request
            SearchContactRequest searchRequest = new() { Query = query };
            (bool isValid, List<string> validationErrors) = ValidationHelper.ValidateModel(searchRequest);

            if (!isValid)
            {
                _logger.LogWarning("Search validation failed: {Errors}", string.Join(", ", validationErrors));
                return BadRequest(ApiResponse<object>.ValidationErrorResult(validationErrors));
            }

            string safeQuery = searchRequest.GetSafeQuery();
            _logger.LogInformation("Searching contacts with query: '{Query}'", safeQuery);

            List<Contact> contacts = await _contactService.SearchAsync(safeQuery);
            List<ContactResponse> response = ContactResponse.FromContacts(contacts);

            _logger.LogInformation("Search returned {Count} contacts for query '{Query}'", contacts.Count, safeQuery);

            return Ok(ApiResponse<List<ContactResponse>>.SuccessResult(
                response,
                $"Found {contacts.Count} contacts matching the search criteria"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while searching contacts with query '{Query}'", query);
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while searching contacts"
            ));
        }
    }

    /// <summary>
    /// Gets a specific contact by ID
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>Contact details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ContactResponse>>> GetContact(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid contact ID provided: {Id}", id);
                return BadRequest(ApiResponse<object>.ErrorResult(
                    "Contact ID must be a positive integer"
                ));
            }

            _logger.LogInformation("Getting contact with ID: {Id}", id);

            // Use search to simulate GetById since it's not in the interface
            List<Contact> allContacts = await _contactService.GetAllAsync();
            Contact? contact = allContacts.FirstOrDefault(c => c.Id == id);

            if (contact == null)
            {
                _logger.LogWarning("Contact not found with ID: {Id}", id);
                return NotFound(ApiResponse<object>.ErrorResult(
                    $"Contact with ID {id} was not found"
                ));
            }

            ContactResponse response = ContactResponse.FromContact(contact);
            _logger.LogInformation("Retrieved contact with ID: {Id}", id);

            return Ok(ApiResponse<ContactResponse>.SuccessResult(
                response,
                "Contact retrieved successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting contact with ID: {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while retrieving the contact"
            ));
        }
    }

    /// <summary>
    /// Creates a new contact
    /// </summary>
    /// <param name="request">Contact creation request</param>
    /// <returns>Created contact</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ContactResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ContactResponse>>> CreateContact([FromBody] CreateContactRequest request)
    {
        try
        {
            // Validate model state first
            if (!ModelState.IsValid)
            {
                List<string> modelErrors = ValidationHelper.GetModelStateErrors(ModelState);
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", modelErrors));
                return BadRequest(ApiResponse<object>.ValidationErrorResult(modelErrors));
            }

            // Additional custom validation
            (bool isValid, List<string> validationErrors) = ValidationHelper.ValidateModel(request);
            if (!isValid)
            {
                _logger.LogWarning("Request validation failed: {Errors}", string.Join(", ", validationErrors));
                return BadRequest(ApiResponse<object>.ValidationErrorResult(validationErrors));
            }

            _logger.LogInformation("Creating new contact: {FirstName} {LastName}", request.FirstName, request.LastName);

            Contact contact = request.ToContact();
            Contact createdContact = await _contactService.CreateAsync(contact);
            ContactResponse response = ContactResponse.FromContact(createdContact);

            _logger.LogInformation("Created contact with ID: {Id}", createdContact.Id);

            return CreatedAtAction(
                nameof(GetContact),
                new { id = createdContact.Id },
                ApiResponse<ContactResponse>.SuccessResult(
                    response,
                    "Contact created successfully"
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating contact");
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while creating the contact"
            ));
        }
    }

    /// <summary>
    /// Updates an existing contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <param name="request">Contact update request</param>
    /// <returns>Updated contact</returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<ContactResponse>>> UpdateContact(int id,
        [FromBody] UpdateContactRequest request)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid contact ID provided for update: {Id}", id);
                return BadRequest(ApiResponse<object>.ErrorResult(
                    "Contact ID must be a positive integer"
                ));
            }

            // Validate model state first
            if (!ModelState.IsValid)
            {
                List<string> modelErrors = ValidationHelper.GetModelStateErrors(ModelState);
                _logger.LogWarning("Model validation failed for update: {Errors}", string.Join(", ", modelErrors));
                return BadRequest(ApiResponse<object>.ValidationErrorResult(modelErrors));
            }

            // Additional custom validation
            (bool isValid, List<string> validationErrors) = ValidationHelper.ValidateModel(request);
            if (!isValid)
            {
                _logger.LogWarning("Request validation failed for update: {Errors}",
                    string.Join(", ", validationErrors));
                return BadRequest(ApiResponse<object>.ValidationErrorResult(validationErrors));
            }

            _logger.LogInformation("Updating contact with ID: {Id}", id);

            Contact contact = request.ToContact(id);
            Contact? updatedContact = await _contactService.UpdateAsync(id, contact);

            if (updatedContact == null)
            {
                _logger.LogWarning("Contact not found for update with ID: {Id}", id);
                return NotFound(ApiResponse<object>.ErrorResult(
                    $"Contact with ID {id} was not found"
                ));
            }

            ContactResponse response = ContactResponse.FromContact(updatedContact);
            _logger.LogInformation("Updated contact with ID: {Id}", id);

            return Ok(ApiResponse<ContactResponse>.SuccessResult(
                response,
                "Contact updated successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating contact with ID: {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while updating the contact"
            ));
        }
    }

    /// <summary>
    /// Deletes a contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteContact(int id)
    {
        try
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid contact ID provided for deletion: {Id}", id);
                return BadRequest(ApiResponse<object>.ErrorResult(
                    "Contact ID must be a positive integer"
                ));
            }

            _logger.LogInformation("Deleting contact with ID: {Id}", id);

            bool deleted = await _contactService.DeleteAsync(id);

            if (!deleted)
            {
                _logger.LogWarning("Contact not found for deletion with ID: {Id}", id);
                return NotFound(ApiResponse<object>.ErrorResult(
                    $"Contact with ID {id} was not found"
                ));
            }

            _logger.LogInformation("Deleted contact with ID: {Id}", id);

            return Ok(ApiResponse<object>.SuccessResult(
                null,
                "Contact deleted successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while deleting contact with ID: {Id}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult(
                "An error occurred while deleting the contact"
            ));
        }
    }


    /// <summary>
    /// Ping endpoint for health checks
    /// <returns>Pong response</returns>
    /// </summary>
    [HttpGet("ping")]
    public async Task<string> Ping()
    {
        _logger.LogInformation("Ping received");
        return await Task.FromResult("Pong");
    }
}