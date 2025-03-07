using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedisKeyEventsMonitor
{
    public class RedisCommandClient : IDisposable
    {
        private readonly EndPoint _endpoint;
        private readonly Socket _socket;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public RedisCommandClient(EndPoint endpoint)
        {
            _endpoint = endpoint;
            _socket = RedisProtocolHelper.CreateSocket(_endpoint);
            _socket.Connect(_endpoint);
            _stream = new NetworkStream(_socket, ownsSocket: false);
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };
        }

        /// <summary>
        /// Uses the persistent connection to send a GET command and read its response.
        /// </summary>
        public async Task<string> RetrieveKeyValueAsync(string key)
        {
            await _semaphore.WaitAsync();
            try
            {
                await _writer.WriteAsync($"GET {key}\r\n");
                return await RedisProtocolHelper.ReadRESPObjectAsync(_reader, CancellationToken.None);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _socket?.Dispose();
            _semaphore?.Dispose();
        }
    }
}
