using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RedisKeyEventsMonitor
{
    /// <summary>
    /// Represents a Redis key event notification.
    /// </summary>
    public class RedisKeyEvent
    {
        public string MessageType { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        // Note: Value is no longer set by the value retriever.
        public string? Value { get; set; }
    }

    /// <summary>
    /// A background service that subscribes to Redis key event notifications.
    /// Optionally, it calls a delegate to retrieve (and print) the key's value.
    /// </summary>
    public class RedisSubscriberService : BackgroundService
    {
        private readonly EndPoint _endpoint;
        private readonly Func<RedisKeyEvent, Task> _processNotification;
        private readonly Func<string, Task>? _valueRetriever;

        /// <summary>
        /// Initializes a new instance of the RedisSubscriberService.
        /// </summary>
        /// <param name="endpoint">The endpoint (Unix or TCP) for connecting to Redis.</param>
        /// <param name="processNotification">Delegate to process the complete key event.</param>
        /// <param name="valueRetriever">
        /// Optional delegate that retrieves (and prints) the current key value.
        /// It receives the key name and does not return a value.
        /// </param>
        public RedisSubscriberService(
            EndPoint endpoint,
            Func<RedisKeyEvent, Task> processNotification,
            Func<string, Task>? valueRetriever = null)
        {
            _endpoint = endpoint;
            _processNotification = processNotification;
            _valueRetriever = valueRetriever;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Starting Redis Subscriber Service...");

            using var subscriptionClient = new RedisSubscriptionClient(_endpoint);
            await subscriptionClient.SubscribeAsync("__keyevent@*:*", stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = await subscriptionClient.ReadMessageAsync(stoppingToken);
                    if (message.Length >= 4)
                    {
                        var keyEvent = new RedisKeyEvent
                        {
                            MessageType = message[0],
                            Pattern = message[1],
                            Channel = message[2],
                            Key = message[3]
                        };

                        // Call the value retriever delegate (if provided) to print the value.
                        if (_valueRetriever != null)
                        {
                            try
                            {
                                await _valueRetriever(keyEvent.Key);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error retrieving value for key '{keyEvent.Key}': {ex.Message}");
                            }
                        }

                        await _processNotification(keyEvent);
                    }
                    else
                    {
                        Console.WriteLine("Received incomplete message.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving event: {ex.Message}");
                    break;
                }
            }
        }
    }
}
