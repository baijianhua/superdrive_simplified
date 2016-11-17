using System;
using System.Linq;
using System.Net.NetworkInformation;
using ConnectTo.Foundation.Messages;
using NLog;

namespace ConnectTo.Foundation.Core
{
    public class DeviceInfo
    {
        public string ID { get; set; }
        //用户名。如Wallace.
        public string Name { get; set; }

        internal bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(ID) && !string.IsNullOrEmpty(Name);
            }
        }

        public Avatar Avatar { get; set; }

        public string IPAddress { get; set; }

        internal string Version { get; set; }

        internal string Language { get; set; }

        internal string OS { get; set; }

        internal Version OSVersion { get; set; }
        //设备名称，如A530, iphone 6s
        public string DeviceName { get; set; }

        internal DeviceType DeviceType { get; set; }

        internal bool IsSecured { get; set; }

        internal bool IsOnline { get; set; }

        internal DiscoveryType DiscoveryType { get; set; }
    }
    //通过何种方式初始化比较好？AppModel变成抽象类？LocalDeviceInfo变成抽象类？如果是后者，如何约束AppModel一定设置这个成员？
    //或者让AppModel的初始化函数要求必须传入平台相关的内容。那么AppModel就不适合通过GetInstance获取，必须显示初始化。
}
