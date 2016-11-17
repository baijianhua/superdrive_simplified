using System;
using System.Collections.Generic;
using System.Threading;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;

namespace ConnectTo.Foundation.Business
{
    public abstract class Responder : Conversation
    {
        internal virtual ConversationRequestMessage RequestMessage { get; set; }
        public void Reject()
        {
            ConversationRejectMessage msg = new ConversationRejectMessage();
            PostMessage(msg);
        }

        public void Agree()
        {
            
            ConversationAgreeMessage msg = new ConversationAgreeMessage();
            PostMessage(msg);
            //TODO 同意之后的下一步行为是什么？例如对方请求浏览，其实同意之后，马上就可以发送查询结果。
            //如果消息还没有送达对方，这个时候回给对方的任何消息，对方其实都没有理会。

            //TODO BUG 先用龌龊的方法先解决一下，都不能保证解决。最终需要请求端发一个确认消息，这边才能真正的开始行动。
            Thread.Sleep(200);
            OnAgreed();
        }

        protected virtual void OnAgreed()
        {

        }
    }
}