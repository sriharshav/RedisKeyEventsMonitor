// -----------------------------------------------------------------------
// <summary>
//   Contains the implementation of the RedisSubscriberService which
//   supports both Unix domain sockets and TCP connections for subscribing
//   to Redis key events.
// </summary>
// <author>
//   Sriharsha V. Thimmareddy
// </author>
// <date>
//   2025/06/03
// </date>
// <license>
//   MIT
// </license>
// -----------------------------------------------------------------------

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
    public class RedisSubscriberService : BackgroundService
    {
        private readonly EndPoint _endpoint;
        private readonly Func<string, Task> _processNotification;

        public RedisSubscriberService(EndPoint endpoint, Func<string, Task> processNotification)
        {
            _endpoint = endpoint;
            _processNotification = processNotification;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("Starting Redis Subscriber Service...");

            try
            {
                using var socket = CreateSocket();
                Console.WriteLine("Connecting to Redis...");
                socket.Connect(_endpoint);

                using var stream = new NetworkStream(socket, ownsSocket: false);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
                using var reader = new StreamReader(stream, Encoding.ASCII);

                // Send subscription command
                await writer.WriteAsync("PSUBSCRIBE __keyevent@*:*\r\n");
                Console.WriteLine("Subscribed to key events.");

                // Read initial confirmation message
                var initialMessage = await ReadRedisMessageAsync(reader, stoppingToken);
                Console.WriteLine($"Initial response: {string.Join(" | ", initialMessage)}");

                // Process notifications
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var message = await ReadRedisMessageAsync(reader, stoppingToken);
                        if (message != null && message.Length >= 4)
                        {
                            // Format:
                            // [0]: message type ("pmessage")
                            // [1]: subscription pattern
                            // [2]: channel name
                            // [3]: payload (the key event)
                            string formatted = $"Type: {message[0]}, Pattern: {message[1]}, Channel: {message[2]}, Payload: {message[3]}";
                            await _processNotification(formatted);
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
        /// Creates a socket based on the type of endpoint.
        /// </summary>
        private Socket CreateSocket()
        {
            if (_endpoint is UnixDomainSocketEndPoint)
            {
                return new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            }
            else if (_endpoint is IPEndPoint)
            {
                return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                throw new InvalidOperationException("Unsupported endpoint type.");
            }
        }

        /// <summary>
        /// Reads a complete Redis multi-bulk message from the stream.
        /// </summary>
        private async Task<string[]> ReadRedisMessageAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            // Read the multi-bulk header (e.g., "*4")
            string? header = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(header) || header[0] != '*')
                throw new InvalidDataException("Expected multi-bulk header starting with '*'.");

            if (!int.TryParse(header.Substring(1), out int count))
                throw new InvalidDataException("Invalid multi-bulk header count.");

            string[] message = new string[count];
            for (int i = 0; i < count; i++)
            {
                message[i] = await ReadRESPObjectAsync(reader, cancellationToken);
            }
            return message;
        }

        /// <summary>
        /// Reads a single RESP object (bulk string, simple string, integer, or error) from the stream.
        /// </summary>
        private async Task<string> ReadRESPObjectAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                throw new EndOfStreamException("Unexpected end of stream.");

            char prefix = line[0];
            switch (prefix)
            {
                case '$': // Bulk string
                    if (!int.TryParse(line.Substring(1), out int length))
                        throw new InvalidDataException("Invalid bulk string length.");

                    if (length == -1) return string.Empty;

                    char[] buffer = new char[length];
                    int read = 0;
                    while (read < length)
                    {
                        int r = await reader.ReadAsync(buffer, read, length - read);
                        if (r == 0)
                            throw new EndOfStreamException("Unexpected end of stream while reading bulk string.");
                        read += r;
                    }
                    // Discard trailing CRLF
                    await reader.ReadLineAsync();
                    return new string(buffer);

                case '+': // Simple string
                    return line.Substring(1);

                case ':': // Integer
                    return line.Substring(1);

                case '-': // Error
                    throw new Exception("Redis error: " + line.Substring(1));

                default:
                    throw new InvalidDataException("Unexpected RESP type: " + prefix);
            }
        }
    }
}
