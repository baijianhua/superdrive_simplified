using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Connect2.Foundation.Security;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Helper;

namespace ConnectTo.Foundation.MISC
{
    internal class Misc
    {
        internal static void UnpairDevice(bool needDisconnect = false)
        {
            var pd = Env.Instance.Config.PairedDevice;
            if(needDisconnect && pd != null)
                AppModel.Instance.GetDevices().FirstOrDefault(d => d.ID == pd.ID)?.Disconnect(true);

            Env.Instance.Config.PairedDevice = null;
            Env.Instance.Config.Save();
            
            

            Env.Instance.SecurityManager.DeleteString(SecurityManager.SSID_PASSWORD);
            //对PC端没意义。REMOTE_CONNECT_CODE其实是为了发起连接时输入的，另一台设备的code.
            Env.Instance.SecurityManager.DeleteString(SecurityManager.REMOTE_CONNECT_CODE);
            //对手机端没意义。配对断开后，就重新生成LocalConnectCode.
            var c = StringHelper.NewRandomGUID();
            AppModel.Instance.LocalDevice.ConnectCode = c;
            Env.Instance.SecurityManager.SaveString(SecurityManager.LOCAL_CONNECT_CODE, c);


        }
    }
}
