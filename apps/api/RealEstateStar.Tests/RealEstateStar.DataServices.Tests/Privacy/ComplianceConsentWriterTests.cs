using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Markdown;
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
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Privacy;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class ComplianceConsentWriterTests
{
    private readonly Mock<IComplianceFileStorageProvider> _storageProvider = new();

    [Fact]
    public async Task WriteAsync_AppendsCSVRowToCompliancePath()
    {
        var sut = new ComplianceConsentDataService(_storageProvider.Object, NullLogger<ComplianceConsentDataService>.Instance);
        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            OptedIn = true,
            ConsentText = "Consented",
            Channels = ["email", "calls"],
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        await sut.WriteAsync("agent-1", consent, "hmac-sig-123", CancellationToken.None);

        _storageProvider.Verify(sp => sp.AppendRowAsync(
            It.Is<string>(path => path.Contains("compliance/agent-1/consent-log")),
            It.Is<List<string>>(row => row.Count >= 12 && row[row.Count - 1] == "hmac-sig-123"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WriteAsync_DoesNotThrowOnStorageFailure()
    {
        _storageProvider.Setup(sp => sp.AppendRowAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Drive unavailable"));

        var sut = new ComplianceConsentDataService(_storageProvider.Object, NullLogger<ComplianceConsentDataService>.Instance);
        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(),
            Email = "x@y.com",
            FirstName = "A",
            LastName = "B",
            OptedIn = true,
            ConsentText = "C",
            Channels = ["email"],
            IpAddress = "0",
            UserAgent = "U",
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        // Should not throw — compliance writes are non-blocking
        await sut.WriteAsync("agent-1", consent, "sig", CancellationToken.None);
    }
}
