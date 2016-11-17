using System.Net;

namespace SuperDrive.Core
{
    public static class HttpResponseStatusCodeExtensions
    {
        public static void NotFound(this HttpListenerResponse response)
        {
            response.StatusCode = HttpStatusCode.NotFound;
            response.ReasonPhrase = "Not Found";
        }

        public static void InternalServerError(this HttpListenerResponse response)
        {
            response.StatusCode = HttpStatusCode.InternalServerError;
            response.ReasonPhrase = "Internal Server Error";
        }

        public static void MethodNotAllowed(this HttpListenerResponse response)
        {
            response.StatusCode = HttpStatusCode.MethodNotAllowed;
            response.ReasonPhrase = "Method Not Allowed";
        }

        public static void NotImplemented(this HttpListenerResponse response)
        {
            response.StatusCode = HttpStatusCode.NotImplemented;
            response.ReasonPhrase = "Not Implemented";
        }

        public static void Forbidden(this HttpListenerResponse response)
        {
            response.StatusCode = HttpStatusCode.Forbidden;
            response.ReasonPhrase = "Forbidden";
        }

    }
}
