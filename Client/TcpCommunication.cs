namespace Client;

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

    internal static async Task TcpProcessSocketCommunication(Options options, IPAddress ip)
    {
        TcpClient tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(ip, options.Port);
            Task receiveTask = ReceiveMessages(tcpClient);
            Task sendMessage = SendMessages(tcpClient);

            await Task.WhenAll(receiveTask, sendMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERR: {ex.Message}");
        }
        finally
        {
            tcpClient.Close();
        }
    }

    static async Task SendMessages(TcpClient tcpClient)
    {
        Console.CancelKeyPress += async (sender, e) =>
        {
            _mutexSate.WaitOne();
            if (_state != State.START)
            {
                _mutexSate.ReleaseMutex();
                //TODO: SEND BYE             
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
                continue;
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

            // Отправляем сообщение серверу
            //byte[] data = Encoding.UTF8.GetBytes(message);
            //await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine("Сообщение отправлено серверу.");
        }
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
                return;
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            string[] messages = receivedMessage.Split("\r\n");

            foreach (string message in messages)
            {
                string firstPart;
                string secondPart;
                string[] words;
                int index = message.IndexOf("IS");
                // Если подстрока "IS" найдена
                if (index != -1)
                {
                    // Получаем подстроку до первого вхождения "IS"
                    firstPart = message.Substring(0, index + 3);
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
                                _mutexReply.WaitOne();
                                _reply = 1;
                                _mutexReply.ReleaseMutex();
                                try
                                {
                                    Reply reply = Reply.FromStringTcp(words);
                                    if (reply.Result)
                                        Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                    else
                                        Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"$ERR: {e}");
                                    //TODO: SEND BYE
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                    return;
                                }
                            case "BYE":
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                            case "ERR":
                                Err err = Err.FromStringTcp(words);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                        }
                        break;
                    case State.OPEN:
                        switch (words[0])
                        {
                            case "MSG":
                                Msg msg = Msg.FromStringTcp(words);
                                Console.WriteLine($"{msg.DisplayName}: {msg.MessageContents}\n");
                                break;
                            case "REPLY":
                                _mutexReply.WaitOne();
                                _reply = 1;
                                _mutexReply.ReleaseMutex();
                                try
                                {
                                    Reply reply = Reply.FromStringTcp(words);
                                    if (reply.Result)
                                        Console.Error.WriteLine($"Success: {reply.MessageContent}");
                                    else
                                        Console.Error.WriteLine($"Failure: {reply.MessageContent}");
                                    break;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"$ERR: {e}");
                                    //TODO: SEND BYE
                                    tcpClient.Close();
                                    Environment.Exit(0);
                                    return;
                                }
                            case "BYE":
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                            case "ERR":
                                Err err = Err.FromStringTcp(words);
                                Console.Error.WriteLine($"ERR FROM {err.DisplayName}: {err.MessageContents}");
                                tcpClient.Close();
                                Environment.Exit(0);
                                return;
                            default:
                                Console.WriteLine("ERR: wrong data from server");
                                // TODO: send err, send bye
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