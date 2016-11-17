using ConnectTo.Foundation.Messages;


namespace ConnectTo.Foundation.Core
{
    internal interface ISplittable
    {
        Message GetNextMessage();
    }
}
