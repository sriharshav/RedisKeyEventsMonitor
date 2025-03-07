using System;
using System.Net;
using System.Threading.Tasks;

namespace RedisKeyEventsMonitor
{
    /// <summary>
    /// Encapsulates the Redis endpoint and a persistent command connection.
    /// </summary>
    public class RedisClient : IDisposable
    {
        public EndPoint Endpoint { get; }
        private readonly RedisCommandClient _commandClient;

        public RedisClient(EndPoint endpoint)
        {
            Endpoint = endpoint;
            _commandClient = new RedisCommandClient(endpoint);
        }

        /// <summary>
        /// Uses the persistent command connection to retrieve a key's value.
        /// </summary>
        public Task<string> RetrieveKeyValueAsync(string key)
        {
            return _commandClient.RetrieveKeyValueAsync(key);
        }

        /// <summary>
        /// Creates a subscription client that uses its own connection (as required by Redis).
        /// </summary>
        public RedisSubscriptionClient CreateSubscriptionClient()
        {
            return new RedisSubscriptionClient(Endpoint);
        }

        public void Dispose()
        {
            _commandClient.Dispose();
        }
    }
}
