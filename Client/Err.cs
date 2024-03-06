namespace Client;

public class Err: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.ERR;
    public ushort MessageId { get; set; }
    public required string DisplayName { get; set; } 
    public required string MessageContents { get; set; } 
    
}