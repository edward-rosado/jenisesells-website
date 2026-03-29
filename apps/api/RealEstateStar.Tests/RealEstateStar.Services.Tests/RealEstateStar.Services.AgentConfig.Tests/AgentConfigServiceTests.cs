using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Services.AgentConfig.Tests;

public class AgentConfigServiceTests
{
    private readonly Mock<IAccountConfigService> _accountConfig = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly AgentConfigService _sut;
    private static readonly CancellationToken Ct = CancellationToken.None;

    public AgentConfigServiceTests()
    {
        _sut = new AgentConfigService(
            _accountConfig.Object,
            _storage.Object,
            NullLogger<AgentConfigService>.Instance);
    }

    private static ActivationOutputs MakeOutputs(
        string? agentName = "Jane Smith",
        string? email = "jane@example.com",
        string? phone = "(555) 123-4567",
        string? state = "NJ",
        string? tagline = "Forward. Moving.",
        BrandingKit? brandingKit = null)
    {
        return new ActivationOutputs
        {
            AgentName = agentName,
            AgentEmail = email,
            AgentPhone = phone,
            AgentTitle = "REALTOR\u00ae",
            AgentLicenseNumber = "NJ-123",
            AgentTagline = tagline,
            State = state,
            ServiceAreas = ["Middlesex County"],
            Languages = ["English", "Spanish"],
            BrandingKit = brandingKit,
        };
    }

