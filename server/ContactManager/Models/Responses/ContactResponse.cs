namespace ContactManager.Models.Data.Responses;

public class ContactResponse
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }

    public static ContactResponse FromContact(Contact contact)
    {
        return new ContactResponse
        {
            Id = contact.Id,
            FirstName = contact.FirstName,
            LastName = contact.LastName,
            Email = contact.Email,
            Phone = contact.Phone
        };
    }

    public static List<ContactResponse> FromContacts(List<Contact> contacts)
    {
        return contacts.Select(FromContact).ToList();
    }
}
