﻿using System;
using System.Threading.Channels;
using CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Client;

class Program
{
    
    static async Task<int> Main(string[] args)
    {
        var parser = new Parser(with => with.EnableDashDash = false);
        var result = parser.ParseArguments<Options>(args)
            .WithParsed(async options =>
            {
                if (options.DisplayHelp)
                {
                    Options.PrintHelp();
                    return;
                }

                if (options.Protocol == "" || options.Ip == "")
                {
                    Console.Error.WriteLine("ERR: error parsing arguments");
                    Console.WriteLine("Use -h for help");
                    return;
                }
                
                IPAddress[] addresses = Dns.GetHostAddresses(options.Ip);

                if (addresses.Length == 0)
                {
                    Console.Error.WriteLine("ERR: Wrong IP");
                    return;
                }
                
                IPAddress ip = addresses[0];
                

                if (options.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
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
                return;
            });
        return 0;
    }



    
}