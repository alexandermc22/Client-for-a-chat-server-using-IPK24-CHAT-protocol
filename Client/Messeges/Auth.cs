namespace Client;
using System.Text;
using System.Text.RegularExpressions;
public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    //public static ushort MessageId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Secret { get; set; }

    
    public string ToTcpString( )
    {
        Exception ex = new Exception("Wrong input data");
        // Проверяем длину идентификатора канала и названия канала
        if (Username.Length > 20 || DisplayName.Length > 20 || Secret.Length>128)
        {
            throw new ArgumentException("Channel ID and Display Name cannot exceed 20 characters in length.");
        }
        string patternId = @"^[a-zA-Z0-9\-]+$";
        if (!Regex.IsMatch(Username, patternId))
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(DisplayName, patternDname))
            throw ex;
        if (!Regex.IsMatch(Secret, patternId))
            throw ex;
        // Строим строку в формате "JOIN SP ID SP AS SP DNAME \r\n"
        return string.Format("AUTH {0} AS {1} USING {2}\r\n", Username, DisplayName, Secret );
    }
    
    
    
    public byte[] ToBytes()
    {
        byte[] usernameBytes = Encoding.UTF8.GetBytes(Username);
        byte[] displayNameBytes = Encoding.UTF8.GetBytes(DisplayName);
        byte[] secretBytes = Encoding.UTF8.GetBytes(Secret);

        // Создаем массив для объединения всех байтов
        byte[] result = new byte[1 + 2 + usernameBytes.Length + 1 + displayNameBytes.Length + 1 + secretBytes.Length + 1];

        // Используем приведение enum к byte для преобразования MessageType в байт
        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        int offset = 3;

        Array.Copy(usernameBytes, 0, result, offset, usernameBytes.Length);
        offset += usernameBytes.Length;
        result[offset] = 0; // Null terminator after Username
        offset++;
        Array.Copy(displayNameBytes, 0, result, offset, displayNameBytes.Length);
        offset += displayNameBytes.Length;
        result[offset] = 0; // Null terminator after DisplayName
        offset++;
        Array.Copy(secretBytes, 0, result, offset, secretBytes.Length);
        offset += secretBytes.Length;
        result[offset] = 0; // Null terminator after Secret

        return result;
    }
}