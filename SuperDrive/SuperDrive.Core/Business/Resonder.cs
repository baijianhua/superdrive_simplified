using System.Threading.Tasks;
using SuperDrive.Core.Messages;

namespace SuperDrive.Core.Business
{
    public abstract class Responder : Conversation
    {
        internal virtual ConversationRequestMessage RequestMessage { get; set; }
        public void Reject()
        {
            ConversationRejectMessage msg = new ConversationRejectMessage();
            PostMessageAsync(msg);
        }

        public async void Agree()
        {
            ConversationAgreeMessage msg = await OnAgreed();
            PostMessageAsync(msg);
        }

#pragma warning disable 1998
        protected virtual async Task<ConversationAgreeMessage> OnAgreed()
#pragma warning restore 1998
        {
            return new ConversationAgreeMessage();
        }
    }
}