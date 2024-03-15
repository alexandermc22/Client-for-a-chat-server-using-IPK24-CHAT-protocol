namespace Client;
using System.Text;
public class Bye: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.BYE;
    //public static ushort MessageId { get; set; }
    
    public byte[] ToBytes()
    {
        // Создаем массив для объединения всех байтов
        byte[] result = new byte[1 + 2];

        // Используем приведение enum к byte для преобразования MessageType в байт
        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        return result;
    }
}