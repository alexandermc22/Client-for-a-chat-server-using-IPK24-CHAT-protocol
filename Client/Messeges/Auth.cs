namespace Client.Messeges;
using System.Text;
using System.Text.RegularExpressions;
public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Secret { get; set; }

    
    public string ToTcpString( )
    {
        Exception ex = new Exception("Wrong input data");
        // Check the length of the channel identifier and channel name
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
        // Build a string in the format "JOIN SP ID SP AS SP DNAME \r\n".
        return string.Format("AUTH {0} AS {1} USING {2}\r\n", Username, DisplayName, Secret );
    }
    
    
    
    public byte[] ToBytes(ushort id)
    {
        byte[] usernameBytes = Encoding.UTF8.GetBytes(Username);
        byte[] displayNameBytes = Encoding.UTF8.GetBytes(DisplayName);
        byte[] secretBytes = Encoding.UTF8.GetBytes(Secret);

        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2 + usernameBytes.Length + 1 + displayNameBytes.Length + 1 + secretBytes.Length + 1];

        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(id);
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