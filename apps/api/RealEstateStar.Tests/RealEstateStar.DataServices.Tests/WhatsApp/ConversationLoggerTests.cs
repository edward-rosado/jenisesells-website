using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Tests.WhatsApp;

public class ConversationLoggerTests
{
    private readonly Mock<IFileStorageProvider> _storage = new();
    private readonly Mock<ILogger<ConversationLogger>> _logger = new();
    private readonly ConversationLogger _sut;

    // Folder and file name that the implementation should derive
    private const string LeadName = "Jane Doe";
    private const string FileName = "conversation.md";

    public ConversationLoggerTests()
    {
        _sut = new ConversationLogger(_storage.Object, _logger.Object);
    }

    // Helper: a single inbound message from a lead
    private static List<(DateTime timestamp, string sender, string body, string? templateName)>
        OneMessage(DateTime ts, string sender = "Jane Doe", string body = "Hi there",
            string? templateName = null) =>
        [(ts, sender, body, templateName)];

    [Fact]
    public async Task LogMessagesAsync_AppendsMessagePairToLeadFolder()
    {
        var ts = new DateTime(2026, 3, 19, 14, 15, 0);

        // Simulate file already existing so header is NOT prepended
        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("# existing content");

        await _sut.LogMessagesAsync("agent-1", LeadName, OneMessage(ts), CancellationToken.None);

        // WriteDocumentAsync must be called with the lead folder path
        _storage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(folder => folder.Contains("Jane Doe")),
            FileName,
            It.Is<string>(content => content.Contains("Jane Doe") && content.Contains("Hi there")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogMessagesAsync_CreatesHeaderOnFirstWrite()
    {
        var ts = new DateTime(2026, 3, 19, 10, 0, 0);

        // No existing file
        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.LogMessagesAsync("agent-1", LeadName, OneMessage(ts), CancellationToken.None);

        _storage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(),
            FileName,
            It.Is<string>(content =>
                content.Contains("# WhatsApp Conversation") &&
                content.Contains("**Score:** 0/100")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogMessagesAsync_RoutesNonLeadToGeneralConversation()
    {
        var ts = new DateTime(2026, 3, 19, 9, 0, 0);

        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // leadName is null → general conversation
        await _sut.LogMessagesAsync("agent-1", null, OneMessage(ts), CancellationToken.None);

        _storage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(folder => folder.Contains("WhatsApp") || folder.Contains("General")),
            FileName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Must NOT use a lead-specific folder
        _storage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(folder => folder.Contains("Jane Doe")),
            FileName,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogMessagesAsync_InsertsDateHeader_WhenDateChanges()
    {
        var day1 = new DateTime(2026, 3, 18, 10, 0, 0);
        var day2 = new DateTime(2026, 3, 19, 10, 0, 0);

        var messages = new List<(DateTime, string, string, string?)>
        {
            (day1, "Jane Doe", "First day message", null),
            (day2, "Jane Doe", "Second day message", null)
        };

        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await _sut.LogMessagesAsync("agent-1", LeadName, messages, CancellationToken.None);

        _storage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(),
            FileName,
            It.Is<string>(content =>
                content.Contains("### Mar 18, 2026") &&
                content.Contains("### Mar 19, 2026")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogMessagesAsync_DriveWriteFailure_LogsWarning_DoesNotThrow()
    {
        var ts = new DateTime(2026, 3, 19, 8, 0, 0);

        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _storage.Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Drive quota exceeded"));

        var act = () => _sut.LogMessagesAsync("agent-1", LeadName, OneMessage(ts), CancellationToken.None);

        await act.Should().NotThrowAsync();

        _logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WA-011]")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task LogMessagesAsync_RendersSystemEvents()
    {
        var ts = new DateTime(2026, 3, 19, 11, 0, 0);
        var messages = OneMessage(ts, "Real Estate Star", "Welcome to our service!",
            "new_lead_notification");

        _storage.Setup(s => s.ReadDocumentAsync(
                It.IsAny<string>(), FileName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("# existing");

        await _sut.LogMessagesAsync("agent-1", LeadName, messages, CancellationToken.None);

        _storage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(),
            FileName,
            It.Is<string>(content =>
                content.Contains("template: new_lead_notification") &&
                content.Contains("Real Estate Star")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
