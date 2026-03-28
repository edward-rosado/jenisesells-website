// Domain type imports — added during Phase 3 (Domain extraction)
// These replace the implicit same-namespace access that existed before the restructure.

// Shared
global using RealEstateStar.Domain.Shared.Models;
global using RealEstateStar.Domain.Shared.Interfaces.Storage;
global using RealEstateStar.Domain.Shared.Interfaces.Senders;
global using RealEstateStar.Domain.Shared.Interfaces.External;

// Leads
global using RealEstateStar.Domain.Leads.Models;
global using RealEstateStar.Domain.Leads.Interfaces;
global using RealEstateStar.Domain.Leads;

// CMA
global using RealEstateStar.Domain.Cma.Interfaces;
global using RealEstateStar.Domain.Cma;

// HomeSearch
global using RealEstateStar.Domain.HomeSearch;

// Privacy
global using RealEstateStar.Domain.Privacy.Interfaces;

// WhatsApp
global using RealEstateStar.Domain.WhatsApp.Interfaces;
global using RealEstateStar.Domain.WhatsApp;

// Onboarding
global using RealEstateStar.Domain.Onboarding.Models;
global using RealEstateStar.Domain.Onboarding.Interfaces;
global using RealEstateStar.Domain.Onboarding.Services;
global using RealEstateStar.Domain.Onboarding;

// Notifications
global using RealEstateStar.Notifications.WhatsApp;

// Orchestration
global using RealEstateStar.Domain.Orchestration;

// Workers
global using RealEstateStar.Workers.Shared;
global using RealEstateStar.Workers.Lead.CMA;
global using RealEstateStar.Workers.Lead.HomeSearch;
global using RealEstateStar.Workers.WhatsApp;

