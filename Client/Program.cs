using System;
using System.Threading.Channels;
using CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace Client;

class Program
{
    public enum State : int
    {
        START,
        AUTH,
        OPEN,
        ERROR,
        END
    }
    private static Mutex _mutexSate = new Mutex();
    private static State _state = State.START;
    private static Queue<Confirm> ConfirmList = new Queue<Confirm>();
    private static Queue<Reply> ReplyList = new Queue<Reply>();
    private static Mutex mutexConfirm = new Mutex();
    private static Mutex mutexReply = new Mutex();
    private static Err _err;
    private static string? _displayName;
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
                    Console.WriteLine("ERR: error parsing arguments");
                    Console.WriteLine("Use -h for help");
                    return;
                }
                
                IPAddress[] addresses = Dns.GetHostAddresses(options.Ip);

                if (addresses.Length == 0)
                {
                    Console.WriteLine("ERR: Wrong IP");
                    return;
                }
                
                IPAddress ip = addresses[0];
                

                if (options.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    using (TcpClient tcpClient = new TcpClient())
                    {
                        try
                        {
                            tcpClient.Connect(ip, options.Port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"ERR: {ex.Message}");
                        }
                    }
                }
                else
                {
                    if (options.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
                    {
                            try
                            {
                                await UdpProcessSocketCommunication(options,ip);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"ERR: {ex.Message}");
                            }
                            return;
                    }
                    else
                    {
                        Console.WriteLine("ERR: Wrong protocol");
                        return;
                    }
                }
            })
            .WithNotParsed(errors =>
            {
                Console.WriteLine("Error parsing arguments");
                Console.WriteLine("Use -h for help");
                return;
            });
        return 0;
    }

    static async Task<int> TcpProcessSocketCommunication(
/*TcpClient tcpClient, Options options, HistoryOfCommunication history*/)
    {
        while (true)
        {
            Console.WriteLine("ok\n");
            await Task.Delay(10000);
        }

        return 0;
    }

    static async Task<int> UdpProcessSocketCommunication(Options options,IPAddress ip)
    {
        IMessage.MessageId = 0;
        UdpClient udpClientSend = new UdpClient();
        IPEndPoint serverEP = new IPEndPoint(ip, options.Port);
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
        udpClientSend.Client.Bind(clientEP);
        
        Task<int> receiveTask = ReceiveMessage(udpClientSend,serverEP,udpClientSend,options,ip,clientEP);
        Task<int> readTask = ReadMessage(options,udpClientSend,serverEP);
        Task result = await Task.WhenAny(readTask, receiveTask);
        udpClientSend.Close();
        return 0;
    }


    static async Task<int> ReceiveMessage(UdpClient udpClient,IPEndPoint serverEP,UdpClient sender,Options options,IPAddress ip, IPEndPoint clientEP)
    {
        try
        {
            while (true)
            {
                UdpReceiveResult receiveResult = await udpClient.ReceiveAsync();
                _mutexSate.WaitOne();
                switch (_state)
                {
                    case State.START:
                        _mutexSate.ReleaseMutex();
                        break;
                    case State.AUTH:
                        _mutexSate.ReleaseMutex();
                        if (receiveResult.RemoteEndPoint.Port != serverEP.Port)
                        {
                            serverEP.Port = receiveResult.RemoteEndPoint.Port;
                        }
                
                        switch (receiveResult.Buffer[0])
                        {
                            case (byte)MessageType.CONFIRM:
                                ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                                break;
                            case (byte)MessageType.REPLY:
                                Task a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Reply reply = Reply.FromBytes(receiveResult.Buffer);
                                if (reply.Result)
                                    Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                else
                                    Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                ReplyList.Enqueue(reply);
                                break;
                            case (byte)MessageType.ERR:
                                a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Err err = Err.FromBytes(receiveResult.Buffer);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");

                                Task close = SendBye(sender, serverEP, options);
                                Task<int> readConfirm = ReceiveMessage(udpClient,serverEP,sender,options,ip,clientEP);
                                await close;
                                return 0;
                        }
                        
                        break;
                    
                    
                    
                    case State.OPEN:
                        _mutexSate.ReleaseMutex();
                        switch (receiveResult.Buffer[0])
                        {
                            case (byte)MessageType.CONFIRM:
                                ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                                break;
                            case (byte)MessageType.REPLY:
                                Task a =  SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Reply reply = Reply.FromBytes(receiveResult.Buffer);
                                if (reply.Result)
                                    Console.Error.WriteLine($"Success: {reply.MessageContent}\n");
                                else
                                    Console.Error.WriteLine($"Failure: {reply.MessageContent}\n");
                                ReplyList.Enqueue(Reply.FromBytes(receiveResult.Buffer));
                                break;
                            case (byte)MessageType.ERR:
                                a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Err err = Err.FromBytes(receiveResult.Buffer);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");
                                
                                Task close = SendBye(sender, serverEP, options);
                                Task<int> readConfirm = ReceiveMessage(udpClient,serverEP,sender,options,ip,clientEP);
                                await close;
                                return 0;
                            case (byte)MessageType.MSG:
                                a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Msg message = Msg.FromBytes(receiveResult.Buffer);
                                Console.WriteLine($"{message.DisplayName}: {message.MessageContents}\n");
                                break;
                            case (byte)MessageType.BYE:
                                a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                return 0;
                            default:
                                Err sendErr = new Err();
                                sendErr.MessageContents = "error string";
                                sendErr.DisplayName = _displayName;
                                _mutexSate.WaitOne();
                                _state = State.ERROR;
                                _mutexSate.ReleaseMutex();
                                Task sendError = SendMessageAsync(sendErr.ToBytes(), sender, serverEP, options);
                                Task<int> readConfirm2 = ReceiveMessage(udpClient,serverEP,sender,options,ip,clientEP);
                                await sendError;
                                Task close1 = SendBye(sender, serverEP, options);
                                await close1;
                                return 0;
                        }
                        break;
                    case State.ERROR:
                        _mutexSate.ReleaseMutex();
                        if (receiveResult.Buffer[0] == (byte)MessageType.CONFIRM)
                            ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                        break;
                    case State.END:
                        _mutexSate.ReleaseMutex();
                        if (receiveResult.Buffer[0] == (byte)MessageType.CONFIRM)
                            ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                        break;
                }
            }
        }
        catch (ArgumentException) 
        {
            Console.WriteLine("ERR: ArgumentException");
            throw ;
        }

    }

    static async Task<int> ReadMessage(Options options,UdpClient udpClient,IPEndPoint localEndPoint)
    {
        
        while (true)
        {
            Console.CancelKeyPress += async (sender, e) =>
            {
                _mutexSate.WaitOne();
                if (_state != State.START)
                {
                    _mutexSate.ReleaseMutex();
                    await SendBye(udpClient, localEndPoint, options);
                }
                else
                {
                    _mutexSate.ReleaseMutex();
                }
                Console.WriteLine("End of communication");

                Environment.Exit(0);
            };
            string? userInput = Console.ReadLine();
            if (userInput == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.Error.WriteLine("ERR: Wrong input, repeat");
                continue;
            }

            string[] words = userInput.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                Console.Error.WriteLine("ERR: Wrong input, repeat");
                continue;
            }

            if (words[0] == "/help")
            {
                Options.PrintHelp();
                continue;
            }

            int result;
            _mutexSate.WaitOne();
            switch (_state)
            {
                case State.START:
                case State.AUTH:
                    _mutexSate.ReleaseMutex();
                    switch (words[0])
                    {
                        case "/auth":
                            if (words.Length != 4)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }

                            _displayName = words[3];
                            Auth auth = new Auth()
                            {
                                Username = words[1],
                                DisplayName = words[3],
                                Secret = words[2]
                            };
                            _mutexSate.WaitOne();
                            _state = State.AUTH;
                            _mutexSate.ReleaseMutex();
                            result = await SendMessageAsync(auth.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 1)
                            {
                                int replyBool = await WaitReply(IMessage.MessageId , options);
                                if (replyBool == 1)
                                {
                                    _mutexSate.WaitOne();
                                    _state = State.OPEN;
                                    _mutexSate.ReleaseMutex();
                                }
                                else if (replyBool == -1)
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    return 1;
                                }

                                continue;
                            }
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                        default:
                            Console.Error.WriteLine("ERR: Wrong input, repeat");
                            continue;
                    }
                case State.OPEN:
                    _mutexSate.ReleaseMutex();
                    switch (words[0])
                    {
                        case "/join":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }

                            Join join = new Join()
                            {
                                ChannelId = words[1],
                                DisplayName = _displayName
                            };
                            result = await SendMessageAsync(join.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 1)
                            {
                                int replyBool = await WaitReply(IMessage.MessageId , options);
                                if (replyBool == -1)
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    return 1;
                                }
                                continue;
                            }
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                        case "/rename":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            _displayName = words[1];
                            continue;
                        default:
                            //TODO: проверить состояние отправить сообщение на сервер, проверить 1 символо на / и вывести ошибку
                            if (words[0][0] == '/')
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }

                            Msg msg = new Msg()
                            {
                                MessageContents = userInput, //TODO: проверка на то что символы в utf8
                                DisplayName = _displayName
                            };

                            result = await SendMessageAsync(msg.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 1)
                                continue;
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                    }
                case State.ERROR:
                    _mutexSate.ReleaseMutex();
                    return 1;
                case State.END:
                    _mutexSate.ReleaseMutex();
                    return 0;
            }
        }
    }

    static async Task<int> SendMessageAsync(byte[] message, UdpClient udpClient, IPEndPoint localEndPoint,
        Options options)
    {
        await udpClient.SendAsync(message, message.Length, localEndPoint);
        
        for (int i = 0; i < options.MaxRetries; i++)
        {
            //await Task.Delay(options.UdpTimeout);
            Thread.Sleep(options.UdpTimeout);
            mutexConfirm.WaitOne();
            while (ConfirmList.Count > 0)
            {
                Confirm confirm = new Confirm();
                confirm = ConfirmList.Dequeue();
                if (confirm.MessageId == BitConverter.ToUInt16(message, 1))
                {
                    mutexConfirm.ReleaseMutex();
                    IMessage.MessageId++;
                    return 1;
                }
            }

            mutexConfirm.ReleaseMutex();
        }
        IMessage.MessageId++;
        Console.Error.WriteLine("ERR: No confirm");
        return 0;
    }

    static async Task SendConfirm(byte[] message, UdpClient udpClient, IPEndPoint localEndPoint)
    {
        Confirm confirm = new Confirm();
        confirm.MessageId = BitConverter.ToUInt16(message, 1);
        byte[] confirmByte = confirm.ToBytes();
        await udpClient.SendAsync(confirmByte, confirmByte.Length, localEndPoint);
    }
    
    static async Task SendBye(UdpClient udpClient, IPEndPoint localEndPoint,Options options)
    {
        Bye bye = new Bye();
        _mutexSate.WaitOne();
        _state = State.END;
        _mutexSate.ReleaseMutex();
        await SendMessageAsync(bye.ToBytes(), udpClient, localEndPoint, options);
    }
    
    static async Task<int> WaitReply(ushort messageId, Options options)
    {
        for (int i = 0; i < options.MaxRetries; i++)
        {
            Thread.Sleep(options.UdpTimeout);
            mutexReply.WaitOne();
            while (ReplyList.Count > 0)
            {
                Reply reply;
                reply = ReplyList.Dequeue();
                if (reply.RefMessageId == messageId-1)
                {
                    mutexReply.ReleaseMutex();
                    if (reply.Result)
                        return 1; //true
                    else
                        return 0; //false
                }
            }

            mutexConfirm.ReleaseMutex();
        }
        Console.Error.WriteLine("ERR: no reply");
        return -1; //err
    }
}