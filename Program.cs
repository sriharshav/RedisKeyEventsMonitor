using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Sockets; // Ensure this using is present
using RedisKeyEventsMonitor;
using System.Threading.Tasks;

namespace MyApp
{
    public class Program
    {
        // Use default! to silence the warning since it will be initialized in Main.
        private static RedisClient _redisClient = default!;

        /// <summary>
        /// Delegate that retrieves the current value for a key using the persistent RedisClient,
        /// prints it to the console, and does not return a value.
        /// </summary>
        public static async Task RetrieveKeyValue(string key)
        {
            string value = await _redisClient.RetrieveKeyValueAsync(key);
            Console.WriteLine($"Retrieved value for key '{key}': {value}");
        }

        public static async Task Main(string[] args)
        {
            // Define a single endpoint for both subscription and key retrieval.
            EndPoint endpoint = new UnixDomainSocketEndPoint("/var/run/redis/redis.sock");
            // Alternatively, for TCP:
            // EndPoint endpoint = new IPEndPoint(System.Net.IPAddress.Loopback, 6379);

            // Create a single persistent RedisClient instance.
            _redisClient = new RedisClient(endpoint);

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register the RedisSubscriberService as a hosted service,
                    // using the same endpoint and the RetrieveKeyValue delegate.
                    services.AddHostedService<RedisSubscriberService>(provider =>
                        new RedisSubscriberService(
                            endpoint,
                            async (redisEvent) =>
                            {
                                Console.WriteLine($"Received event: Type={redisEvent.MessageType}, " +
                                                  $"Pattern={redisEvent.Pattern}, Channel={redisEvent.Channel}, " +
                                                  $"Key={redisEvent.Key}");
                                await Task.CompletedTask;
                            },
                            RetrieveKeyValue
                        ));
                })
                .Build();

            await host.RunAsync();
        }
    }
}
