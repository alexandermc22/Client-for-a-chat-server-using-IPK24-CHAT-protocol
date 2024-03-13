using System;
using System.Threading.Channels;
using CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

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

    private static State _state = State.START;
    private static HistoryOfCommunication history = new HistoryOfCommunication();
    private static Queue<Confirm> ConfirmList = new Queue<Confirm>();
    private static Queue<Reply> ReplyList = new Queue<Reply>();
    private static Mutex mutexConfirm = new Mutex();
    private static Mutex mutexReply = new Mutex();
    private static Err _err;
    private static string? _displayName;
    static async Task Main(string[] args)
    {
        Confirm c = new Confirm();
        c.MessageId = 0;
        Confirm c1 = new Confirm();
        c1.MessageId = 1;
        Reply r = new Reply();
        r.MessageId = 1;
        r.RefMessageId = 0;
        r.Result = true;
        r.MessageContent="ok";
        ConfirmList.Enqueue(c);
        ConfirmList.Enqueue(c1);
        ReplyList.Enqueue(r);
        
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
                    Console.WriteLine("Error parsing arguments");
                    Console.WriteLine("Use -h for help");
                    return;
                }
                //HistoryOfCommunication history = new HistoryOfCommunication();

                if (!IPAddress.TryParse(options.Ip, out IPAddress? ip))
                {
                    Console.WriteLine("Wrong IP");
                    return;
                }

                if (options.Protocol.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    using (TcpClient tcpClient = new TcpClient())
                    {
                        try
                        {
                            // Подключаемся к серверу
                            tcpClient.Connect(ip, options.Port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка подключения: {ex.Message}");
                        }
                    }
                }
                else
                {
                    if (options.Protocol.Equals("udp", StringComparison.OrdinalIgnoreCase))
                    {
                            try
                            {
                                await UdpProcessSocketCommunication(options, history,ip);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"wtf: {ex.Message}");
                            }
                    }
                    else
                    {
                        Console.WriteLine("Wrong protocol");
                        return;
                    }
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

    static async Task<int> UdpProcessSocketCommunication(Options options, HistoryOfCommunication history,IPAddress ip)
    {
        IMessage.MessageId = 0;
        UdpClient udpClientReceive = new UdpClient(4567);
        UdpClient udpClientSend = new UdpClient();
        IPEndPoint serverEP = new IPEndPoint(ip, options.Port);
        
        Task<int> receiveTask = ReceiveMessage(udpClientReceive,serverEP,udpClientSend,options);
        Task<int> readTask = ReadMessage(history, options,udpClientSend,serverEP);
        Task result = await Task.WhenAny(readTask, receiveTask);
        if(result==receiveTask)
            Console.WriteLine("rec");
        else
        {
            Console.WriteLine("read");
        }
        return 0;
    }


    static async Task<int> ReceiveMessage(UdpClient udpClient,IPEndPoint serverEP,UdpClient sender,Options options)
    {
        try
        {
            while (true)
            {
                // if (_state == State.END)
                //     return 0;
                UdpReceiveResult receiveResult = await udpClient.ReceiveAsync();

                switch (_state)
                {
                    case State.START:
                        break;
                    case State.AUTH:
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
                                Console.Error.WriteLine($"ERROR FROM {err.DisplayName}: {err.MessageContents}");

                                Task close = SendBye(sender, serverEP, options);
                                Task<int> readConfirm = ReceiveMessage(udpClient,serverEP,sender,options);
                                await close;
                                return 0;
                        }
                        break;
                    case State.OPEN:
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
                                Console.Error.WriteLine($"ERROR FROM {err.DisplayName}: {err.MessageContents}");
                                
                                Task close = SendBye(sender, serverEP, options);
                                Task<int> readConfirm = ReceiveMessage(udpClient,serverEP,sender,options);
                                await close;
                                return 0;
                            case (byte)MessageType.MSG:
                                a = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Msg message = Msg.FromBytes(receiveResult.Buffer);
                                Console.WriteLine($"{message.DisplayName}: {message.MessageContents}\n");
                                break;
                            case (byte)MessageType.BYE:
                                return 0;
                            default:
                                Task close1 = SendBye(sender, serverEP, options);
                                Task<int> readConfirm1 = ReceiveMessage(udpClient,serverEP,sender,options);
                                await close1;
                                return 0;
                        }
                        break;
                    case State.ERROR:
                        if (receiveResult.Buffer[0] == (byte)MessageType.CONFIRM)
                            ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                        break;
                    case State.END:
                        if (receiveResult.Buffer[0] == (byte)MessageType.CONFIRM)
                            ConfirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                        break;
                        return 0;
                }
            }
        }
        catch (ArgumentException) // сделать если на вход говно
        {
            Console.WriteLine("end");
            throw ;
        }

    }

    static async Task<int> ReadMessage(HistoryOfCommunication history, Options options,UdpClient udpClient,IPEndPoint localEndPoint)
    {
        while (true)
        {
            string? userInput = Console.ReadLine();
            if (userInput == null)
            {
                if (_state != State.START) 
                    await SendBye(udpClient, localEndPoint, options);
                return 0;
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                continue;
            }

            string[] words = userInput.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                continue;
            }

            if (words[0] == "/help")
            {
                Options.PrintHelp();
                continue;
            }

            int result;
            switch (_state)
            {
                case State.START:
                case State.AUTH:
                    switch (words[0])
                    {
                        case "/auth":
                            if (words.Length != 4)
                            {
                                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                                continue;
                            }

                            _displayName = words[3];
                            Auth auth = new Auth()
                            {
                                Username = words[1],
                                DisplayName = words[3],
                                Secret = words[2]
                            };
                            _state = State.AUTH;
                            result = await SendMessageAsync(auth.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 0)
                            {
                                int replyBool = await WaitReply(IMessage.MessageId , options);
                                if (replyBool == 1)
                                {
                                    _state = State.OPEN;
                                }
                                else if (replyBool == -1)
                                {
                                    Console.Error.WriteLine("ERROR: no reply");
                                    await SendBye(udpClient, localEndPoint, options);
                                    return 1;
                                }

                                continue;
                            }
                            else
                            {
                                Console.Error.WriteLine("ERROR: No confirm");
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                        default:
                            Console.Error.WriteLine("ERROR: Wrong input, repeat");
                            continue;
                    }
                case State.OPEN:
                    switch (words[0])
                    {
                        case "/join":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                                continue;
                            }

                            Join join = new Join()
                            {
                                ChannelId = words[1],
                            };
                            result = await SendMessageAsync(join.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 0)
                            {
                                int replyBool = await WaitReply(IMessage.MessageId , options);
                                if (replyBool == -1)
                                {
                                    Console.Error.WriteLine("ERROR: no reply");
                                    await SendBye(udpClient, localEndPoint, options);
                                    return 1;
                                }

                                continue;
                            }
                            else
                            {
                                Console.Error.WriteLine("ERROR: No confirm");
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                        case "/rename":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                                continue;
                            }
                            _displayName = words[1];
                            continue;
                        default:
                            //TODO: проверить состояние отправить сообщение на сервер, проверить 1 символо на / и вывести ошибку
                            if (words[0][0] == '/')
                            {
                                Console.Error.WriteLine("ERROR: Wrong input, repeat");
                                continue;
                            }

                            Msg msg = new Msg()
                            {
                                MessageContents = userInput, //TODO: проверка на то что символы в utf8
                                DisplayName = _displayName
                            };

                            result = await SendMessageAsync(msg.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 0)
                                continue;
                            else
                            {
                                Console.Error.WriteLine("ERROR: No confirm");
                                await SendBye(udpClient, localEndPoint, options);
                                return 1;
                            }
                    }
                case State.ERROR:
                    return 1;
                case State.END:
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
                if (IMessage.MessageId == BitConverter.ToUInt16(message, 1))
                {
                    mutexConfirm.ReleaseMutex();
                    IMessage.MessageId++;
                    return 0;
                }
            }

            mutexConfirm.ReleaseMutex();
        }
        IMessage.MessageId++;
        return 1;
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
        _state = State.END;
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

        return -1; //err
    }
}