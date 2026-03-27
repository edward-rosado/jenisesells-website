using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Leads.Interfaces;

public interface ILeadScorer
{
    LeadScore Score(Lead lead);
}
