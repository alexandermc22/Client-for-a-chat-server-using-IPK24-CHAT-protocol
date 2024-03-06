namespace Client;

public class Msg: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.MSG;
    public ushort MessageId { get; set; }
    public string DisplayName { get; set; } 
    public required string MessageContents { get; set; } 
    
}