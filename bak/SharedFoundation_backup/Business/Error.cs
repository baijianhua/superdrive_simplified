using System.Collections.Generic;

namespace ConnectTo.Foundation.Business
{
    public class Error
    {
        internal static readonly int PortOccupied = 1;
        internal static readonly int InstanceAlreadyCreated = 2;
        internal Dictionary<object, object> bundle = new Dictionary<object, object>();

        public int ErrorCode { get; internal set; }

        public Error(int errorCode)
        {
            ErrorCode = errorCode;
        }
        public void AddError(object errorKey, object errorVal)
        {
            bundle.Add(errorKey, errorVal);
        }
    }
    
}