using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RedisKeyEventsMonitor;

namespace MyApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Define a single endpoint for both subscription and key reading.
            EndPoint endpoint = new UnixDomainSocketEndPoint("/var/run/redis/redis.sock");
            // Alternatively, for TCP:
            // EndPoint endpoint = new IPEndPoint(System.Net.IPAddress.Loopback, 6379);

            // Create a single RedisClient instance using the chosen endpoint.
            var redisClient = new RedisClient(endpoint);

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // Register the RedisSubscriberService as a hosted service,
                    // passing in the same endpoint and the RedisClient's RetrieveKeyValue method.
                    services.AddHostedService<RedisSubscriberService>(provider =>
                        new RedisSubscriberService(
                            endpoint,
                            async (redisEvent) =>
                            {
                                Console.WriteLine($"Received event: Type={redisEvent.MessageType}, " +
                                    $"Pattern={redisEvent.Pattern}, Channel={redisEvent.Channel}, " +
                                    $"Key={redisEvent.Key}, Value={redisEvent.Value}");
                                await Task.CompletedTask;
                            },
                            redisClient.RetrieveKeyValue
                        ));
                })
                .Build();

            await host.RunAsync();
        }
    }
}
