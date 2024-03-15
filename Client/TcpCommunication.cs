namespace Client;
using System.Net;
using System.Net.Sockets;
using System;
using System.Text;
public class TcpCommunication
{
    internal static async Task TcpProcessSocketCommunication(Options options, IPAddress ip)
    {

        TcpClient tcpClient = new TcpClient();
        try
        {
            // Подключаемся к серверу
            await tcpClient.ConnectAsync(ip, options.Port);


            // Запускаем асинхронный метод для приема сообщений
            Task receiveTask = ReceiveMessages(tcpClient);

            // Отправляем сообщения в параллельном потоке
            await SendMessages(tcpClient);

            Task result = await Task.WhenAny(SendMessages, ReceiveMessages);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            // Закрываем соединение с сервером
            tcpClient.Close();
            Console.WriteLine("Соединение с сервером закрыто.");
        }
    }
    
    static async Task SendMessages(TcpClient tcpClient)
    {
        while (true)
        {
            // Считываем сообщение с консоли
            Console.Write("Введите сообщение для отправки серверу (для завершения введите пустую строку): ");
            string message = Console.ReadLine();

            // Если сообщение пустое, прерываем отправку
            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Принудительное завершение отправки.");
                break;
            }

            // Отправляем сообщение серверу
            byte[] data = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(data, 0, data.Length);
            Console.WriteLine("Сообщение отправлено серверу.");
        }
    }
    static async Task ReceiveMessages(TcpClient tcpClient)
    {
        byte[] buffer = new byte[1024];
        while (true)
        {
            // Принимаем ответ от сервера
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Получен ответ от сервера: {receivedMessage}");
        }
    }
}