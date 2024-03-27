namespace Client.Messeges;

public interface IMessage
{
    MessageType MessageType { get; set; }
    static ushort MessageId { get; set; }

    public byte[] ToBytes();
}    
public enum MessageType : byte
{
    CONFIRM = 0x00,
    REPLY = 0x01,
    AUTH = 0x02,
    JOIN = 0x03,
    MSG = 0x04,
    ERR = 0xFE,
    BYE = 0xFF
}