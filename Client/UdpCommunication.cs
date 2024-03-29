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

    // shared variable and mutex for communication between Tasks
    private static Mutex _mutexSate = new Mutex();
    private static State _state = State.START;
    // if we receive confirm or reply we add instance in queue
    private static Queue<Confirm> _confirmList = new Queue<Confirm>();
    private static Queue<Reply> _replyList = new Queue<Reply>();
    
    private static Mutex _mutexConfirm = new Mutex();
    private static Mutex _mutexReply = new Mutex();
    private static string? _displayName;
    public static ushort id = 0;
    internal static async Task UdpProcessSocketCommunication(Options options, IPAddress ip)
    {
        UdpClient udpClientSend = new UdpClient();
        // server point
        IPEndPoint serverEP = new IPEndPoint(ip, options.Port);
        // local point
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
                // Receive a response from the server
                UdpReceiveResult receiveResult = await udpClient.ReceiveAsync();

                
                _mutexSate.WaitOne();
                switch (_state)
                {
                    // no action if we in state start
                    case State.START:
                        _mutexSate.ReleaseMutex();
                        break;
                    
                    // state start and auth are merged in this method
                    // but in open start we dont receive message, bye and default
                    case State.AUTH:
                    case State.OPEN:
                        if ((receiveResult.RemoteEndPoint.Port != serverEP.Port) && (_state== State.AUTH))
                        {
                            serverEP.Port = receiveResult.RemoteEndPoint.Port;
                        }
                        _mutexSate.ReleaseMutex();
                        switch (receiveResult.Buffer[0])
                        {
                            case (byte)MessageType.CONFIRM:
                                _mutexConfirm.WaitOne();
                                _confirmList.Enqueue(Confirm.FromBytes(receiveResult.Buffer));
                                _mutexConfirm.ReleaseMutex();
                                break;
                            case (byte)MessageType.REPLY:
                                // send confirm to server
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                
                                // print response
                                Reply reply = Reply.FromBytes(receiveResult.Buffer);
                                if (reply.Result)
                                    Console.Error.WriteLine($"Success: {reply.MessageContent}\n");
                                else
                                    Console.Error.WriteLine($"Failure: {reply.MessageContent}\n");
                                
                                // add to queue
                                _mutexReply.WaitOne();
                                _replyList.Enqueue(Reply.FromBytes(receiveResult.Buffer));
                                _mutexReply.ReleaseMutex();
                                break;
                            
                            case (byte)MessageType.ERR:
                                
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                
                                // print Error
                                Err err = Err.FromBytes(receiveResult.Buffer);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");

                                // send bye to server
                                Task close = SendBye(udpClient, serverEP, options);
                                
                                // recursive call because we need to receive confirm
                                _ = ReceiveMessage(udpClient, serverEP, options);
                                await close;
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                            
                            case (byte)MessageType.MSG:
                                if(_state==State.AUTH)
                                    break;
                                
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                
                                // print message
                                Msg message = Msg.FromBytes(receiveResult.Buffer);
                                Console.WriteLine($"{message.DisplayName}: {message.MessageContents}\n");
                                
                                break;
                            
                            case (byte)MessageType.BYE:
                                if(_state==State.AUTH)
                                    break;
                                _ = SendConfirm(receiveResult.Buffer, udpClient, serverEP);
                                udpClient.Close();
                                Environment.Exit(0);
                                return 0;
                            default:
                                if(_state==State.AUTH)
                                    break;
                                
                                // send error
                                Console.WriteLine("ERR: wrong data from server");
                                Err sendErr = new Err();
                                sendErr.MessageContents = "wrong data from server";
                                sendErr.DisplayName = _displayName;
                                _mutexSate.WaitOne();
                                _state = State.ERROR;
                                _mutexSate.ReleaseMutex();
                                Task sendError = SendMessageAsync(sendErr.ToBytes(id), udpClient, serverEP, options);
                                
                                // recursive call because we need to receive confirm
                                _ = ReceiveMessage(udpClient, serverEP, options);
                                await sendError;
                                
                                // send bye
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
                        
                        //receive confirm only
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
        
        // processing cansel key at ctrl c cmd c 
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
            // read string from stdin
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
            
            // split string
            string[] words = userInput.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                Console.Error.WriteLine("ERR: Wrong input, repeat");
                continue;
            }
            
            // print help message
            if (words[0] == "/help")
            {
                Options.PrintHelp();
                continue;
            }

            int result;
            _mutexSate.WaitOne();
            switch (_state)
            {
                
                // state start and auth are merged in this method
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
                            
                            // send auth and wait reply and confirm
                            result = await SendMessageAsync(auth.ToBytes(id), udpClient, localEndPoint, options);
                            if (result == 1) // if we receive confirm
                            {
                                int replyBool =  WaitReply(options);
                                if (replyBool == 1) // if reply is OK
                                {
                                    _mutexSate.WaitOne();
                                    _state = State.OPEN;
                                    _mutexSate.ReleaseMutex();
                                }
                                else if (replyBool == -1) // if no reply
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    udpClient.Close();
                                    Environment.Exit(0);
                                    return 1;
                                }

                                continue;
                            }
                            else // if no confirm
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
                            if (words[1].Length > 20)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            Join join = new Join()
                            {
                                ChannelId = words[1],
                                DisplayName = _displayName
                            };
                            // send join
                            result = await SendMessageAsync(join.ToBytes(id), udpClient, localEndPoint, options);
                            if (result == 1) // if we receive confirm
                            {
                                int replyBool =  WaitReply(options);
                                if (replyBool == -1) // if no reply
                                {
                                    await SendBye(udpClient, localEndPoint, options);
                                    udpClient.Close();
                                    Environment.Exit(0);
                                    return 1;
                                }
                                //else nothing to do

                                continue;
                            }
                            else // if no confirm
                            { 
                                await SendBye(udpClient, localEndPoint, options);
                                udpClient.Close();
                                Environment.Exit(0);
                                return 1;
                            }
                        
                        //change local name
                        case "/rename":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            if (words[1].Length > 20)
                            {
                                Console.Error.WriteLine("ERR: Name must not contain more then 20 symbols");
                                continue;
                            }

                            _displayName = words[1];
                            continue;
                        
                        default:
                            // if it is command print Err
                            if (words[0][0] == '/')
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            if (userInput.Length > 1400)
                            {
                                Console.Error.WriteLine("ERR: Message must not contain more then 1400 symbols");
                                continue;
                            }
                            if (_displayName.Length > 20)
                            {
                                Console.Error.WriteLine("ERR: Name must not contain more then 20 symbols");
                                continue;
                            }
                            // else send message
                            Msg msg = new Msg()
                            {
                                MessageContents = userInput,
                                DisplayName = _displayName
                            };

                            result = await SendMessageAsync(msg.ToBytes(id), udpClient, localEndPoint, options);
                            if (result == 1) // if we receive confirm
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
        // send message to server
        await udpClient.SendAsync(message, message.Length, localEndPoint);

        // wait confirm 
        for (int i = 0; i < options.MaxRetries; i++)
        {
            Thread.Sleep(options.UdpTimeout);
            _mutexConfirm.WaitOne();
            while (_confirmList.Count > 0) // use shared queue to receive confirm
            {
                Confirm confirm = _confirmList.Dequeue();
                if (confirm.MessageId == BitConverter.ToUInt16(message, 1))
                {
                    _mutexConfirm.ReleaseMutex();
                    id++; // increment id
                    return 1;
                }
            }

            _mutexConfirm.ReleaseMutex();
        }
        // if no response
        id++;
        Console.Error.WriteLine("ERR: No confirm");
        return 0;
    }

    // method to send confirm
    static async Task SendConfirm(byte[] message, UdpClient udpClient, IPEndPoint localEndPoint)
    {
        Confirm confirm = new Confirm();
        confirm.MessageId = BitConverter.ToUInt16(message, 1);
        byte[] confirmByte = confirm.ToBytes(id);
        await udpClient.SendAsync(confirmByte, confirmByte.Length, localEndPoint);
    }

    // method to send bye
    static async Task SendBye(UdpClient udpClient, IPEndPoint localEndPoint, Options options)
    {
        _mutexSate.WaitOne();
        _state = State.END;
        _mutexSate.ReleaseMutex();
        Bye bye = new Bye();
        await SendMessageAsync(bye.ToBytes(id), udpClient, localEndPoint, options);
    }

    // method to wait reply from server
    static int WaitReply(Options options)
    {
        for (int i = 0; i < options.MaxRetries; i++)
        {
            Thread.Sleep(options.UdpTimeout);
            _mutexReply.WaitOne();
            while (_replyList.Count > 0) // use shared queue 
            {
                Reply reply;
                reply = _replyList.Dequeue(); 
                if (reply.RefMessageId == (id - 1))
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
        
        // if no reply
        Console.Error.WriteLine("ERR: no reply");
        return -1; //err
    }
}