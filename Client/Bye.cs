namespace Client;

public class Bye: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.BYE;
    public ushort MessageId { get; set; }
}