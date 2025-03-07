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
    /// Encapsulates a connection for subscribing to Redis events.
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
        /// Sends a PSUBSCRIBE command for the given pattern and prints the subscription confirmation.
        /// </summary>
        public async Task SubscribeAsync(string pattern, CancellationToken cancellationToken)
        {
            string command = $"PSUBSCRIBE {pattern}\r\n";
            await _writer.WriteAsync(command);
            var confirmation = await RedisProtocolHelper.ReadRedisMessageAsync(_reader, cancellationToken);
            Console.WriteLine("Subscription confirmation: " + string.Join(" | ", confirmation));
        }

        /// <summary>
        /// Reads the next complete message from Redis.
        /// </summary>
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
