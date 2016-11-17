using Newtonsoft.Json;
using SuperDrive.Core.Channel.Protocol;
using SuperDrive.Core.Enitity;
using Util = SuperDrive.Core.Support.Util;

namespace SuperDrive.Core.Messages
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class AbstractDeviceMessage : Message
    {
        [JsonProperty(PropertyName = "device")]
        public Device Device { get; set; }
        internal void InitFromDevice(Device d)
        {
            Device = new Device();
            //不要去修改原来的Device.
            Device.CopyFrom(d);
        }


        internal void ExtractToDevice(Device d)
        {
            d.CopyFrom(Device);
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal class OnlineMessage : AbstractDeviceMessage
    {
        [JsonProperty(PropertyName = "is_ping")]
        public bool IsPingMessage { get; set; }
        public OnlineMessage(Device localInfo):this()
        {
            InitFromDevice(localInfo);
        }
        internal OnlineMessage()
        {
            IsPingMessage = false;
            Type = MessageType.Discover;
        }
    }
    [JsonObject(MemberSerialization.OptIn)]
    internal class OfflineMessage : AbstractDeviceMessage
    {
        internal OfflineMessage(Device local) : this()
        {
            Util.Check(local != null);
            InitFromDevice(local);
            Device.State = DeviceState.OffLine;
        }
        //空构造函数在反序列化时需要。
        internal OfflineMessage()
        {
            Type = MessageType.Offline;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal class AcknowledgeMessage : AbstractDeviceMessage
    {
        internal AcknowledgeMessage(Device local) : this()
        {
            InitFromDevice(local);
        }
        //对端读取这个消息的时候，仍然需要这个空的构造函数。
        internal AcknowledgeMessage()
        {
            Type = MessageType.Acknowledge;
        }
    }
}