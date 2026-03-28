// Domain type imports — added during Phase 3 (Domain extraction)
global using RealEstateStar.Domain.Shared.Models;
global using RealEstateStar.Domain.Shared.Interfaces.Storage;
global using RealEstateStar.Domain.Shared.Interfaces.Senders;
global using RealEstateStar.Domain.Leads.Models;
global using RealEstateStar.Domain.Leads.Interfaces;
global using RealEstateStar.Domain.Leads;
global using RealEstateStar.Domain.Cma.Models;
global using RealEstateStar.Domain.Cma.Interfaces;
global using RealEstateStar.Domain.HomeSearch.Interfaces;
global using RealEstateStar.Domain.Privacy.Interfaces;
global using RealEstateStar.Domain.WhatsApp.Interfaces;
global using RealEstateStar.Domain.Onboarding.Models;
global using RealEstateStar.Domain.Onboarding.Interfaces;
global using RealEstateStar.Domain.Onboarding.Services;

// DataServices
global using RealEstateStar.DataServices.Privacy;
global using RealEstateStar.DataServices.WhatsApp;

// Api features (types that remain in Api)
global using RealEstateStar.Api.Features.Leads;
global using RealEstateStar.Api.Features.Leads.Submit;
global using RealEstateStar.Api.Features.Onboarding.Services;
global using RealEstateStar.Api.Features.Onboarding.Tools;
global using RealEstateStar.TestUtilities;

// Workers
global using RealEstateStar.Workers.Shared;
global using RealEstateStar.Workers.Lead.CMA;
global using RealEstateStar.Workers.Lead.HomeSearch;

// Notifications
global using RealEstateStar.Notifications.WhatsApp;
