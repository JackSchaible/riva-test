namespace ContactManager.Tests;

using Helpers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Models.Data;
using Models.Data.Requests;

[TestFixture]
public class ValidationTests
{
    #region CreateContactRequest Tests

    [Test]
    public void CreateContactRequest_WithValidData_PassesValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Yusuke",
            LastName = "Urameshi",
            Email = "yusuke.urameshi@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void CreateContactRequest_WithNullFirstName_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = null!,
            LastName = "Yuy",
            Email = "heero.yuy@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("First name is required"));
        }
    }

    [Test]
    public void CreateContactRequest_WithEmptyFirstName_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "",
            LastName = "Okazaki",
            Email = "tomoya.okazaki@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("First name is required"));
        }
    }

    [Test]
    public void CreateContactRequest_WithTooLongFirstName_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = new string('A', 65),
            LastName = "Komuro",
            Email = "takashi.komuro@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("First name must be between 1 and 64 characters"));
        }
    }

    [Test]
    public void CreateContactRequest_WithNullLastName_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Gon",
            LastName = null!,
            Email = "gon.freecss@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Last name is required"));
        }
    }

    [Test]
    public void CreateContactRequest_WithTooLongLastName_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Takami",
            LastName = new string('B', 67),
            Email = "takami.fujiwara@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Last name must be between 1 and 64 characters"));
        }
    }

    [Test]
    public void CreateContactRequest_WithInvalidEmail_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Spike",
            LastName = "Spiegel",
            Email = "invalid-email",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Please provide a valid email address"));
        }
    }

    [Test]
    public void CreateContactRequest_WithTooLongEmail_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Tanjiro",
            LastName = "Kamado",
            Email = new string('a', 246) + "@hotmail.com", // 257 characters total
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Email must not exceed 256 characters"));
        }
    }

    [Test]
    public void CreateContactRequest_WithTooLongPhone_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Shigeo",
            LastName = "Kageyama",
            Email = "shigeo.kageyama@hotmail.com",
            Phone = new string('1', 257)
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Phone number must be between 10 and 256 characters"));
        }
    }

    [Test]
    public void CreateContactRequest_WithNullEmail_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Yuno",
            LastName = "Grinberryall",
            Email = null!,
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? _) = ValidationHelper.ValidateModel(request);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void CreateContactRequest_WithNullPhone_FailsValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Rimuru",
            LastName = "Tempest",
            Email = "rimuru.tempest@hotmail.com",
            Phone = null!
        };

        (bool isValid, List<string>? _) = ValidationHelper.ValidateModel(request);

        Assert.That(isValid, Is.False);
    }

    [Test]
    public void CreateContactRequest_ToContact_MapsCorrectly()
    {
        CreateContactRequest request = new()
        {
            FirstName = "Thorfinn",
            LastName = "Karlsefni",
            Email = "thorfinn.karlsefni@hotmail.com",
            Phone = "(123) 456-7890"
        };

        Contact contact = request.ToContact();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(contact.Id, Is.EqualTo(0)); // Should be 0 for new contacts
            Assert.That(contact.FirstName, Is.EqualTo(request.FirstName));
            Assert.That(contact.LastName, Is.EqualTo(request.LastName));
            Assert.That(contact.Email, Is.EqualTo(request.Email));
            Assert.That(contact.Phone, Is.EqualTo(request.Phone));
        }
    }

    #endregion

    #region UpdateContactRequest Tests

    [Test]
    public void UpdateContactRequest_WithValidData_PassesValidation()
    {
        UpdateContactRequest request = new()
        {
            FirstName = "Izuku",
            LastName = "Midoriya",
            Email = "izuku.midoriya@hotmail.com",
            Phone = "(987) 654-3210"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void UpdateContactRequest_WithInvalidData_FailsValidation()
    {
        UpdateContactRequest request = new()
        {
            FirstName = "", // Empty
            LastName = new string('X', 101), // Too long
            Email = "invalid-email", // Invalid format
            Phone = new string('9', 257) // Too long
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Has.Count.EqualTo(4));
            Assert.That(errors, Contains.Item("First name is required"));
            Assert.That(errors, Contains.Item("Last name must be between 1 and 64 characters"));
            Assert.That(errors, Contains.Item("Please provide a valid email address"));
            Assert.That(errors, Contains.Item("Phone number must be between 10 and 256 characters"));
        }
    }

    [Test]
    public void UpdateContactRequest_ToContact_MapsCorrectlyWithId()
    {
        const int contactId = 42;
        UpdateContactRequest request = new()
        {
            FirstName = "Eren",
            LastName = "Yaegar",
            Email = "eren.yaegar@hotmail.com",
            Phone = "(987) 654-3210"
        };

        Contact contact = request.ToContact(contactId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(contact.Id, Is.EqualTo(contactId));
            Assert.That(contact.FirstName, Is.EqualTo(request.FirstName));
            Assert.That(contact.LastName, Is.EqualTo(request.LastName));
            Assert.That(contact.Email, Is.EqualTo(request.Email));
            Assert.That(contact.Phone, Is.EqualTo(request.Phone));
        }
    }

    #endregion

    #region SearchContactRequest Tests

    [Test]
    public void SearchContactRequest_WithValidQuery_PassesValidation()
    {
        SearchContactRequest request = new()
        {
            Query = "Alphonse Elric"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void SearchContactRequest_WithNullQuery_FailsValidation()
    {
        SearchContactRequest request = new()
        {
            Query = null
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Search query is required"));
        }
    }

    [Test]
    public void SearchContactRequest_WithEmptyQuery_FailsValidation()
    {
        SearchContactRequest request = new()
        {
            Query = ""
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Search query is required"));
        }
    }

    [Test]
    public void SearchContactRequest_WithTooLongQuery_FailsValidation()
    {
        SearchContactRequest request = new()
        {
            Query = new string('A', 101)
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Search query must be between 3 and 100 characters"));
        }
    }

    [Test]
    public void SearchContactRequest_WithTooShortQuery_FailsValidation()
    {
        SearchContactRequest request = new()
        {
            Query = "ab"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Contains.Item("Search query must be between 3 and 100 characters"));
        }
    }

    [Test]
    public void SearchContactRequest_GetSafeQuery_WithNullQuery_ReturnsEmptyString()
    {
        SearchContactRequest request = new() { Query = null };

        string safeQuery = request.GetSafeQuery();

        Assert.That(safeQuery, Is.EqualTo(""));
    }

    [Test]
    public void SearchContactRequest_GetSafeQuery_WithWhitespaceQuery_ReturnsEmptyString()
    {
        SearchContactRequest request = new() { Query = "   \t\n  " };

        string safeQuery = request.GetSafeQuery();

        Assert.That(safeQuery, Is.EqualTo(""));
    }

    [Test]
    public void SearchContactRequest_GetSafeQuery_WithValidQuery_ReturnsTrimmedQuery()
    {
        SearchContactRequest request = new() { Query = "  Chilchuck Tims  " };

        string safeQuery = request.GetSafeQuery();

        Assert.That(safeQuery, Is.EqualTo("Chilchuck Tims"));
    }

    #endregion

    #region ValidationHelper Tests

    [Test]
    public void ValidationHelper_ValidateModel_WithValidObject_ReturnsTrue()
    {
        CreateContactRequest validRequest = new()
        {
            FirstName = "Laios",
            LastName = "Touden",
            Email = "laios@hotmail.com",
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(validRequest);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void ValidationHelper_ValidateModel_WithInvalidObject_ReturnsFalse()
    {
        CreateContactRequest invalidRequest = new()
        {
            FirstName = "", // Required field empty
            LastName = "Senshi",
            Email = "invalid-email", // Invalid format
            Phone = "(123) 456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(invalidRequest);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.False);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(errors, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void ValidationHelper_GetModelStateErrors_WithNoErrors_ReturnsEmptyList()
    {
        ModelStateDictionary modelState = new();

        List<string> errors = ValidationHelper.GetModelStateErrors(modelState);

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void ValidationHelper_GetModelStateErrors_WithErrors_ReturnsErrorMessages()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("FirstName", "First name is required");
        modelState.AddModelError("Email", "Invalid email format");

        List<string> errors = ValidationHelper.GetModelStateErrors(modelState);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Count.EqualTo(2));
            Assert.That(errors, Contains.Item("First name is required"));
            Assert.That(errors, Contains.Item("Invalid email format"));
        }
    }

    [Test]
    public void ValidationHelper_GetModelStateErrors_WithMultipleErrorsPerField_ReturnsAllErrors()
    {
        ModelStateDictionary modelState = new();
        modelState.AddModelError("FirstName", "First name is required");
        modelState.AddModelError("FirstName", "First name is too short");
        modelState.AddModelError("Email", "Invalid email format");

        List<string> errors = ValidationHelper.GetModelStateErrors(modelState);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors, Has.Count.EqualTo(3));
            Assert.That(errors, Contains.Item("First name is required"));
            Assert.That(errors, Contains.Item("First name is too short"));
            Assert.That(errors, Contains.Item("Invalid email format"));
        }
    }

    #endregion

    #region Edge Cases and Complex Validation Tests

    [Test]
    public void CreateContactRequest_WithBoundaryLengthValues_PassesValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = new string('A', 50), // Exactly 50 characters
            LastName = new string('B', 50),  // Exactly 50 characters
            Email = new string('c', 243) + "@example.com", // Exactly 255 characters
            Phone = new string('1', 20) // Exactly 20 characters
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void SearchContactRequest_WithBoundaryLength_PassesValidation()
    {
        SearchContactRequest request = new()
        {
            Query = new string('A', 100)
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void CreateContactRequest_WithSpecialCharacters_PassesValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "José-María",
            LastName = "O'Connor-Smith",
            Email = "jose.maria@example-company.co.uk",
            Phone = "+1 (555) 123-4567 ext. 890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    [Test]
    public void CreateContactRequest_WithUnicodeCharacters_PassesValidation()
    {
        CreateContactRequest request = new()
        {
            FirstName = "张",
            LastName = "伟",
            Email = "zhang.wei@example.com",
            Phone = "123-456-7890"
        };

        (bool isValid, List<string>? errors) = ValidationHelper.ValidateModel(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isValid, Is.True);
            Assert.That(errors, Is.Empty);
        }
    }

    #endregion
}
