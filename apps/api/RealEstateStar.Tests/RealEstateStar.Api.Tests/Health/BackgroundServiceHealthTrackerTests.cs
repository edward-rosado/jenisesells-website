using RealEstateStar.Workers.Shared;
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
