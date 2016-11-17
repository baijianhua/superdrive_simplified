using System;
using System.Collections.Generic;
using System.Text;
using ConnectTo.Foundation.Core;

namespace ConnectTo.Foundation.Business
{
    public interface ICancellable
    {
        void Cancel();
        void Cancel(List<Item> list);
    }
}
