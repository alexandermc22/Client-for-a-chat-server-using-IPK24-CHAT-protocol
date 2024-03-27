namespace Client.Messeges;
using System.Text;
using System.Text.RegularExpressions;
public class Err: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.ERR;
    public  string DisplayName { get; set; } 
    public  string MessageContents { get; set; } 
    
    
    public static string ToTcpString(Err err)
    {
        Exception ex = new Exception("Wrong input data");
        // Check the length of the channel identifier and channel name
        if (err.DisplayName.Length > 20 || err.MessageContents.Length > 1400)
        {
            throw new ArgumentException("MessageContents and Display Name cannot exceed 20 characters in length.");
        }
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(err.DisplayName, patternDname))
            throw ex;
        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(err.MessageContents, pattern))
            throw ex;
        // Build a string in the format "JOIN SP ID SP AS SP DNAME \r\n".
        return string.Format("ERR FROM {0} IS {1}\r\n", err.DisplayName, err.MessageContents);
    }
    public static Err FromStringTcp(string[] words)
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
        
        Err err = new Err()
        {
            DisplayName = words[2],
            MessageContents = words[4]
        };
        return err;
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
    public static Err FromBytes(byte[] data)
    {
        if (data.Length < 3)
        {
            // Invalid data for parsing
            throw new ArgumentException("wrong data from server");
        }

        Err errMessage = new Err();
        // Extract MessageType from the first byte
        errMessage.MessageType = (MessageType)data[0];

        // Extract MessageId from the following two bytes
        IMessage.MessageId = BitConverter.ToUInt16(data, 1);

        // Skip byte 3, because in the original array it is 0x00 after MessageId

        // Retrieve DisplayName, assuming it ends with byte 0x00
        int offset = 4;
        List<byte> displayNameBytes = new List<byte>();
        while (offset < data.Length && data[offset] != 0)
        {
            displayNameBytes.Add(data[offset]);
            offset++;
        }
        errMessage.DisplayName = Encoding.UTF8.GetString(displayNameBytes.ToArray());

        // Skip byte 0x00 after DisplayName
        offset++;

        // Retrieve MessageContents, assuming it ends with byte 0x00
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