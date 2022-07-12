using Engine.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// <see cref="IHealthCheck"/> that checks all registered queues are not stopped
/// </summary>
public class QueueHealthCheck : IHealthCheck
{
    private readonly SqsListenerManager listenerManager;

    public QueueHealthCheck(SqsListenerManager listenerManager)
    {
        this.listenerManager = listenerManager;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = new())
        => Task.FromResult(GetCheckResult());

    private HealthCheckResult GetCheckResult()
    {
        var queues = listenerManager.GetConfiguredQueues();

        if (queues.Count == 0)
        {
            return HealthCheckResult.Healthy("Healthy");
        }
        
        if (queues.Count(q => q.IsListening == false) > 0)
        {
            HealthCheckResult.Unhealthy("Queue listener stopped", data: GetData(queues));
        }

        if (queues.Count(q => q.IsListening == null) > 0)
        {
            HealthCheckResult.Degraded("Queue not started", data: GetData(queues));
        }
        
        return HealthCheckResult.Healthy("Healthy", data: GetData(queues));
    }
    
    private Dictionary<string, object> GetData(IReadOnlyList<ConfiguredQueue> configuredQueues)
    {
        var dataDictionary = new Dictionary<string, object>(configuredQueues.Count);

        foreach (var configuredQueue in configuredQueues)
        {
            var status = configuredQueue.IsListening == null
                ? "Not started"
                : configuredQueue.IsListening.Value
                    ? "Listening"
                    : "Stopped";
            dataDictionary[configuredQueue.QueueName] = status;
        }
        return dataDictionary;
    }
}

public static class HealthCheckX
{
    /// <summary>
    /// Add a health check for registered queue
    /// </summary>
    /// <param name="builder">The <see cref="IHealthChecksBuilder"/>.</param>
    /// <param name="name">The health check name. Optional. If <c>null</c> then Registered Queues.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> that should be reported when the health check fails. Optional. If <c>null</c> then
    /// the default status of <see cref="HealthStatus.Unhealthy"/> will be reported.
    /// </param>
    /// <param name="tags">A list of tags that can be used to filter sets of health checks. Optional.</param>
    /// <param name="timeout">An optional <see cref="TimeSpan"/> representing the timeout of the check.</param>
    /// <returns>The specified <paramref name="builder"/>.</returns>
    public static IHealthChecksBuilder AddQueueHealthCheck(this IHealthChecksBuilder builder,
        string? name = default,
        HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default,
        TimeSpan? timeout = default)
        => builder.Add(new HealthCheckRegistration(
            name ?? "Registered Queues",
            sp => ActivatorUtilities.CreateInstance<QueueHealthCheck>(sp),
            failureStatus,
            tags,
            timeout));
}