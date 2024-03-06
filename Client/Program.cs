using System.Threading.Channels;
using CommandLine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Reflection;

namespace Client;

class Program
{
    static void Main(string[] args)
    {
        var parser = new Parser(with => with.EnableDashDash = false);
        Socket listenerSocket;
        var result = parser.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                if (options.DisplayHelp)
                {
                    Options.PrintHelp();
                    return;
                }

                if (options.Protocol == "" || options.Ip == "")
                {
                    Console.WriteLine("Error parsing arguments");
                    Console.WriteLine("Use -h for help");
                    return;
                }

                try
                {
                    if (options.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                    {
                        listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    }
                    else
                    {
                        if (options.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
                        {
                            listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        }
                        else
                        {
                            Console.WriteLine("Wrong protocol");
                            return;
                        }
                    }

                    if (IPAddress.TryParse(options.Ip, out IPAddress? ip))
                    {
                        var localEndPoint = new IPEndPoint(ip, options.Port);
                        listenerSocket.Bind(localEndPoint);
                        listenerSocket.Listen(10);
                    }
                    else
                    {
                        Console.WriteLine("Wrong IP");
                        return;
                    }

                    HistoryOfCommunication history = new HistoryOfCommunication();
                    if (listenerSocket.ProtocolType == ProtocolType.Tcp)
                    {
                        Task sendMessage = TcpProcessSocketCommunication(listenerSocket, options, history);
                    }
                    else
                    {
                        Task sendMessage = UdpProcessSocketCommunication(listenerSocket, options, history);
                        //TODO: AWAIT
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    return;
                }
            })
            .WithNotParsed(errors =>
            {
                foreach (var error in errors)
                {
                    Console.WriteLine($"error: {error}");
                }

                Console.WriteLine("Error parsing arguments");
                Console.WriteLine("Use -h for help");
                return;
            });
    }

    static  async Task<int> TcpProcessSocketCommunication(Socket listenerSocket, Options options, HistoryOfCommunication history)
    {
        return 0;
    }

    static async Task<int> UdpProcessSocketCommunication(Socket listenerSocket, Options options, HistoryOfCommunication history)
    {
        ReadMessage(history);
        return 0;
    }

    static int ReadMessage(HistoryOfCommunication history)
    {
        int nextChar = Console.In.Peek();

        if (nextChar == -1)
        {
            return -1; //end of read
        }
        //TODO: проверить на ctrl c и отправить bye

        string userInput = Console.ReadLine();
        string[] words = userInput.Split(' ');

        if (words.Length == 0)
        {
            Console.WriteLine("Wrong input, repeat");
            return 1;
        }

        switch (words[0])
        {
            case "/auth":
                if (history.AuthSuccess==true)
                {
                    Console.WriteLine("Wrong input, you are authorize ");
                    return 1; 
                }
                if (words.Length != 4)
                {
                    Console.WriteLine("Wrong input, repeat");
                    return 1;
                }

                Auth auth = new Auth()
                {
                    Username = words[1],
                    DisplayName = words[3],
                    Secret = words[2]
                };
                lock (history.lockObject)
                {
                    auth.MessageId = history.i;
                    history.i++;
                    history.AuthList.Add(auth);
                    history.Name = auth.DisplayName;
                    //TODO: ОТПРАВИТЬ СООБЩЕНИЕ
                    return 0;
                }


            case "/join":
                if (history.AuthSuccess == false)
                {
                    Console.WriteLine("Wrong input, you need to authorize");
                    return 1; 
                }
                if (words.Length != 2)
                {
                    Console.WriteLine("Wrong input, repeat");
                    return 1;
                }

                Join join = new Join()
                {
                    ChannelId = words[1],
                };
                lock (history.lockObject)
                {
                    join.DisplayName = history.Name;
                    join.MessageId = history.i;
                    history.i++;
                    history.JoinList.Add(join);
                    //TODO: ОТПРАВИТЬ СООБЩЕНИЕ
                    return 0;
                }
                
            case "/rename":
                if (history.AuthSuccess == false)
                {
                    Console.WriteLine("Wrong input, you need to authorize");
                    return 1; 
                }
                if (words.Length != 2)
                {
                    Console.WriteLine("Wrong input, repeat");
                    return 1;
                }
                lock (history.lockObject)
                {
                    history.Name = words[1];
                    return 0;
                }

            case "/help":
                Options.PrintHelp();
                return 0;
            default:
                //TODO: проверить состояние отправить сообщение на сервер, проверить 1 символо на / и вывести ошибку
                if (words[0][0] == '/')
                {
                    Console.WriteLine("Wrong input, repeat");
                    return 1;
                }
                if (history.AuthSuccess == false)
                {
                    Console.WriteLine("Wrong input, you need to authorize");
                    return 1; 
                }

                Msg msg = new Msg()
                {
                    MessageContents = userInput //TODO: проверка на то что символы в utf8
                };
                lock (history.lockObject)
                {
                    msg.DisplayName = history.Name;
                    msg.MessageId = history.i;
                    history.i++;
                    history.MsgList.Add(msg);
                    //TODO: ОТПРАВИТЬ СООБЩЕНИЕ
                    return 0;
                }
        }

        return 0;
    }
}