using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedisKeyEventsMonitor
{
    /// <summary>
    /// A simple utility that encapsulates connecting to Redis, subscribing to a pattern,
    /// and reading RESP messages from the connection.
    /// </summary>
    public class RedisSubscriptionClient : IDisposable
    {
        private readonly EndPoint _endpoint;
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

        public RedisSubscriptionClient(EndPoint endpoint)
        {
            _endpoint = endpoint;
            _socket = RedisProtocolHelper.CreateSocket(_endpoint);
            _socket.Connect(_endpoint);
            _stream = new NetworkStream(_socket, ownsSocket: false);
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };
        }

        /// <summary>
        /// Sends a PSUBSCRIBE command for the given pattern and reads the initial confirmation.
        /// </summary>
        /// <param name="pattern">The subscription pattern (e.g., "__keyevent@*:*").</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        public async Task SubscribeAsync(string pattern, CancellationToken cancellationToken)
        {
            string command = $"PSUBSCRIBE {pattern}\r\n";
            await _writer.WriteAsync(command);
            // Read the subscription confirmation message.
            var confirmation = await RedisProtocolHelper.ReadRedisMessageAsync(_reader, cancellationToken);
            Console.WriteLine("Subscription confirmation: " + string.Join(" | ", confirmation));
        }

        /// <summary>
        /// Reads the next complete multi-bulk message from Redis.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An array of strings representing the message parts.</returns>
        public async Task<string[]> ReadMessageAsync(CancellationToken cancellationToken)
        {
            return await RedisProtocolHelper.ReadRedisMessageAsync(_reader, cancellationToken);
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _socket?.Dispose();
        }
    }
}
