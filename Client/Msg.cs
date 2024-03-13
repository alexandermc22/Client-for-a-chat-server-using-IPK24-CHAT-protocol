namespace Client;
using System.Text;
public class Msg: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.MSG;
    //public static ushort MessageId { get; set; }
    public string DisplayName { get; set; } 
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
    public static Msg FromBytes(byte[] data)
    {
        Msg msg = new Msg();

        // Проверка длины массива, чтобы избежать выхода за границы
        if (data.Length >= 3)
        {
            msg.MessageType = (MessageType)data[0];

            // Получение MessageId из массива байтов
            IMessage.MessageId = BitConverter.ToUInt16(data, 1);

            // Используем Encoding.UTF8.GetString для извлечения строк из массива байтов
            int offset = 3;

            // DisplayName
            int displayNameLength = Array.IndexOf<byte>(data, 0, offset) - offset;
            if (displayNameLength >= 0)
            {
                msg.DisplayName = Encoding.UTF8.GetString(data, offset, displayNameLength);
                offset += displayNameLength + 1; // Переходим к следующему байту после нулевого терминатора
            }

            // MessageContents
            int messageContentsLength = Array.IndexOf<byte>(data, 0, offset) - offset;
            if (messageContentsLength >= 0)
            {
                msg.MessageContents = Encoding.UTF8.GetString(data, offset, messageContentsLength);
                //offset += messageContentsLength + 1; // Не нужно, так как это последнее поле
            }
        }

        return msg;
    }
}