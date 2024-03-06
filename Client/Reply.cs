namespace Client;

public class Reply : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.REPLY;
    public ushort MessageId { get; set; }
    public bool Result { get; set; }
    public ushort RefMessageId { get; set; }
    public required string MessageContent { get; set; }
    
}