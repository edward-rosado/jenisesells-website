using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace RealEstateStar.Clients.Azure;

public static class QueueDiagnostics
{
    public const string ServiceName = "RealEstateStar.Queue";
    public static readonly ActivitySource ActivitySource = new(ServiceName, "1.0.0");
    private static readonly Meter Meter = new(ServiceName, "1.0.0");

    public static readonly Counter<long> Enqueued = Meter.CreateCounter<long>(
        "queue.messages.enqueued", description: "Queue messages enqueued");
    public static readonly Counter<long> Completed = Meter.CreateCounter<long>(
        "queue.messages.completed", description: "Queue messages completed");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "queue.messages.failed", description: "Queue messages failed");
    public static readonly Histogram<double> ProcessingDuration = Meter.CreateHistogram<double>(
        "queue.processing.duration", unit: "ms", description: "Queue message processing duration");

    public static Activity? StartEnqueue(string queueName) =>
        ActivitySource.StartActivity("queue.enqueue")?
            .SetTag("queue.name", queueName);

    public static Activity? StartDequeue(string queueName) =>
        ActivitySource.StartActivity("queue.dequeue")?
            .SetTag("queue.name", queueName);

    public static Activity? StartComplete(string queueName) =>
        ActivitySource.StartActivity("queue.complete")?
            .SetTag("queue.name", queueName);

    public static void RecordEnqueue(string queueName, string messageId, Activity? activity)
    {
        activity?.SetTag("message.id", messageId);
        Enqueued.Add(1, new TagList { { "queue.name", queueName } });
    }

    public static void RecordComplete(string queueName, string messageId, Activity? activity)
    {
        activity?.SetTag("message.id", messageId);
        Completed.Add(1, new TagList { { "queue.name", queueName } });
    }

    public static void RecordFailure(string queueName, Activity? activity, Exception? ex = null)
    {
        activity?.SetTag("error", true);
        if (ex is not null)
            activity?.SetTag("error.message", ex.Message);
        Failed.Add(1, new TagList { { "queue.name", queueName } });
    }
}
