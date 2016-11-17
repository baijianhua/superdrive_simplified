using System;
using System.Runtime.Serialization;

namespace ConnectTo.Foundation.Metrics
{
    public sealed class MetricsException : Exception
    {
        public MetricsException(int returnCode, string message = null) : this(message)
        {
            ReturnCode = returnCode;
        }

        public MetricsException(string message) : base(message) { }

        public MetricsException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public int ReturnCode { get; }
    }
}
