namespace Client;
using System.Text;
public class Confirm : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.CONFIRM;
    public ushort MessageId { get; set; }
    public byte[] ToBytes()
        {
            // Создаем массив для объединения всех байтов
            byte[] result = new byte[1 + 2];
    
            // Используем приведение enum к byte для преобразования MessageType в байт
            result[0] = (byte)MessageType;
    
            byte[] messageIdBytes = BitConverter.GetBytes(MessageId);
            Array.Copy(messageIdBytes, 0, result, 1, 2);
    
            return result;
        }
    public static Confirm FromBytes(byte[] data)
    {
        if (data == null || data.Length < 3)
        {
            throw new ArgumentException("Invalid data array", nameof(data));
        }

        MessageType messageType = (MessageType)data[0];
        ushort refMessageId = BitConverter.ToUInt16(data, 1);

        return new Confirm
        {
            MessageType = messageType,
            MessageId = refMessageId
        };
    }
}