    // ── Single agent ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_SingleAgent_NewConfig_WritesAccountJsonAndContentJson()
    {
        const string handle = "jane-smith";
        var outputs = MakeOutputs();

        _accountConfig.Setup(a => a.GetAccountAsync(handle, Ct))
            .ReturnsAsync((AccountConfig?)null);

        _storage.Setup(s => s.EnsureFolderExistsAsync($"config/accounts/{handle}", Ct))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        _storage.Setup(s => s.ReadDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.ContentJsonFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(handle, handle, handle, outputs, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct), Times.Once);
        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_SingleAgent_ExistingConfig_SkipsAccountJson()
    {
        const string handle = "jane-smith";
        var outputs = MakeOutputs();

        _accountConfig.Setup(a => a.GetAccountAsync(handle, Ct))
            .ReturnsAsync(new AccountConfig { Handle = handle });

        // content.json also exists
        _storage.Setup(s => s.ReadDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.ContentJsonFile, Ct))
            .ReturnsAsync("existing content");

        await _sut.GenerateAsync(handle, handle, handle, outputs, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct), Times.Never);
        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{handle}", AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct), Times.Never);
    }

    // ── Brokerage ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_Brokerage_FirstAgent_BootstrapsBrokerageAndWritesAgentConfig()
    {
        const string accountId = "test-brokerage";
        const string agentId = "agent-a";
        var outputs = MakeOutputs();

        // No existing brokerage account
        _accountConfig.Setup(a => a.GetAccountAsync(accountId, Ct))
            .ReturnsAsync((AccountConfig?)null);

        // Brokerage account.json write
        _storage.Setup(s => s.EnsureFolderExistsAsync($"config/accounts/{accountId}", Ct))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.WriteDocumentAsync(
            $"config/accounts/{accountId}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Brokerage content.json write
        _storage.Setup(s => s.WriteDocumentAsync(
            $"config/accounts/{accountId}", AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Agent config.json
        var agentFolder = $"config/accounts/{accountId}/{AgentConfigService.AgentsSubfolder}/{agentId}";
        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentConfigService.AgentConfigJsonFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.AgentConfigJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        // Agent content.json
        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentConfigService.ContentJsonFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(accountId, agentId, agentId, outputs, Ct);

        // Brokerage account.json bootstrapped
        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{accountId}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct), Times.Once);

        // Agent config written
        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.AgentConfigJsonFile, It.IsAny<string>(), Ct), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_Brokerage_SubsequentAgent_SkipsBrokerageBootstrap()
    {
        const string accountId = "test-brokerage";
        const string agentId = "agent-b";
        var outputs = MakeOutputs();

        // Existing brokerage account
        _accountConfig.Setup(a => a.GetAccountAsync(accountId, Ct))
            .ReturnsAsync(new AccountConfig { Handle = accountId });

        var agentFolder = $"config/accounts/{accountId}/{AgentConfigService.AgentsSubfolder}/{agentId}";
        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentConfigService.AgentConfigJsonFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.AgentConfigJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        _storage.Setup(s => s.ReadDocumentAsync(agentFolder, AgentConfigService.ContentJsonFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.ContentJsonFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.GenerateAsync(accountId, agentId, agentId, outputs, Ct);

        // No brokerage account.json write
        _storage.Verify(s => s.WriteDocumentAsync(
            $"config/accounts/{accountId}", AgentConfigService.AccountJsonFile, It.IsAny<string>(), Ct), Times.Never);
        // Agent config written
        _storage.Verify(s => s.WriteDocumentAsync(
            agentFolder, AgentConfigService.AgentConfigJsonFile, It.IsAny<string>(), Ct), Times.Once);
    }

    // ── JSON builders ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildSingleAgentAccountJson_ContainsRequiredFields()
    {
        const string handle = "jane-smith";
        var outputs = MakeOutputs();

        var json = AgentConfigService.BuildSingleAgentAccountJson(handle, outputs);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(handle, doc.RootElement.GetProperty("handle").GetString());
        Assert.Equal(handle, doc.RootElement.GetProperty("accountId").GetString());
        Assert.Equal("Jane Smith", doc.RootElement.GetProperty("agent").GetProperty("name").GetString());
        Assert.Equal("jane@example.com", doc.RootElement.GetProperty("agent").GetProperty("email").GetString());
        Assert.Equal("(555) 123-4567", doc.RootElement.GetProperty("agent").GetProperty("phone").GetString());
        Assert.Equal("NJ", doc.RootElement.GetProperty("location").GetProperty("state").GetString());
        Assert.Equal("gmail", doc.RootElement.GetProperty("integrations").GetProperty("email_provider").GetString());
    }

    [Fact]
    public void BuildSingleAgentAccountJson_WithBrandingKit_UsesKitColors()
    {
        const string handle = "jane-smith";
        var branding = new BrandingKit(
            Colors: [new ColorEntry("primary", "#123456", "Logo", "Background")],
            Fonts: [new FontEntry("body", "Georgia", "400", "CSS")],
            Logos: [],
            RecommendedTemplate: "urban-loft",
            TemplateReason: null);
        var outputs = MakeOutputs(brandingKit: branding);

        var json = AgentConfigService.BuildSingleAgentAccountJson(handle, outputs);
        var doc = JsonDocument.Parse(json);

        Assert.Equal("#123456", doc.RootElement.GetProperty("branding").GetProperty("primary_color").GetString());
        Assert.Equal("urban-loft", doc.RootElement.GetProperty("template").GetString());
    }

    [Fact]
    public void BuildContentJson_ContainsRequiredSections()
    {
        const string handle = "jane-smith";
        var outputs = MakeOutputs();

        var json = AgentConfigService.BuildContentJson(handle, outputs);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("navigation", out _));
        Assert.True(doc.RootElement.TryGetProperty("pages", out _));
        Assert.True(doc.RootElement.GetProperty("pages").TryGetProperty("home", out _));
        Assert.True(doc.RootElement.GetProperty("pages").TryGetProperty("thank_you", out _));

        var sections = doc.RootElement
            .GetProperty("pages").GetProperty("home").GetProperty("sections");
        Assert.True(sections.TryGetProperty("hero", out _));
        Assert.True(sections.TryGetProperty("contact_form", out _));
    }

    [Fact]
    public void BuildAgentConfigJson_ContainsIdAndName()
    {
        const string agentId = "agent-a";
        var outputs = MakeOutputs(agentName: "James Whitfield", email: "james@brokerage.com");

        var json = AgentConfigService.BuildAgentConfigJson(agentId, outputs);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(agentId, doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("James Whitfield", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("james@brokerage.com", doc.RootElement.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("NJ", "NJ-REALTORS-118", "NJ Real Estate Commission")]
    [InlineData("NY", "NY-DOS-1736", "NY Department of State")]
    [InlineData("CA", "CAR-RPA-CA", "CA Department of Real Estate")]
    [InlineData("TX", "TREC-1-4", "TX Real Estate Commission")]
    [InlineData("WA", "WA-STANDARD", "WA Real Estate Commission")]
    public void GetStateComplianceDefaults_ReturnsCorrectValues(
        string state, string expectedForm, string expectedBody)
    {
        var (form, body) = AgentConfigService.GetStateComplianceDefaults(state);

        Assert.Equal(expectedForm, form);
        Assert.Equal(expectedBody, body);
    }

    [Fact]
    public void BuildSingleAgentAccountJson_IsValidJson()
    {
        var outputs = MakeOutputs();
        var json = AgentConfigService.BuildSingleAgentAccountJson("test-handle", outputs);

        // Should not throw
        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void BuildContentJson_IsValidJson()
    {
        var outputs = MakeOutputs();
        var json = AgentConfigService.BuildContentJson("test-handle", outputs);

        var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void BuildBrokerageAccountJson_ContainsHandleAndAccountId()
    {
        const string accountId = "test-brokerage";
        var outputs = MakeOutputs(agentName: "Sterling Realty Group");

        var json = AgentConfigService.BuildBrokerageAccountJson(accountId, outputs);
        var doc = JsonDocument.Parse(json);

        Assert.Equal(accountId, doc.RootElement.GetProperty("handle").GetString());
        Assert.Equal(accountId, doc.RootElement.GetProperty("accountId").GetString());
    }
}
