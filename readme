## RedisKeyEventsMonitor ##

RedisKeyEventsMonitor is a .NET library that provides a background service for subscribing to Redis key event notifications using the Redis Pub/Sub mechanism. It supports both Unix domain socket and TCP connections, making it suitable for a variety of deployment environments.

**Features**
- Flexible Connection Options: Use either Unix domain sockets or TCP connections to subscribe to Redis key events.
- Background Service: Built as a .NET Generic Host background service for seamless integration.
- Customizable Notification Handling: Easily provide your own logic via a delegate for processing each notification.
- Reusable Library: Package as a NuGet package to be consumed in any .NET application.

**Installation**
Install via NuGet Package Manager or with the command line:

```bash
dotnet add package RedisKeyEventsMonitor --version 1.0.0
```

## Using with a Unix Domain Socket ##

If your Redis server is configured to use a Unix domain socket (e.g., redis.sock), you can set up the service as follows:

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using RedisKeyEventsMonitor;

    namespace <namespace>
    {
        public class Program
        {
            public static async Task Main(string[] args)
            {
                // Define the Unix domain socket endpoint.
                EndPoint endpoint = new UnixDomainSocketEndPoint("/var/run/redis/redis.sock");

                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<RedisSubscriberService>(provider =>
                            new RedisSubscriberService(endpoint, async (notification) =>
                            {
                                // Handle the notification.
                                System.Console.WriteLine($"Received notification: {notification}");
                                await Task.CompletedTask;
                            }));
                    })
                    .Build();

                await host.RunAsync();
            }
        }
    }

## Using with a TCP Socket ##

For a typical TCP connection to Redis (for example, running on localhost:6379), use this configuration:


    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System.Net;
    using System.Threading.Tasks;
    using RedisKeyEventsMonitor;

    namespace <namespace>
    {
        public class Program
        {
            public static async Task Main(string[] args)
            {
                // Define the TCP endpoint.
                EndPoint endpoint = new IPEndPoint(IPAddress.Loopback, 6379);

                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<RedisSubscriberService>(provider =>
                            new RedisSubscriberService(endpoint, async (notification) =>
                            {
                                // Handle the notification.
                                System.Console.WriteLine($"Received notification: {notification}");
                                await Task.CompletedTask;
                            }));
                    })
                    .Build();

                await host.RunAsync();
            }
        }
    }

## How It Works ##

The service connects to Redis and issues the command:

    PSUBSCRIBE __keyevent@*:*

It then continuously reads and parses the complete multi-bulk RESP messages, formatting them as notifications that are passed to your delegate. This allows you to concentrate on your business logic rather than the low-level protocol details.

RESP parser supports:
- Bulk Strings: (prefixed with $)
- Simple Strings: (prefixed with +)
- Integers: (prefixed with :)
- Errors: (prefixed with -)

## Extending and Customizing ##

- Custom Notification Handling: The notification delegate allows you to plug in your own logic (e.g., parsing the payload, triggering actions, or integrating with other systems).
- Subscription Customization: Currently, the service subscribes to key events using the default command. If you need different subscriptions or further customization, consider forking the library or contributing enhancements.

## Contributing ##

Contributions, bug fixes, and improvements are welcome! Feel free to open issues or submit pull requests on the project's repository.

## License ##

This project is licensed under the MIT License. See the LICENSE file for details.