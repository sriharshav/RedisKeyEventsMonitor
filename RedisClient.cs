using System;
using System.Net;
using System.Threading.Tasks;

namespace RedisKeyEventsMonitor
{
    /// <summary>
    /// A simple client that encapsulates the Redis endpoint and provides methods for both subscription and key retrieval.
    /// </summary>
    public class RedisClient
    {
        public EndPoint Endpoint { get; }

        public RedisClient(EndPoint endpoint)
        {
            Endpoint = endpoint;
        }

        /// <summary>
        /// Retrieves the current value of a key using a raw GET command over the configured endpoint.
        /// </summary>
        public Task<string> RetrieveKeyValue(string key)
        {
            // Uses the same endpoint as the subscription.
            return RedisProtocolHelper.RetrieveKeyValue(Endpoint, key);
        }

        /// <summary>
        /// Creates a subscription client that uses the same endpoint.
        /// </summary>
        public RedisSubscriptionClient CreateSubscriptionClient()
        {
            return new RedisSubscriptionClient(Endpoint);
        }
    }
}
