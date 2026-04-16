using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Functions.Activation.Activities;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Unit tests for BrokerageJoinFunction.
/// All tests mock IAccountConfigService; no file I/O or real CAS infrastructure is used.
/// </summary>
public sealed class BrokerageJoinFunctionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static BrokerageJoinInput MakeInput(
        string accountId = "acct-1",
        string agentId = "agent-42",
        string agentName = "Jane Smith",
        string correlationId = "corr-abc") =>
        new()
        {
            AccountId = accountId,
            AgentId = agentId,
            AgentName = agentName,
            CorrelationId = correlationId,
        };

    private static AccountConfig MakeAccount(
        string handle = "acct-1",
        List<AgentMember>? members = null,
        string etag = "etag-1") =>
        new()
        {
            Handle = handle,
            AgentMembers = members,
            ETag = etag,
        };

    // ─── Successful join on first attempt ────────────────────────────────────

    [Fact]
    public async Task RunAsync_SuccessOnFirstAttempt_CallsSaveIfUnchangedOnce()
    {
        // Arrange
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var account = MakeAccount(members: null, etag: "etag-first");

        // GetAccountAsync: called once for idempotency check, once inside CAS loop.
        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);
        service.Setup(s => s.SaveIfUnchangedAsync(
                It.Is<AccountConfig>(a => a.AgentMembers!.Any(m => m.AgentId == "agent-42")),
                "etag-first",
                Ct))
            .ReturnsAsync(true);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act
        await fn.RunAsync(MakeInput(), Ct);

        // Assert — SaveIfUnchangedAsync called exactly once (first CAS attempt succeeded).
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SuccessOnFirstAttempt_AgentMemberHasCorrectFields()
    {
        // Arrange
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var account = MakeAccount(members: [], etag: "etag-x");
        AccountConfig? capturedUpdate = null;

        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);
        service.Setup(s => s.SaveIfUnchangedAsync(It.IsAny<AccountConfig>(), "etag-x", Ct))
            .Callback<AccountConfig, string, CancellationToken>((a, _, _) => capturedUpdate = a)
            .ReturnsAsync(true);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act
        await fn.RunAsync(MakeInput(agentId: "agent-99", agentName: "Bob Jones"), Ct);

        // Assert — member fields set correctly.
        capturedUpdate.Should().NotBeNull();
        capturedUpdate!.AgentMembers.Should().HaveCount(1);
        capturedUpdate.AgentMembers![0].AgentId.Should().Be("agent-99");
        capturedUpdate.AgentMembers![0].AgentName.Should().Be("Bob Jones");
        capturedUpdate.AgentMembers![0].JoinedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ─── Idempotent: agent already a member ──────────────────────────────────

    [Fact]
    public async Task RunAsync_AgentAlreadyMember_NoopNoCasAttempted()
    {
        // Arrange — account already has the agent.
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var existingMember = new AgentMember { AgentId = "agent-42", AgentName = "Jane Smith" };
        var account = MakeAccount(members: [existingMember]);

        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act — should complete without error and without calling SaveIfUnchangedAsync.
        await fn.RunAsync(MakeInput(), Ct);

        // Assert — SaveIfUnchangedAsync was NEVER called (idempotent no-op).
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Never);
    }

    [Fact]
    public async Task RunAsync_AgentAlreadyMemberOnCasReread_NoopReturnsSucceeded()
    {
        // Arrange — first read returns empty list (passes idempotency guard), but CAS re-read
        // returns the agent already present (simulates concurrent join winning the race).
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);

        var emptyAccount = MakeAccount(members: [], etag: "etag-empty");
        var memberAccount = MakeAccount(
            members: [new AgentMember { AgentId = "agent-42", AgentName = "Jane Smith" }],
            etag: "etag-with-member");

        var callCount = 0;
        service.Setup(s => s.GetAccountAsync("acct-1", Ct))
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call: empty (pre-CAS idempotency check).
                // Second call: agent already there (concurrent winner detected during CAS loop).
                return callCount == 1 ? emptyAccount : memberAccount;
            });

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act — should succeed without SaveIfUnchangedAsync (idempotent inside CAS loop).
        await fn.RunAsync(MakeInput(), Ct);

        // Assert — SaveIfUnchangedAsync never called; CAS loop detected concurrent join.
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Never);
    }

    // ─── Success after CAS conflicts ─────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SuccessAfterTwoCasConflicts_CallsSaveThreeTimes()
    {
        // Arrange — SaveIfUnchangedAsync returns false twice (ETag mismatch), then true.
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var account = MakeAccount(members: null, etag: "etag-v1");

        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);

        var saveCallCount = 0;
        service.Setup(s => s.SaveIfUnchangedAsync(It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct))
            .ReturnsAsync(() =>
            {
                saveCallCount++;
                return saveCallCount >= 3; // Succeed on the 3rd attempt.
            });

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act
        await fn.RunAsync(MakeInput(), Ct);

        // Assert — SaveIfUnchangedAsync called 3 times total.
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Exactly(3));
    }

    // ─── CAS exhaustion after 5 attempts ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_CasExhaustedAfterFiveAttempts_ThrowsInvalidOperationException()
    {
        // Arrange — SaveIfUnchangedAsync always returns false (permanent ETag conflict).
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var account = MakeAccount(members: null, etag: "etag-stale");

        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);
        service.Setup(s => s.SaveIfUnchangedAsync(It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct))
            .ReturnsAsync(false);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act & Assert — must throw so Durable Functions can retry the activity.
        var act = async () => await fn.RunAsync(MakeInput(), Ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ACCOUNT-MERGE-020*");
    }

    [Fact]
    public async Task RunAsync_CasExhaustedAfterFiveAttempts_SaveCalledFiveTimes()
    {
        // Arrange
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        var account = MakeAccount(members: null, etag: "etag-stale");

        service.Setup(s => s.GetAccountAsync("acct-1", Ct)).ReturnsAsync(account);
        service.Setup(s => s.SaveIfUnchangedAsync(It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct))
            .ReturnsAsync(false);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act — swallow exception; we just want to count Save calls.
        try { await fn.RunAsync(MakeInput(), Ct); }
        catch (InvalidOperationException) { /* expected */ }

        // Assert — exactly 5 save attempts before exhaustion.
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Exactly(5));
    }

    // ─── Account not found ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_AccountNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — GetAccountAsync returns null (account does not exist).
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        service.Setup(s => s.GetAccountAsync("acct-missing", Ct))
            .ReturnsAsync((AccountConfig?)null);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act & Assert — must throw; account must exist for a brokerage join.
        var act = async () => await fn.RunAsync(MakeInput(accountId: "acct-missing"), Ct);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*ACCOUNT-MERGE-002*");
    }

    [Fact]
    public async Task RunAsync_AccountNotFound_DoesNotCallSaveIfUnchanged()
    {
        // Arrange
        var service = new Mock<IAccountConfigService>(MockBehavior.Strict);
        service.Setup(s => s.GetAccountAsync(It.IsAny<string>(), Ct))
            .ReturnsAsync((AccountConfig?)null);

        var fn = new BrokerageJoinFunction(service.Object, NullLogger<BrokerageJoinFunction>.Instance);

        // Act — swallow expected exception.
        try { await fn.RunAsync(MakeInput(accountId: "ghost"), Ct); }
        catch (InvalidOperationException) { /* expected */ }

        // Assert — no save attempted for a missing account.
        service.Verify(s => s.SaveIfUnchangedAsync(
            It.IsAny<AccountConfig>(), It.IsAny<string>(), Ct), Times.Never);
    }

    // ─── ActivityNames constant ───────────────────────────────────────────────

    [Fact]
    public void ActivityNames_BrokerageJoin_IsDefinedAndStartsWithActivation()
    {
        RealEstateStar.Functions.Activation.ActivityNames.BrokerageJoin
            .Should().NotBeNullOrWhiteSpace()
            .And.StartWith("Activation");
    }
}
