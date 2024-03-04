using System.Threading.Channels;
using CommandLine;


class Options
{
    [Option('t',  Required = false, Default = "", HelpText = "The transport protocol used for the connection")]
    public string? TransportProtocol { get; set; }
    
    [Option('s', Required = false, Default = "", HelpText = "Server IP address or host name")]
    public string? Server { get; set; }

    [Option('p',  Required = false, Default =4567, HelpText = "Server port")]
    public int Port { get; set; }

    [Option('d', Required = false, Default =250, HelpText = "UDP acknowledgement timeout")]
    public int UdpTimeout { get; set; }

    [Option('r', Required = false, Default =3, HelpText = "Maximum number of repeated transmissions UDP")]
    public int MaxRetries { get; set; }
    
    [Option('h',  Required = false, HelpText = "Prints the program help and terminates the program.")]
    public bool DisplayHelp { get; set; }

    public void PrintHelp()
    {
        Console.WriteLine("Program help:");
        Console.WriteLine($"-t: The transport protocol used for the connection. Required");
        Console.WriteLine($"-s: Server IP address or host name. Required");
        Console.WriteLine($"-p: Server port. Default 4567");
        Console.WriteLine($"-d: UDP acknowledgement timeout. Default 250");
        Console.WriteLine($"-r: Maximum number of repeated transmissions UDP. Default 3");
    }
}

class Program
{
    static void Main(string[] args)
    {
        var parser = new Parser(with => with.EnableDashDash = false);

        var result = parser.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                if (options.DisplayHelp)
                {
                    options.PrintHelp();
                    return;
                }
                else
                {
                    if (options.TransportProtocol == "" || options.Server == "")
                    {
                        Console.WriteLine("Error parsing arguments");
                        Console.WriteLine("Use -h for help");
                    }
                }
            })
            .WithNotParsed(errors =>
            {
                Console.WriteLine("Error parsing arguments");
                Console.WriteLine("Use -h for help");
            });
    }
}