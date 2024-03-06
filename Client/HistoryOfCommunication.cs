namespace Client;

public class HistoryOfCommunication
{
    public List<Auth> AuthList = new List<Auth>();
    public List<Join> JoinList = new List<Join>();
    public List<Msg> MsgList = new List<Msg>();
    public List<Reply> ReplyList = new List<Reply>();
    public List<Err> ErrList = new List<Err>();
    public List<Confirm> ConfirmList = new List<Confirm>();
    public List<Bye> ByeList = new List<Bye>();

    public string? Name = null;
    public bool AuthSuccess = false; 
    public object lockObject = new object();
    
    public ushort i = 0;
    

}