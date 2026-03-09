using System.ComponentModel.DataAnnotations;

namespace RealEstateStar.Api.Models;

public class Lead
{
    [Required]
    [StringLength(100)]
    public required string FirstName { get; init; }

    [Required]
    [StringLength(100)]
    public required string LastName { get; init; }

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; init; }

    [Phone]
    [StringLength(30)]
    public required string Phone { get; init; }

    [Required]
    [StringLength(300)]
    public required string Address { get; init; }

    [Required]
    [StringLength(100)]
    public required string City { get; init; }

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public required string State { get; init; }

    [Required]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Zip must be a valid US zip code (e.g. 07081 or 07081-1234).")]
    public required string Zip { get; init; }

    [Required]
    [StringLength(50)]
    public required string Timeline { get; init; }

    public int? Beds { get; init; }
    public int? Baths { get; init; }
    public int? Sqft { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

    public string FullName => $"{FirstName} {LastName}";
    public string FullAddress => $"{Address}, {City}, {State} {Zip}";
}
