namespace Client.Messeges;
public class Bye: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.BYE;
    
    
    public byte[] ToBytes()
    {
        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2];

        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(IMessage.MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        return result;
    }
}