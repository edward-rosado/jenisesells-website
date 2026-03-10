using RealEstateStar.Api.Features.Cma;

namespace RealEstateStar.Api.Tests.TestHelpers;

public static class TestData
{
    public static Lead MakeLead(
        string firstName = "John",
        string lastName = "Doe",
        string email = "john@example.com",
        string phone = "555-1234",
        string address = "123 Main St",
        string city = "Springfield",
        string state = "NJ",
        string zip = "07081",
        string timeline = "3-6 months",
        int? beds = null,
        int? baths = null,
        int? sqft = null) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        Phone = phone,
        Address = address,
        City = city,
        State = state,
        Zip = zip,
        Timeline = timeline,
        Beds = beds,
        Baths = baths,
        Sqft = sqft
    };
}
