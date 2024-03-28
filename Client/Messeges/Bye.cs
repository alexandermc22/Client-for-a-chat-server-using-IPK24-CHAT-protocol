namespace Client.Messeges;
public class Bye: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.BYE;
    
    
    public byte[] ToBytes(ushort id)
    {
        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2];

        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(id);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        return result;
    }
}