using System.ComponentModel.DataAnnotations;

namespace RealEstateStar.Api.Features.Leads.OptOut;

public class OptOutRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required, StringLength(128)]
    public required string Token { get; init; }
}
