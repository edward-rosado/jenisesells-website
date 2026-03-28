global using Xunit;
global using Moq;
global using FluentAssertions;

// Domain namespaces
global using RealEstateStar.Domain.Shared.Models;
global using RealEstateStar.Domain.Shared.Interfaces.Storage;
global using RealEstateStar.Domain.Shared.Interfaces.External;
global using RealEstateStar.Domain.Leads.Models;
global using RealEstateStar.Domain.Leads.Interfaces;
global using RealEstateStar.Domain.Leads.Markdown;
global using RealEstateStar.Domain.Leads;
global using RealEstateStar.Domain.Privacy.Interfaces;
global using RealEstateStar.Domain.WhatsApp.Interfaces;
global using RealEstateStar.Domain.Onboarding.Models;
global using RealEstateStar.Domain.Onboarding.Interfaces;

// DataServices namespaces
global using RealEstateStar.DataServices.Config;
global using RealEstateStar.DataServices.Leads;
global using RealEstateStar.DataServices.Onboarding;
global using RealEstateStar.DataServices.Privacy;
global using RealEstateStar.DataServices.Storage;
global using RealEstateStar.DataServices.WhatsApp;
