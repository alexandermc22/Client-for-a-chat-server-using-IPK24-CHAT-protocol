namespace Client;

public class Confirm : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.CONFIRM;
    public ushort MessageId { get; set; }
    
}