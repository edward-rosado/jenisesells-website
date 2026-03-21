// Domain type imports — Notifications depends only on Domain.

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
global using RealEstateStar.Domain.Cma.Models;
global using RealEstateStar.Domain.Cma.Interfaces;
global using RealEstateStar.Domain.Cma;

// HomeSearch
global using RealEstateStar.Domain.HomeSearch.Interfaces;
global using RealEstateStar.Domain.HomeSearch.Markdown;
global using RealEstateStar.Domain.HomeSearch;

// WhatsApp
global using RealEstateStar.Domain.WhatsApp.Models;
global using RealEstateStar.Domain.WhatsApp.Interfaces;
global using RealEstateStar.Domain.WhatsApp;
