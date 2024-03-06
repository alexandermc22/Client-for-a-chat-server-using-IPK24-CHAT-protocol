namespace Client;

public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    public ushort MessageId { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Secret { get; set; }

}