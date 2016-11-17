using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConnectTo.Foundation.Business;
using ConnectTo.Foundation.Helper;
using SuperDrive.Library;

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
        }
    }
}
