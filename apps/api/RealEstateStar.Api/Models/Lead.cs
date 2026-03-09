namespace RealEstateStar.Api.Models;

public class Lead
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public required string Timeline { get; init; }
    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }
    public string? Notes { get; init; }

    public string FullName => $"{FirstName} {LastName}";
    public string FullAddress => $"{Address}, {City}, {State} {Zip}";
}
