using Xunit;
using Moq;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.Senders;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Submit;
using RealEstateStar.Api.Features.Onboarding.Services;
using RealEstateStar.Api.Features.Onboarding.Tools;
using RealEstateStar.TestUtilities;
using RealEstateStar.Workers.Shared;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Notifications.WhatsApp;
using FluentAssertions;

namespace RealEstateStar.Api.Tests.Health;

public class BackgroundServiceHealthTrackerTests
{
    [Fact]
    public void GetLastActivity_ReturnsNull_WhenWorkerNeverRecorded()
    {
        var tracker = new BackgroundServiceHealthTracker();

        tracker.GetLastActivity("UnknownWorker").Should().BeNull();
    }

    [Fact]
    public void RecordActivity_SetsTimestamp()
    {
        var tracker = new BackgroundServiceHealthTracker();
        var before = DateTime.UtcNow;

        tracker.RecordActivity("TestWorker");

        var lastActivity = tracker.GetLastActivity("TestWorker");
        lastActivity.Should().NotBeNull();
        lastActivity!.Value.Should().BeOnOrAfter(before);
        lastActivity.Value.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void RecordActivity_UpdatesTimestamp_OnSubsequentCalls()
    {
        var tracker = new BackgroundServiceHealthTracker();

        tracker.RecordActivity("TestWorker");
        var first = tracker.GetLastActivity("TestWorker");

        // Small delay to ensure timestamps differ
        Thread.Sleep(1);
        tracker.RecordActivity("TestWorker");
        var second = tracker.GetLastActivity("TestWorker");

        second.Should().BeOnOrAfter(first!.Value);
    }

    [Fact]
    public void TracksMultipleWorkers_Independently()
    {
        var tracker = new BackgroundServiceHealthTracker();

        tracker.RecordActivity("WorkerA");
        Thread.Sleep(1);
        tracker.RecordActivity("WorkerB");

        var activityA = tracker.GetLastActivity("WorkerA");
        var activityB = tracker.GetLastActivity("WorkerB");

        activityA.Should().NotBeNull();
        activityB.Should().NotBeNull();
        activityB!.Value.Should().BeOnOrAfter(activityA!.Value);
    }
}
