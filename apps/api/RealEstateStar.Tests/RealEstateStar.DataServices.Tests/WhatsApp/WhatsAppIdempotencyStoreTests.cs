using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using Microsoft.Extensions.Caching.Memory;

namespace RealEstateStar.DataServices.Tests.WhatsApp;

public class WhatsAppIdempotencyStoreTests
{
    private readonly WhatsAppIdempotencyStore _store;

    public WhatsAppIdempotencyStoreTests()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        _store = new WhatsAppIdempotencyStore(cache);
    }

    [Fact]
    public void IsProcessed_ReturnsFalse_ForNewMessageId()
    {
        _store.IsProcessed("wamid.new").Should().BeFalse();
    }

    [Fact]
    public void MarkProcessed_ThenIsProcessed_ReturnsTrue()
    {
        _store.MarkProcessed("wamid.abc");
        _store.IsProcessed("wamid.abc").Should().BeTrue();
    }

    [Fact]
    public void MarkProcessed_SameId_Twice_DoesNotThrow()
    {
        _store.MarkProcessed("wamid.abc");
        var act = () => _store.MarkProcessed("wamid.abc");
        act.Should().NotThrow();
    }
}
