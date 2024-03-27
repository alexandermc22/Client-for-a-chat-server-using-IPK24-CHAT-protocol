namespace Client;

using System;
using System.Net;
using System.Net.Sockets;
using Messeges;

public class UdpCommunication
{
    private enum State
    {
        START,
        AUTH,
        OPEN,
        ERROR,
        END
    }

    private static Mutex _mutexSate = new Mutex();
    private static State _state = State.START;
    private static Queue<Confirm> _confirmList = new Queue<Confirm>();
    private static Queue<Reply> _replyList = new Queue<Reply>();
    private static Mutex _mutexConfirm = new Mutex();
    private static Mutex _mutexReply = new Mutex();
    private static string? _displayName;

    internal static async Task UdpProcessSocketCommunication(Options options, IPAddress ip)
    {
        IMessage.MessageId = 0;
        UdpClient udpClientSend = new UdpClient();
        IPEndPoint serverEP = new IPEndPoint(ip, options.Port);
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        try
        {
            udpClientSend.Client.Bind(clientEP);
            Task<int> receiveTask = ReceiveMessage(udpClientSend, serverEP, options);
            Task<int> readTask = ReadMessage(options, udpClientSend, serverEP);
            await Task.WhenAny(readTask, receiveTask);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"ERR: {e.Message}");
            throw;
        }
        finally
        {
            udpClientSend.Close();
        }

    }

//00d3554d-30c3-4a47-a37e-7b9032bd27e4
    static async Task<int> ReceiveMessage(UdpClient udpClient, IPEndPoint serverEP, Options options)
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
                                _mutexConfirm.WaitOne();
                                _confirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                                _mutexConfirm.ReleaseMutex();
                                break;
                            case (byte)MessageType.REPLY:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Reply reply = Reply.FromBytes(receiveResult.Buffer);
                                if (reply.Result)
                                    Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                else
                                    Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                _mutexReply.WaitOne();
                                _replyList.Enqueue(reply);
                                _mutexReply.ReleaseMutex();
                                break;
                            case (byte)MessageType.ERR:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Err err = Err.FromBytes(receiveResult.Buffer);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");

                                Task close = SendBye(udpClient, serverEP, options);
                                _ = ReceiveMessage(udpClient, serverEP, options);
                                await close;
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                        }

                        break;


                    case State.OPEN:
                        _mutexSate.ReleaseMutex();
                        switch (receiveResult.Buffer[0])
                        {
                            case (byte)MessageType.CONFIRM:
                                _mutexConfirm.WaitOne();
                                _confirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                                _mutexConfirm.ReleaseMutex();
                                break;
                            case (byte)MessageType.REPLY:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Reply reply = Reply.FromBytes(receiveResult.Buffer);
                                if (reply.Result)
                                    Console.Error.WriteLine($"Success: {reply.MessageContent}\n");
                                else
                                    Console.Error.WriteLine($"Failure: {reply.MessageContent}\n");
                                _mutexReply.WaitOne();
                                _replyList.Enqueue(Reply.FromBytes(receiveResult.Buffer));
                                _mutexReply.ReleaseMutex();
                                break;
                            case (byte)MessageType.ERR:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Err err = Err.FromBytes(receiveResult.Buffer);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");

                                Task close = SendBye(udpClient, serverEP, options);
                                _ = ReceiveMessage(udpClient, serverEP, options);
                                await close;
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                            case (byte)MessageType.MSG:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                Msg message = Msg.FromBytes(receiveResult.Buffer);
                                Console.WriteLine($"{message.DisplayName}: {message.MessageContents}\n");
                                break;
                            case (byte)MessageType.BYE:
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                            default:
                                Console.WriteLine("ERR: wrong data from server");
                                Err sendErr = new Err();
                                sendErr.MessageContents = "wrong data from server";
                                sendErr.DisplayName = _displayName;
                                _mutexSate.WaitOne();
                                _state = State.ERROR;
                                _mutexSate.ReleaseMutex();
                                Task sendError = SendMessageAsync(sendErr.ToBytes(), udpClient, serverEP, options);
                                _ = ReceiveMessage(udpClient, serverEP, options);
                                await sendError;
                                Task close1 = SendBye(udpClient, serverEP, options);
                                await close1;
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                        }

                        break;
                    case State.ERROR:
                    case State.END:
                        _mutexSate.ReleaseMutex();
                        if (receiveResult.Buffer[0] == (byte)MessageType.CONFIRM)
                        {
                            _mutexConfirm.WaitOne();
                            _confirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                            _mutexConfirm.ReleaseMutex();
                        }

                        break;
                }
            }
        }
        catch (ArgumentException)
        {
            Console.WriteLine("ERR: ArgumentException");
            throw;
        }
    }

    static async Task<int> ReadMessage(Options options, UdpClient udpClient, IPEndPoint localEndPoint)
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

            udpClient.Close();

            Environment.Exit(0);
        };
        while (true)
        {
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

            string[] words = userInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

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
                                int replyBool =  WaitReply(options);
                                if (replyBool == 1)
                                {
                                    _mutexSate.WaitOne();
                                    _state = State.OPEN;
                                    _mutexSate.ReleaseMutex();
                                }
                                else if (replyBool == -1)
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    udpClient.Close();
                                    Environment.Exit(0);
                                    return 1;
                                }

                                continue;
                            }
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                udpClient.Close();
                                Environment.Exit(0);
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
                                int replyBool =  WaitReply(options);
                                if (replyBool == -1)
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    udpClient.Close();
                                    Environment.Exit(0);
                                    return 1;
                                }

                                continue;
                            }
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                udpClient.Close();
                                Environment.Exit(0);
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
                            if (words[0][0] == '/')
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }

                            Msg msg = new Msg()
                            {
                                MessageContents = userInput,
                                DisplayName = _displayName
                            };

                            result = await SendMessageAsync(msg.ToBytes(), udpClient, localEndPoint, options);
                            if (result == 1)
                                continue;
                            else
                            {
                                await SendBye(udpClient, localEndPoint, options);
                                udpClient.Close();
                                Environment.Exit(0);
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
            Thread.Sleep(options.UdpTimeout);
            _mutexConfirm.WaitOne();
            while (_confirmList.Count > 0)
            {
                Confirm confirm = _confirmList.Dequeue();
                if (confirm.MessageId == BitConverter.ToUInt16(message, 1))
                {
                    _mutexConfirm.ReleaseMutex();
                    IMessage.MessageId++;
                    return 1;
                }
            }

            _mutexConfirm.ReleaseMutex();
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

    static async Task SendBye(UdpClient udpClient, IPEndPoint localEndPoint, Options options)
    {
        Bye bye = new Bye();
        _mutexSate.WaitOne();
        _state = State.END;
        _mutexSate.ReleaseMutex();
        await SendMessageAsync(bye.ToBytes(), udpClient, localEndPoint, options);
    }

    static int WaitReply(Options options)
    {
        for (int i = 0; i < options.MaxRetries; i++)
        {
            Thread.Sleep(options.UdpTimeout);
            _mutexReply.WaitOne();
            while (_replyList.Count > 0)
            {
                Reply reply;
                reply = _replyList.Dequeue();
                if (reply.RefMessageId == (IMessage.MessageId - 1))
                {
                    _mutexReply.ReleaseMutex();
                    if (reply.Result)
                        return 1; //true
                    else
                        return 0; //false
                }
            }

            _mutexReply.ReleaseMutex();
        }

        Console.Error.WriteLine("ERR: no reply");
        return -1; //err
    }
}