namespace Client;
using System.Text;
public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    //public static ushort MessageId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Secret { get; set; }

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
        result[offset++] = 0; // Null terminator after Username

        Array.Copy(displayNameBytes, 0, result, offset, displayNameBytes.Length);
        offset += displayNameBytes.Length;
        result[offset++] = 0; // Null terminator after DisplayName

        Array.Copy(secretBytes, 0, result, offset, secretBytes.Length);
        offset += secretBytes.Length;
        result[offset] = 0; // Null terminator after Secret

        return result;
    }
}