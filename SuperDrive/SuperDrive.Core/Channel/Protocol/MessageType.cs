namespace SuperDrive.Core.Channel.Protocol
{
    public enum MessageType : int
    {
        Unknown = -1,
        Heartbeat = 0x00,

        Discover = 0x10,
        Acknowledge = 0x11,
        Offline = 0x12,

        Connect = 0x20,
        Reject = 0x21,
        Accept = 0x22,
        Confirm = 0x23,
        Cancel = 0x24,
        Send = 0x25,
        Disconnect = 0x26,
        FileDataMessage = 0x27,
        BrowseRequest=0x28,
        BrowseResponse=0x29,

        SendItems = 0x30,
        GetItems = 0x31,

        ThumbnailRequest = 0x32,
        ThumbnailResponse = 0x33,

        CancelItems = 0x34,

        
        AgreeConversation = 0x35,
        RejectConversation = 0x36,
        CancelConversation = 0x37,
        ConfirmItem = 0x38,
        ConversationRecoverRequest = 0x39,
        ConversationRecoverResponse = 0x40,
        RecoverSendItemRequest = 0x41,
        RecoverSendItemsResponse = 0x42,
        GetItemsRecoverRequest = 0x43,
        ReceiveItemError = 0x44,
        Ping = 0x45,
        PingReply = 0x46,
        ChannelReady = 0x47,
        Unpair = 0x48,
        GetItemAgreed = 0x49
    }
}