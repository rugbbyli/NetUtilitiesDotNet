// See https://aka.ms/new-console-template for more information

using NetUtilities;

Command GetInputCmd() {
    Console.WriteLine(
        @"Please input command: 
    0. Exit App
    1. Ping
    2. TraceRoute
    3. NsLookup
    4. DnsServers");
    var input = Console.ReadKey();
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
        var ip = NsLookup.GetIp(host);
        var host2 = NsLookup.GetHostName(ip);
        Console.WriteLine($"ip of {host} is {ip}, host of {ip} is {host2}");
    }
    else if(input == Command.Ping)
    {
        Console.WriteLine("Please input host name or ip:");
        var host = Console.ReadLine();
        var result = await Ping.RunAsync(new Ping.Options() {target = host,});
        Console.WriteLine(result);
    }
    else if(input == Command.TraceRoute)
    {
        Console.WriteLine("Please input host name:");
        var host = Console.ReadLine();

        var trace = new TraceRoute();
        trace.OnHop += (hop) => Console.WriteLine(hop);
        await trace.RunAsync(host, new TraceRoute.Options());
    }
    else if(input == Command.DnsServers)
    {
        Console.WriteLine(string.Join("\n\n", NetworkInfo.GetDnsServers()));
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