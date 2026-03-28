using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.WhatsApp.Models;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Notifications.WhatsApp;
// TODO: Pipeline redesign — LeadChatCardRendererTests stubbed in Phase 1.5 pending Phase 2/3/4 redesign.
// LeadChatCardRenderer removed. Tests will be rewritten with new implementation.

namespace RealEstateStar.Notifications.Tests.Leads;
