// See https://aka.ms/new-console-template for more information

using NetUtilities;

namespace Demo
{
    class Program
    {
        static Command GetInputCmd() {
            Console.WriteLine(
                @"Please input command: 
            0. Exit App
            1. Ping
            2. TraceRoute
            3. NsLookup
            4. DnsServers");
            var input = Console.ReadKey(false);
            Console.WriteLine();
            return input.Key switch
            {
                ConsoleKey.D0 => Command.Exit,
                ConsoleKey.D1 => Command.Ping,
                ConsoleKey.D2 => Command.TraceRoute,
                ConsoleKey.D3 => Command.NsLookup,
                ConsoleKey.D4 => Command.DnsServers,
                _ => Command.Unknown
            };
        }

        public static async Task Main(string[] args)
        {
            Command input;
            while((input = GetInputCmd()) != Command.Exit)
            {
                if(input == Command.Unknown)
                {
                    Console.WriteLine("Unknown command, please try again.");
                    continue;
                }
                if(input == Command.NsLookup)
                {
                    Console.WriteLine("Please input host name:");
                    var host = Console.ReadLine();
                    var ip = await NsLookup.GetIpAsync(host);
                    var host2 = await NsLookup.GetHostNameAsync(ip);
                    Console.WriteLine($"ip of {host} is {ip}, host of {ip} is {host2}");
                }
                else if(input == Command.Ping)
                {
                    Console.WriteLine("Please input host name or ip:");
                    var host = Console.ReadLine();
                    var ip = await NsLookup.GetIpAsync(host);
                    Console.WriteLine("Please input ttl (default 64):");
                    if (!int.TryParse(Console.ReadLine(), out var ttl))
                    {
                        ttl = 64;
                    }
                    if (ip == null)
                    {
                        Console.WriteLine($"ping {host} failed, resolve ip error.");
                    }
                    else
                    {
                        IPingDelegate ping = new UDPPing();
                        var timeout = 4000;
                        var result = await ping.RunAsync(ip, ttl, timeout, 32);
                        Console.WriteLine($"ping {host} ({ip}) with ttl {ttl} and timeout {timeout}");
                        Console.WriteLine(result);
                        (ping as IDisposable)?.Dispose();
                        Console.WriteLine("ping finish.");
                    }
                }
                else if(input == Command.TraceRoute)
                {
                    Console.WriteLine("Please input host name:");
                    var host = Console.ReadLine();
                    var ip = await NsLookup.GetIpAsync(host);
                    if (ip == null)
                    {
                        Console.WriteLine($"traceroute to {host} failed, resolve ip error.");
                    }
                    else
                    {
                        var trace = new TraceRoute();
                        var ping = new UDPPing();
                        var options = new TraceRoute.Options() {Target = ip, RetryTimes = 0, PacketSize = 32, PingDelegate = ping};
                        trace.OnHop += (hop) => Console.WriteLine(hop);
                        
                        Console.WriteLine($"traceroute to {host} ({ip}), {options.MaxHops} hops max, {options.PacketSize} byte packets");
                        await trace.RunAsync(options, CancellationToken.None);
                        
                        Console.WriteLine($"traceroute finish.");
                    }
                    
                }
            }
        }

        enum Command
        {
            Unknown = -1,
            Exit = 0,
            Ping,
            TraceRoute,
            NsLookup,
            DnsServers
        }
    }
}