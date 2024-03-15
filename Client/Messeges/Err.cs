namespace Client;
using System.Text;
public class Err: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.ERR;
    //public static ushort MessageId { get; set; }
    public  string DisplayName { get; set; } 
    public  string MessageContents { get; set; } 
    
    public byte[] ToBytes()
    {
        byte[] displayNameBytes = Encoding.UTF8.GetBytes(DisplayName);
        byte[] messageContentsBytes = Encoding.UTF8.GetBytes(MessageContents);

        // Создаем массив для объединения всех байтов
        byte[] result = new byte[1 + 2 + displayNameBytes.Length + 1 + messageContentsBytes.Length + 1];

        // Используем приведение enum к byte для преобразования MessageType в байт
        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        int offset = 3;

        Array.Copy(displayNameBytes, 0, result, offset, displayNameBytes.Length);
        offset += displayNameBytes.Length;
        result[offset++] = 0; // Null terminator after DisplayName

        Array.Copy(messageContentsBytes, 0, result, offset, messageContentsBytes.Length);
        offset += messageContentsBytes.Length;
        result[offset] = 0; // Null terminator after MessageContents

        return result;
    }
    public static Err FromBytes(byte[] data)
    {
        if (data == null || data.Length < 3)
        {
            // Некорректные данные для разбора
            return null;
        }

        Err errMessage = new Err();

        // Извлекаем MessageType из первого байта
        errMessage.MessageType = (MessageType)data[0];

        // Извлекаем MessageId из следующих двух байтов
        IMessage.MessageId = BitConverter.ToUInt16(data, 1);

        // Пропускаем байт 3, так как в оригинальном массиве это 0x00 после MessageId

        // Извлекаем DisplayName, предполагая, что он завершается байтом 0x00
        int offset = 4;
        List<byte> displayNameBytes = new List<byte>();
        while (offset < data.Length && data[offset] != 0)
        {
            displayNameBytes.Add(data[offset]);
            offset++;
        }
        errMessage.DisplayName = Encoding.UTF8.GetString(displayNameBytes.ToArray());

        // Пропускаем байт 0x00 после DisplayName
        offset++;

        // Извлекаем MessageContents, предполагая, что он завершается байтом 0x00
        List<byte> messageContentsBytes = new List<byte>();
        while (offset < data.Length && data[offset] != 0)
        {
            messageContentsBytes.Add(data[offset]);
            offset++;
        }
        errMessage.MessageContents = Encoding.UTF8.GetString(messageContentsBytes.ToArray());

        return errMessage;
    }
}