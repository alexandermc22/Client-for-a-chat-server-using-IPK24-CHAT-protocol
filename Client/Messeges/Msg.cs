namespace Client.Messeges;
using System.Text;
using System.Text.RegularExpressions;
public class Msg: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.MSG;
    public string? DisplayName { get; set; } 
    public  string? MessageContents { get; set; } 
    
    public static string ToTcpString(Msg msg)
    {
        Exception ex = new Exception("Wrong input data");
        // Check the length of the channel identifier and channel name
        if (msg.DisplayName.Length > 20 || msg.MessageContents.Length > 1400)
        {
            throw new ArgumentException("MessageContents and Display Name cannot exceed 20 characters in length.");
        }
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(msg.DisplayName, patternDname))
            throw ex;
        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(msg.MessageContents, pattern))
            throw ex;
        // Build a string in the format "JOIN SP ID SP AS SP DNAME \r\n".
        return string.Format("MSG FROM {0} IS {1}\r\n", msg.DisplayName, msg.MessageContents);
    }
    
    
    public static Msg FromStringTcp(string[] words)
    {
        Exception ex = new Exception("Wrong data from server");
        if (words.Length != 5 )
            throw ex;
        if (words[1] != "FROM")
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(words[2], patternDname))
            throw ex;
        
        if(words[3]!="IS")
            throw ex;
        if (words[4].Length > 1400)
            throw ex;

        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(words[3], pattern))
            throw ex;
        
        Msg msg = new Msg()
        {
            DisplayName = words[2],
            MessageContents = words[4]
        };
        return msg;
    }
    
    
    public byte[] ToBytes()
    {
        byte[] displayNameBytes = Encoding.UTF8.GetBytes(DisplayName);
        byte[] messageContentsBytes = Encoding.UTF8.GetBytes(MessageContents);

        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2 + displayNameBytes.Length + 1 + messageContentsBytes.Length + 1];

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

        // Check the length of the array
        if (data.Length >= 3)
        {
            msg.MessageType = (MessageType)data[0];

            // Getting MessageId from byte array
            IMessage.MessageId = BitConverter.ToUInt16(data, 1);

            int offset = 3;

            // DisplayName
            int displayNameLength = Array.IndexOf<byte>(data, 0, offset) - offset;
            if (displayNameLength >= 0)
            {
                msg.DisplayName = Encoding.UTF8.GetString(data, offset, displayNameLength);
                offset += displayNameLength + 1; // Move to the next byte after the null terminator
            }

            // MessageContents
            int messageContentsLength = Array.IndexOf<byte>(data, 0, offset) - offset;
            if (messageContentsLength >= 0)
            {
                msg.MessageContents = Encoding.UTF8.GetString(data, offset, messageContentsLength);
            }
        }

        return msg;
    }
}