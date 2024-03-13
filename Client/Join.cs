namespace Client;
using System.Text;
public class Join : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.JOIN;
    //public static ushort MessageId { get; set; }
    public required string ChannelId  { get; set; }
    public  string DisplayName    { get; set; } 
    
    public byte[] ToBytes()
    {
        // Получаем байты для каждого поля
        // byte[] messageTypeBytes = BitConverter.GetBytes(MessageType);
        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        byte[] channelIdBytes = Encoding.UTF8.GetBytes(ChannelId);
        byte[] displayNameBytes = Encoding.UTF8.GetBytes(DisplayName);

        // Создаем массив для объединения всех байтов
        byte[] result = new byte[1 + 2 + channelIdBytes.Length + 1 + displayNameBytes.Length + 1];

        // Копируем байты в результирующий массив
        int offset = 0;

        // MessageType (1 byte)
        result[offset] = (byte)MessageType;
        offset += 1;

        // MessageId (2 bytes)
        Array.Copy(messageIdBytes, 0, result, offset, 2);
        offset += 2;

        // ChannelId (variable length)
        Array.Copy(channelIdBytes, 0, result, offset, channelIdBytes.Length);
        offset += channelIdBytes.Length;

        // Null terminator after ChannelId (1 byte)
        result[offset] = 0;
        offset += 1;

        // DisplayName (variable length)
        Array.Copy(displayNameBytes, 0, result, offset, displayNameBytes.Length);
        offset += displayNameBytes.Length;

        // Null terminator after DisplayName (1 byte)
        result[offset] = 0;

        return result;
    }
}