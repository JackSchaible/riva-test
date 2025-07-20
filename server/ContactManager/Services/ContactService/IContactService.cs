namespace ContactManager.Services;

using Models.Data;

public interface IContactService
{
    Task<List<Contact>> GetAllAsync();
    Task<List<Contact>> SearchAsync(string query);
    Task<Contact> CreateAsync(Contact contact);
    Task<Contact?> UpdateAsync(int id, Contact contact);
    Task<bool> DeleteAsync(int id);
}