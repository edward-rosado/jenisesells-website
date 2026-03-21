using System.ComponentModel.DataAnnotations;

namespace RealEstateStar.Api.Features.Leads.RequestDeletion;

public class RequestDeletionRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }
}
