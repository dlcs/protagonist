# SQS

This folder contains helpers for monitoring and responding to SQS messages.

The overall premise is that for a given `Enum` (any enum - determined by the implementing project), we can match that `Enum` to a given message handler for a queue.

## Details

A `QueueHandlerResolver` delegate is registered within the ServiceCollection. This takes an `Enum` and returns an implementation of an `IMessageHandler`.

```cs
services
    .AddSingleton<QueueHandlerResolver<MyMessageType>>(provider => messageType => messageType switch
    {
        MyMessageType.Foo => new FooHandler(),
        MyMessageType.Bar => provider.GetRequiredService<BarHandler>(),
    })
```

The `IMessageHandler` interface contains a single method; it receives a `QueueMessage` object and returns a `bool` value indicating whether the message was successfully processed or not.

`QueueMessage` contains details of the message (raw `JsonObject Body`, `Dictionary<string, string> Attributes` and an Id).

`SqsListenerManager` is passed a queue name and the `Enum` message type. Using this it gets the QueueUrl and creates a `SqsListener` object for given queue.

```cs
// Configure queues
await sqsListenerManager.AddQueueListener("foo-queue", MyMessageType.Foo);
await sqsListenerManager.AddQueueListener("bar-queue", MyMessageType.Bar);

// Start listen loop for queues
sqsListenerManager.StartListening();
```

`SqsListener` monitors the queue on a background thread. On receipt of a message it calls the `QueueHandlerResolver` to get handler for given type. If handler was successful message is deleted, else it's left on queue.

## Single Queue + Handler

The above allows for handling messages from multiple queues with different handlers.

However, when there's a single queue with a single handler it feels overly cumbersome.

There are a couple of convenience methods when working with 1 handler for 1 queue.

First use `AddDefaultQueueHandler<THandler>` when configuring services, this will setup `QueueHandlerResolver` to always return `THandler`.
Internally it uses a default `Enum` to avoid needing to create one just for this purpose.

```cs
services
  .AddScoped<MyHandler>()
  .AddDefaultQueueHandler<MyHandler>();
  
// this is equivalent to
services
  .AddScoped<MyHandler>()
  .AddSingleton<QueueHandlerResolver<SingleHandler>>(provider => _ => provider.GetRequiredService<MyHandler>());
```

Then, when setting up `SqsListenerManager` there's no need to add a listener then start listening. `SetupDefaultQueue` does this in 1 operation.

```
await sqsListenerManager.SetupDefaultQueue("my-queue-name");

// which is equivalent to
await sqsListenerManager.AddQueueListener("my-queue-name", SingleHandler.Default);
sqsListenerManager.StartListening();
```