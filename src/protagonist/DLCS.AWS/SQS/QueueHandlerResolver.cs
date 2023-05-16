using Microsoft.Extensions.DependencyInjection;

namespace DLCS.AWS.SQS;

/// <summary>
/// Delegate for getting <see cref="IMessageHandler"/> for message of specified type.
/// </summary>
/// <param name="messageType">Type of message to be handled.</param>
public delegate IMessageHandler QueueHandlerResolver<in TMessageType>(TMessageType messageType)
    where TMessageType : Enum;


/// <summary>
/// Message handlers are found by an enum, this is a stand-in enum for when we have a single listener 
/// </summary>
internal enum SingleHandler
{
    Default
}

public static class ResolverExtensions
{
    /// <summary>
    /// Convenience method for when there is a single handler type, configures <see cref="QueueHandlerResolver{T}"/> in
    /// DI container to always return that type
    /// </summary>
    /// <param name="serviceCollection">Current serviceCollection</param>
    /// <typeparam name="T">Type of message handler</typeparam>
    /// <returns>Modified <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddDefaultQueueHandler<T>(this IServiceCollection serviceCollection)
        where T : IMessageHandler
        => serviceCollection.AddScoped<QueueHandlerResolver<SingleHandler>>(provider =>
            _ => provider.GetRequiredService<T>());
}