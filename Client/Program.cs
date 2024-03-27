using CommandLine;
using System.Net;

namespace Client;

class Program
{
    
    static async Task<int> Main(string[] args)
    {
        var parser = new Parser(with => with.EnableDashDash = false);
        // start parsing arguments
        var result = parser.ParseArguments<Options>(args)
            .WithParsed(async options =>
            {
                if (options.DisplayHelp)
                {
                    Options.PrintHelp();
                    return;
                }

                if (options.Protocol == "" || options.Ip == "")  //if protocol or ip is not initial
                {
                    Console.Error.WriteLine("ERR: error parsing arguments");
                    Console.WriteLine("Use -h for help");
                    return;
                }
                
                IPAddress[] addresses = Dns.GetHostAddresses(options.Ip); // if we have not ip but dns server

                if (addresses.Length == 0)
                {
                    Console.Error.WriteLine("ERR: Wrong IP");
                    return;
                }
                
                //IPAddress ip = addresses[0]; 
                IPAddress ip = addresses.FirstOrDefault(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ip==null)
                {
                    Console.Error.WriteLine("ERR: Wrong IP");
                    return;
                }   
                
                if (options.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // if we have tcp connection use method from class tcpCommunication
                        await TcpCommunication.TcpProcessSocketCommunication(options);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"ERR: {ex.Message}");
                    }
                }
                else
                {
                    if (options.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
                    {
                            try
                            {
                                // if we have udp connection use method from class UdpProcessSocketCommunication
                                await UdpCommunication.UdpProcessSocketCommunication(options,ip);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"ERR: {ex.Message}");
                            }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERR: Wrong protocol");
                    }
                }
            })
            .WithNotParsed(errors =>
            {
                Console.Error.WriteLine("ERR: Error parsing arguments");
                Console.WriteLine("Use -h for help");
            });
        return 0;
    }



    
}