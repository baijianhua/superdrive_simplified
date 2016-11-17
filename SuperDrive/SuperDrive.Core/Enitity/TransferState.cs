namespace SuperDrive.Core.Enitity
{
    public enum TransferState
    {
        Idle,

        Transferring, 
        Paused,

        PostCompleted, //转换成messages完毕。
        SentCompleted, //底层的socket发送完毕。

        Completed,     //对于一般可传输对象，如果传输进度变成100%，即变成Completed状态。Completed后，如果需要等待对方的Confirm,则进入WaitingConfirm状态。 
        Canceled, 
        Error
    }
}