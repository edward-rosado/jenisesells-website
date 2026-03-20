using System.ComponentModel.DataAnnotations;

namespace RealEstateStar.Api.Features.Leads.DeleteData;

public class DeleteLeadDataRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required]
    public required string Token { get; init; }

    [Required]
    public required string Reason { get; init; }
}
