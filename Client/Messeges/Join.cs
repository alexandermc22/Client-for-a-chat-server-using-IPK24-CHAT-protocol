namespace Client;
using System.Text;
using System.Text.RegularExpressions;
public class Join : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.JOIN;
    //public static ushort MessageId { get; set; }
    public required string ChannelId  { get; set; }
    public  string DisplayName    { get; set; } 
    
    
    public static string ToTcpString(Join join)
    {
        Exception ex = new Exception("Wrong input data");
        // Проверяем длину идентификатора канала и названия канала
        if (join.ChannelId.Length > 20 || join.DisplayName.Length > 20)
        {
            throw new ArgumentException("Channel ID and Display Name cannot exceed 20 characters in length.");
        }
        string patternId = @"^[a-zA-Z0-9\-]+$";
        if (!Regex.IsMatch(join.ChannelId, patternId))
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(join.DisplayName, patternDname))
            throw ex;
        // Строим строку в формате "JOIN SP ID SP AS SP DNAME \r\n"
        return string.Format("JOIN {0} AS {1}\r\n", join.ChannelId, join.DisplayName);
    }
    
    public static Join FromStringTcp(string[] words)
    {
        Exception ex = new Exception("Wrong data from server");
        if (words.Length != 4 )
            throw ex;
        if (words[1].Length > 20)
            throw ex;
        string patternId = @"^[a-zA-Z0-9\-]+$";
        if (!Regex.IsMatch(words[1], patternId))
            throw ex;
        
        
        if(words[2]!="AS")
            throw ex;
        
        if (words[3].Length > 20)
            throw ex;
        
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(words[3], patternDname))
            throw ex;
        
        Join join = new Join()
        {
            DisplayName = words[3],
            ChannelId = words[1]
        };
        return join;
    }
    
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