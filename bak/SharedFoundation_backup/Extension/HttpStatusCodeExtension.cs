using System.Net;

namespace ConnectTo.Foundation.Helper
{
    public static class HttpStatusCodeExtension
    {
        public static bool IsInformational(this HttpStatusCode statusCode)
        {
            return ((int) statusCode).ToString().StartsWith("1");
        }

        public static bool IsSuccessful(this HttpStatusCode statusCode)
        {
            return ((int) statusCode).ToString().StartsWith("2");
        }

        public static bool IsRedirection(this HttpStatusCode statusCode)
        {
            return ((int) statusCode).ToString().StartsWith("3");
        }

        public static bool IsClientError(this HttpStatusCode statusCode)
        {
            return ((int) statusCode).ToString().StartsWith("4");
        }

        public static bool IsServerError(this HttpStatusCode statusCode)
        {
            return ((int) statusCode).ToString().StartsWith("5");
        }
    }
}
