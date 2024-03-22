namespace Client;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Sockets;
using System;
using System.Text;

public class TcpCommunication
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
    private static int _reply = -1;
    private static Mutex _mutexReply = new Mutex();
    private static string? _displayName;

    internal static async Task TcpProcessSocketCommunication(Options options)
    {
        TcpClient tcpClient = new TcpClient();
        try
        {
            tcpClient.Connect(options.Ip, options.Port);
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
            tcpClient.Close();
        }
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
    static async Task SendMessages(TcpClient tcpClient)
    {
        NetworkStream stream = tcpClient.GetStream();
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
                    _mutexSate.ReleaseMutex();
                }
                tcpClient.Close();
                Environment.Exit(0);
                return;
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                Console.Error.WriteLine("ERR: Wrong input, repeat");
                continue;
            }

            string[] words = userInput.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

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
            byte[] data;
            _mutexSate.WaitOne();
            switch (_state)
            {
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
                                data = Encoding.UTF8.GetBytes(Join.ToTcpString(join));
                                await SendWithReply(data, tcpClient);
                                continue;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"ERR: {e}");
                                continue;
                            }
                            
                            break;
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
                            break;
                    }
            }   
            // Отправляем сообщение серверу
            //byte[] data = Encoding.UTF8.GetBytes(message);
            //await stream.WriteAsync(data, 0, data.Length);
        }
    }

    static async Task SendWithReply(byte[] data,TcpClient tcpClient)
    {
        await tcpClient.GetStream().WriteAsync(data, 0, data.Length);
        for (int i = 0; i < 10; i++)
        {
            if(_reply==-1)
                Thread.Sleep(50);
                //await Task.Delay(50);
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
    static async Task ReceiveMessages(TcpClient tcpClient)
    {
        NetworkStream stream = tcpClient.GetStream();
        byte[] buffer = new byte[2048]; //1435 max
        while (true)
        {
            // Принимаем ответ от сервера
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
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
                
            string[] messages = receivedMessage.Split("\r\n");

            foreach (string message in messages)
            {
                if(message=="")
                    continue;
                string firstPart;
                string secondPart;
                string[] words;
                int index = message.IndexOf("IS");
                // Если подстрока "IS" найдена
                if (index != -1)
                {
                    // Получаем подстроку до первого вхождения "IS"
                    firstPart = message.Substring(0, index + 2);
                    // Получаем подстроку после первого вхождения "IS"
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
                                        _reply = -1;
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
                                    Console.WriteLine($"$ERR: {e}");
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
                                    Console.WriteLine($"$ERR: {e}");
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
                                        _reply = -1;
                                        Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                    }
                                    _mutexReply.ReleaseMutex();
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"$ERR: {e}");
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
                                    Console.WriteLine($"$ERR: {e}");
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