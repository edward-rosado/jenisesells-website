using RealEstateStar.Api.Features.Cma.Submit;
using RealEstateStar.Api.Features.Cma.ListLeads;

namespace RealEstateStar.Api.Features.Cma;

public static class CmaMappers
{
    public static Lead ToLead(this SubmitCmaRequest request) => new()
    {
        FirstName = request.FirstName,
        LastName = request.LastName,
        Email = request.Email,
        Phone = request.Phone,
        Address = request.Address,
        City = request.City,
        State = request.State,
        Zip = request.Zip,
        Timeline = request.Timeline,
        Beds = request.Beds,
        Baths = request.Baths,
        Sqft = request.Sqft,
        Notes = request.Notes
    };

    public static ListLeadsResponse ToListLeadsResponse(this CmaJob job) => new()
    {
        Id = job.Id.ToString(),
        Name = job.Lead.FullName,
        Address = job.Lead.FullAddress,
        Timeline = job.Lead.Timeline,
        CmaStatus = job.Status,
        SubmittedAt = job.CreatedAt,
        DriveLink = job.DriveLink
    };
}
