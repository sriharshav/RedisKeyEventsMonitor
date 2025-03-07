using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace RedisKeyEventsMonitor
{
    /// <summary>
    /// Represents a Redis key event notification including the key name and (optionally) its current value.
    /// </summary>
    public class RedisKeyEvent
    {
        public string MessageType { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    /// <summary>
    /// A background service that subscribes to Redis key event notifications.
    /// It optionally uses a value retriever delegate to query the current key value.
    /// </summary>
    public class RedisSubscriberService : BackgroundService
    {
        private readonly EndPoint _endpoint;
        private readonly Func<RedisKeyEvent, Task> _processNotification;
        private readonly Func<string, Task<string>>? _valueRetriever;

        /// <summary>
        /// Initializes a new instance of the RedisSubscriberService.
        /// </summary>
        /// <param name="endpoint">The endpoint (Unix or TCP) for connecting to Redis.</param>
        /// <param name="processNotification">Delegate to process the complete key event.</param>
        /// <param name="valueRetriever">
        /// Optional delegate that retrieves the current key value.
        /// It receives a key name and returns its value as a string.
        /// </param>
        public RedisSubscriberService(
            EndPoint endpoint,
            Func<RedisKeyEvent, Task> processNotification,
            Func<string, Task<string>>? valueRetriever = null)
        {
            _endpoint = endpoint;
            _processNotification = processNotification;
            _valueRetriever = valueRetriever;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Starting Redis Subscriber Service...");

            try
            {
                using var socket = RedisProtocolHelper.CreateSocket(_endpoint);
                Console.WriteLine("Connecting to Redis...");
                socket.Connect(_endpoint);

                using var stream = new NetworkStream(socket, ownsSocket: false);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);

                // Send the subscription command.
                await writer.WriteAsync("PSUBSCRIBE __keyevent@*:* \r\n");
                Console.WriteLine("Subscribed to key events.");

                // Read the initial subscription confirmation message.
                var initialMessage = await ReadRedisMessageAsync(reader, stoppingToken);
                Console.WriteLine($"Initial response: {string.Join(" | ", initialMessage)}");

                // Continuously process notifications.
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var message = await ReadRedisMessageAsync(reader, stoppingToken);
                        if (message != null && message.Length >= 4)
                        {
                            var keyEvent = new RedisKeyEvent
                            {
                                MessageType = message[0],
                                Pattern = message[1],
                                Channel = message[2],
                                Key = message[3]
                            };

                            // If a value retriever delegate is provided, use it.
                            if (_valueRetriever != null)
                            {
                                try
                                {
                                    keyEvent.Value = await _valueRetriever(keyEvent.Key);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error retrieving value for key '{keyEvent.Key}': {ex.Message}");
                                    keyEvent.Value = null;
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Redis subscriber: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Redis Subscriber Service is stopping.");
            }
        }

        /// <summary>
        /// Reads a complete Redis multi-bulk message from the stream.
        /// </summary>
        private async Task<string[]> ReadRedisMessageAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            string? header = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(header) || header[0] != '*')
                throw new InvalidDataException("Expected multi-bulk header starting with '*'.");
            if (!int.TryParse(header.Substring(1), out int count))
                throw new InvalidDataException("Invalid multi-bulk header count.");

            string[] message = new string[count];
            for (int i = 0; i < count; i++)
            {
                message[i] = await RedisProtocolHelper.ReadRESPObjectAsync(reader, cancellationToken);
            }
            return message;
        }
    }
}
