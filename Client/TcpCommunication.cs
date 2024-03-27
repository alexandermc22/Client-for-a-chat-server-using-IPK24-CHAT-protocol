namespace Client;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using System;
using System.Text;
using Messeges;
public class TcpCommunication
{
    public enum State
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
    private static int _reply = -1;
    private static Mutex _mutexReply = new Mutex();
    private static string? _displayName;

    internal static async Task TcpProcessSocketCommunication(Options options)
    {
        // try to connect to server
        TcpClient tcpClient = new TcpClient();
        try
        {
            tcpClient.Connect(options.Ip, options.Port);
            // run to Tasks to receive and send messages
            Task receiveTask = ReceiveMessages(tcpClient);
            Task sendMessage = SendMessages(tcpClient);

            await Task.WhenAll(receiveTask, sendMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine("err");
            Console.Error.WriteLine($"ERR: {ex.Message}");
        }
        finally
        {
            // for safety, the code is expected to expire early
            tcpClient.Close();
        }
    }


    static async Task SendMessages(TcpClient tcpClient)
    {
        NetworkStream stream = tcpClient.GetStream();
            // processing cansel key at ctrl c cmd c 
        Console.CancelKeyPress += async (sender, e) =>
        {
            _mutexSate.WaitOne();
            if (_state != State.START)
            {
                _mutexSate.ReleaseMutex();
                await SendBye(tcpClient);
            }
            else
            {
                _mutexSate.ReleaseMutex();
            }
            tcpClient.Close();
            Environment.Exit(0);
        };
        
        while (true)
        {
            // read string from stdin
            string? userInput = Console.ReadLine();
            if (userInput == null)
            {
                
                _mutexSate.WaitOne();
                if (_state != State.START)
                {
                    _mutexSate.ReleaseMutex();
                    await SendBye(tcpClient);
                }
                else
                {
                    // in our FSM we dont need to send Bye if it is Start state
                    _mutexSate.ReleaseMutex();
                }
                tcpClient.Close();
                Environment.Exit(0);
                return;
            }
            // processing empty input
            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.Error.WriteLine("ERR: Wrong input, repeat");
                continue;
            }
            // split input
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
            
            byte[] data; // array for trnsformed data
            _mutexSate.WaitOne();
            switch (_state)
            {
                // state start and auth are merged in this method
                case State.START:
                case State.AUTH:
                    _mutexSate.ReleaseMutex();
                    
                    
                    if (words[0] == "/auth")
                    {
                        if (words.Length != 4)
                        {
                            Console.Error.WriteLine("ERR: Wrong input, repeat");
                            continue;
                        }

                        _displayName = words[3];
                        // create instance auth 
                        Auth auth = new Auth()
                        {
                            Username = words[1],
                            DisplayName = words[3],
                            Secret = words[2]
                        };
                        _mutexSate.WaitOne();
                        _state = State.AUTH;
                        _mutexSate.ReleaseMutex();
                        try
                        {
                            // create bytes from string and send to server
                            data = Encoding.UTF8.GetBytes(auth.ToTcpString());
                            await SendWithReply(data, tcpClient);
                            continue;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"ERR: {e}");
                            continue;
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("ERR: Wrong input, repeat");
                        continue;
                    }
                case State.OPEN:
                    _mutexSate.ReleaseMutex();
                    // use switch because we have enough case 
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
                            try
                            {
                                // create bytes from string and send to server
                                data = Encoding.UTF8.GetBytes(Join.ToTcpString(join));
                                await SendWithReply(data, tcpClient);
                                continue;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"ERR: {e}");
                                continue;
                            }
                        // check new name and local change name
                        case "/rename":
                            if (words.Length != 2)
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            string patternDname = @"^[\x20-\x7E]*$";
                            if (Regex.IsMatch(words[1], patternDname))
                                _displayName = words[1];
                            else
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                            continue;
                        
                        default:
                            //check if it is a some command print err message
                            if (words[0][0] == '/')
                            {
                                Console.Error.WriteLine("ERR: Wrong input, repeat");
                                continue;
                            }
                            // else send message to server
                            Msg msg = new Msg()
                            {
                                MessageContents = userInput, 
                                DisplayName = _displayName
                            };
                            try
                            {
                                data = Encoding.UTF8.GetBytes(Msg.ToTcpString(msg));
                                await stream.WriteAsync(data, 0, data.Length);
                                continue;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"ERR: {e}");
                                continue;
                            }
                    }
            }   
        }
    }

    
    // help Method to send message and wait reply
    static async Task SendWithReply(byte[] data,TcpClient tcpClient)
    {
        await tcpClient.GetStream().WriteAsync(data, 0, data.Length);
        // if server is busy or dont work we have 10 attempts to response reply message 
        for (int i = 0; i < 20; i++)
        {
            if(_reply==-1)
                Thread.Sleep(100);
            else
            {
                if (_reply == 1)
                {
                    _mutexSate.WaitOne();
                    _state = State.OPEN;
                    _mutexSate.ReleaseMutex();
                }
                _mutexReply.WaitOne();
                _reply = -1;
                _mutexReply.ReleaseMutex();
                return;
            }
        }
        Console.Error.WriteLine("ERR: no response");
        tcpClient.Close();
        Environment.Exit(0);
    }

    static async Task SendBye(TcpClient tcpClient)
    {
        byte[] data = Encoding.UTF8.GetBytes("BYE\r\n");
        await tcpClient.GetStream().WriteAsync(data, 0, data.Length);
    }
    
    static async Task SendErr(TcpClient tcpClient)
    {
        Err err = new Err()
        {
            DisplayName = _displayName,
            MessageContents = "Wrong data from server"
        };
        byte[] data = Encoding.UTF8.GetBytes(Err.ToTcpString(err));
        await tcpClient.GetStream().WriteAsync(data, 0, data.Length);
    }
    
    static async Task ReceiveMessages(TcpClient tcpClient)
    {
        NetworkStream stream = tcpClient.GetStream();
        byte[] buffer = new byte[2048]; //1435 max expected length 
        while (true)
        {
            // Receive a response from the server
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            // if server send 0 byte or empty string its mean server close connection 
            if (bytesRead == 0)
            {
                tcpClient.Close();
                Environment.Exit(0);
                return;
            }
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            if (receivedMessage == "")
            {
                await SendBye(tcpClient);
                tcpClient.Close();
                Environment.Exit(0);
            }
            // if in buffer will be 2 little messages we split it
            string[] messages = receivedMessage.Split("\r\n");
            foreach (string message in messages)
            {
                if(message=="")
                    continue;
                
                string firstPart;
                string secondPart;
                string[] words;
                int index = message.IndexOf("IS", StringComparison.Ordinal);
                // If substring "IS" is found
                if (index != -1)
                {
                    // Get the substring up to the first occurrence of "IS"
                    firstPart = message.Substring(0, index + 2);
                    // Get the substring after the first occurrence of "IS"
                    secondPart = message.Substring(index + 3);
                    words = firstPart.Split(' ');
                    Array.Resize(ref words, words.Length + 1);
                    words[words.Length - 1] = secondPart;
                }
                else
                {
                    words = message.Split(' ');
                }

                _mutexSate.WaitOne();
                switch (_state)
                {
                    case State.START:
                        _mutexSate.ReleaseMutex();
                        break;
                    case State.AUTH:
                        _mutexSate.ReleaseMutex();
                        switch (words[0])
                        {
                            case "REPLY":
                                try
                                {
                                    Reply reply = Reply.FromStringTcp(words);
                                    _mutexReply.WaitOne();
                                    if (reply.Result)
                                    {
                                        _reply = 1;
                                        Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                    }
                                    else
                                    {
                                        _reply = 0;
                                        Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                    }
                                    _mutexReply.ReleaseMutex();
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"ERR: {e}");
                                    await SendErr(tcpClient);
                                    await SendBye(tcpClient);
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                    return;
                                }
                            case "BYE":
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                            case "ERR":
                                try
                                {
                                    Err err = Err.FromStringTcp(words);
                                    Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"ERR: {e}");
                                    await SendErr(tcpClient);
                                }
                                finally
                                {
                                    await SendBye(tcpClient);
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                }
                                return;
                        }
                        break;
                    case State.OPEN:
                        _mutexSate.ReleaseMutex();
                        switch (words[0])
                        {
                            case "MSG":
                                try
                                {
                                    Msg msg = Msg.FromStringTcp(words);
                                    Console.WriteLine($"{msg.DisplayName}: {msg.MessageContents}\n");
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"ERR: {e}");
                                    await SendErr(tcpClient);
                                    await SendBye(tcpClient);
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                    return;
                                }
                                
                            case "REPLY":
                                try
                                {
                                    Reply reply = Reply.FromStringTcp(words);
                                    _mutexReply.WaitOne();
                                    if (reply.Result)
                                    {
                                        _reply = 1;
                                        Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                    }
                                    else
                                    {
                                        _reply = 0;
                                        Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                    }
                                    _mutexReply.ReleaseMutex();
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"ERR: {e}");
                                    await SendErr(tcpClient);
                                    await SendBye(tcpClient);
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                    return;
                                }
                            case "BYE":
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                            case "ERR":
                                try
                                {
                                    Err err = Err.FromStringTcp(words);
                                    Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"ERR: {e}");
                                    await SendErr(tcpClient);
                                }
                                finally
                                {
                                    await SendBye(tcpClient);
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                }
                                return;
                            default:
                                Console.WriteLine(words[0]);
                                Console.WriteLine("ERR: wrong data from server");
                                await SendErr(tcpClient);
                                await SendBye(tcpClient);
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                        }
                        break;
                    case State.ERROR:
                        return;
                    case State.END:
                        return;
                }  
            }
        }
    }
}