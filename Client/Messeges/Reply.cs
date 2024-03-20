using System.Data;
using System.Text.RegularExpressions;
namespace Client;
using System.Text;
public class Reply : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.REPLY;
    //public  ushort MessageId { get; set; }
    public bool Result { get; set; }
    public ushort RefMessageId { get; set; }
    public  string MessageContent { get; set; }

    public static Reply FromStringTcp(string[] words)
    {
        Exception ex = new Exception("Wrong data from server");
        if (words.Length != 4 )
            throw ex;
        bool result;
        if (words[1] == "OK")
            result = true;
        else if (words[1] == "NOK")
            result = false;
        else throw ex;
        
        if(words[2]!="IS")
            throw ex;
        if (words[3].Length > 1400)
            throw ex;

        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(words[3], pattern))
            throw ex;
        
        Reply reply = new Reply()
        {
            Result = result,
            MessageContent = words[3]
        };
        return reply;
    }
    
    
    
    public byte[] ToBytes()
    {
        byte[] messageContentBytes = Encoding.UTF8.GetBytes(MessageContent);

        // Создаем массив для объединения всех байтов
        byte[] result = new byte[1 + 2 + 1 + 2 + messageContentBytes.Length + 1];

        // Используем приведение enum к byte для преобразования MessageType в байт
        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        result[3] = (byte)(Result ? 1 : 0);

        byte[] refMessageIdBytes = BitConverter.GetBytes(RefMessageId);
        Array.Copy(refMessageIdBytes, 0, result, 4, 2);

        int offset = 6;

        Array.Copy(messageContentBytes, 0, result, offset, messageContentBytes.Length);
        offset += messageContentBytes.Length;
        result[offset] = 0; // Null terminator after MessageContent

        return result;
    }
    public static Reply FromBytes(byte[] data)
    {
        if (data == null || data.Length < 6)
        {
            throw new ArgumentException("Invalid data array", nameof(data));
        }

        MessageType messageType = (MessageType)data[0];
        //ushort messageId = BitConverter.ToUInt16(data, 1);
        bool result = data[3] != 0;
        ushort refMessageId = BitConverter.ToUInt16(data, 4);

        // Extract message content, assuming it's a null-terminated UTF-8 string
        int contentLength = Array.IndexOf(data, (byte)0, 6);
        if (contentLength == -1)
        {
            throw new ArgumentException("Invalid data array: missing null terminator", nameof(data));
        }

        string messageContent = Encoding.UTF8.GetString(data, 6, contentLength - 6);

        return new Reply
        {
            MessageType = messageType,
            Result = result,
            RefMessageId = refMessageId,
            MessageContent = messageContent
        };
    }
}