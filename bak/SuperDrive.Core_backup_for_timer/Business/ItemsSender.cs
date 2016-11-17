using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Business
{
    public class ItemsSenderRequester : Requester
    {
        private List<Item> _items;
        public List<Item> Items {
            get
            {
                return _items;
            }
            set
            {
                //如果会话开始，不再允许赋值。
                if (IsStarted)
                {
                    throw new Exception("Conversation already started. Can not change sending items. Please call AppModel::Create to start another conversation");
                }
                //TODO 这样做不行，需要深拷贝。
                _items = value;
            }
        }

        

        public event Action<int> ProgressChanged;
        public event Action<int> SpeedChanged;


        internal protected override void OnInitRequest()
        {
            SendItemsMessage message = new SendItemsMessage(Items);
            Peer.Post(message);
        }

        internal protected override void OnAgreed()
        {
            var tmp = new List<ISendable>();
            Items.ForEach((item) => tmp.Add(item));
            var bulk = new ListSequenceSendable(ID,tmp);
            Peer.Post(bulk);
        }
        

        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public void Cancel(List<Item> list)
        {
            throw new NotImplementedException();
        }
    }
}
