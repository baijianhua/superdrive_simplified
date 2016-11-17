namespace ConnectTo.Foundation.Core
{
    public enum TransferState
    {
        Idle,

        Transferring, 
        Paused,

        PostCompleted, //转换成messages完毕。
        SentCompleted, //底层的socket发送完毕。
        Completed,     //对于一般可传输对象，如果传输进度变成100%，即变成Completed状态。Completed后，如果需要等待对方的Confirm,则进入WaitingConfirm状态。 
        WaitingConfirm,//如果一个对象需要Confirm, 现在正在等待Confirm.
        WaitConfirmTimeouted,
        Confirmed,     //已经收到对方发送的Confirm消息。

        Canceled, 
        Error
    }
}