using System.Threading.Channels;
using CommandLine;
using System;
namespace Client;
    class Options
    {
        [Option('t',  Required = false, Default = "", HelpText = "The transport protocol used for the connection")]
        public string Protocol { get; init; }
    
        [Option('s', Required = false, Default = "", HelpText = "Server IP address or host name")]
        public string Ip { get; init; }

        [Option('p',  Required = false, Default =4567, HelpText = "Server port")]
        public int Port { get; set; }

        [Option('d', Required = false, Default =250, HelpText = "UDP acknowledgement timeout")]
        public int UdpTimeout { get; init; }

        [Option('r', Required = false, Default = 3, HelpText = "Maximum number of repeated transmissions UDP")]
        public int MaxRetries { get; init; }
    
        [Option('h',  Required = false, HelpText = "Prints the program help and terminates the program.")]
        public bool DisplayHelp { get; init; }

        public static void PrintHelp()
        {
            Console.WriteLine("Program help:");
            Console.WriteLine($"-t: The transport protocol used for the connection. Required");
            Console.WriteLine($"-s: Server IP address or host name. Required");
            Console.WriteLine($"-p: Server port. Default 4567");
            Console.WriteLine($"-d: UDP acknowledgement timeout. Default 250");
            Console.WriteLine($"-r: Maximum number of repeated transmissions UDP. Default 3");
        }
    }
