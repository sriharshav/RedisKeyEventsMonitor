using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedisKeyEventsMonitor
{
    public static class RedisProtocolHelper
    {
        public static Socket CreateSocket(EndPoint endpoint)
        {
            if (endpoint is UnixDomainSocketEndPoint)
                return new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            else if (endpoint is IPEndPoint)
                return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            else
                throw new InvalidOperationException("Unsupported endpoint type.");
        }

        public static async Task<string[]> ReadRedisMessageAsync(StreamReader reader, CancellationToken cancellationToken)
        {
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

        public static async Task<string> ReadRESPObjectAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            string? line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line))
                throw new EndOfStreamException("Unexpected end of stream.");

            char prefix = line[0];
            switch (prefix)
            {
                case '$':
                    if (!int.TryParse(line.Substring(1), out int length))
                        throw new InvalidDataException("Invalid bulk string length.");
                    if (length == -1)
                        return string.Empty;
                    char[] buffer = new char[length];
                    int read = 0;
                    while (read < length)
                    {
                        int r = await reader.ReadAsync(buffer, read, length - read);
                        if (r == 0)
                            throw new EndOfStreamException("Unexpected end of stream while reading bulk string.");
                        read += r;
                    }
                    await reader.ReadLineAsync(); // Discard trailing CRLF.
                    return new string(buffer);

                case '+':
                    return line.Substring(1);

                case ':':
                    return line.Substring(1);

                case '-':
                    throw new Exception("Redis error: " + line.Substring(1));

                default:
                    throw new InvalidDataException("Unexpected RESP type: " + prefix);
            }
        }

        public static async Task<string> RetrieveKeyValue(EndPoint endpoint, string key)
        {
            using Socket socket = CreateSocket(endpoint);
            socket.Connect(endpoint);

            using NetworkStream stream = new NetworkStream(socket, ownsSocket: false);
            using StreamWriter writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
            using StreamReader reader = new StreamReader(stream, Encoding.ASCII);

            await writer.WriteAsync($"GET {key}\r\n");
            return await ReadRESPObjectAsync(reader, CancellationToken.None);
        }
    }
}
