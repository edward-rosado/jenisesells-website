global using Xunit;
global using Moq;
global using FluentAssertions;

// Domain namespaces
global using RealEstateStar.Domain.Shared.Models;
global using RealEstateStar.Domain.Shared.Interfaces.Storage;
global using RealEstateStar.Domain.Shared.Interfaces.Senders;
global using RealEstateStar.Domain.Shared.Interfaces.External;
global using RealEstateStar.Domain.Leads.Models;
global using RealEstateStar.Domain.Leads.Interfaces;
global using RealEstateStar.Domain.WhatsApp.Models;
global using RealEstateStar.Domain.WhatsApp.Interfaces;
global using RealEstateStar.Domain.Cma.Models;
global using RealEstateStar.Domain.Cma.Interfaces;
global using RealEstateStar.Domain.HomeSearch;
global using RealEstateStar.Domain.HomeSearch.Interfaces;
global using RealEstateStar.Domain.Leads;

// DataServices namespaces (for test data construction)
global using RealEstateStar.DataServices.Config;
global using RealEstateStar.DataServices.Leads;
global using RealEstateStar.DataServices.Privacy;
global using RealEstateStar.DataServices.WhatsApp;

// Notifications namespaces
global using RealEstateStar.Notifications.Leads;
global using RealEstateStar.Notifications.WhatsApp;
