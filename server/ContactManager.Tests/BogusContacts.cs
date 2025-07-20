namespace ContactManager.Tests;

using Bogus;
using Models.Data;

public static class BogusContacts
{
    /// <summary>
    /// Generates a list of random contacts using Bogus.
    /// </summary>
    /// <param name="count">An optional parameter specifying how many contacts to generate.</param>
    /// <returns>A list of n randomized contacts, where n is either 100 or the value passed in.</returns>
    public static List<Contact> GetContacts(int count = 100)
    {
        Faker<Contact> faker = new Faker<Contact>()
            .RuleFor(c => c.Id, f => f.IndexFaker)
            .RuleFor(c => c.FirstName, f => f.Name.FirstName())
            .RuleFor(c => c.LastName, f => f.Name.LastName())
            .RuleFor(c => c.Email, (f, c) => $"{c.FirstName.ToLower()}.{c.LastName.ToLower()}@{f.Internet.DomainName()}.{f.Internet.DomainSuffix()}")
            .RuleFor(c => c.Phone, f => f.Phone.PhoneNumber("(###) ###-####"));
        
        return faker.Generate(count);
    }
}