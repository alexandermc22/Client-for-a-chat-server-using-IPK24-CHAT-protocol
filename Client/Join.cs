namespace Client;

public class Join : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.JOIN;
    public ushort MessageId { get; set; }
    public required string ChannelId  { get; set; }
    public  string DisplayName    { get; set; } 


}