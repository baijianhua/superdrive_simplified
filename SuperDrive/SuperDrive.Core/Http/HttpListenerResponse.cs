using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SuperDrive.Core.Abstractions;

namespace SuperDrive.Core
{
        public sealed class HttpListenerResponse : IDisposable
        {
                private TcpClientAdapter client;
                private bool _isSocketClosed;

                internal HttpListenerResponse(HttpListenerRequest request, TcpClientAdapter client)
                {
                        Headers = new HttpListenerResponseHeaders(this);

                        this.client = client;
                        this.Request = request;
                }

                internal void Initialize()
                {
                        OutputStream = new MemoryStream();

                        Version = Request.Version;
                        StatusCode = HttpStatusCode.OK;
                        ReasonPhrase = "OK";
                }

                HttpListenerRequest Request { get; set; }

                /// <summary>
                /// Gets the headers of the HTTP response.
                /// </summary>
                public HttpListenerResponseHeaders Headers { get; private set; }

                /// <summary>
                /// Gets the stream containing the content of this response.
                /// </summary>
                public Stream OutputStream { get; set; }

                /// <summary>
                /// Gets or sets the HTTP version.
                /// </summary>
                public string Version { get; set; }

                /// <summary>
                /// Gets or sets the HTTP status code.
                /// </summary>
                public HttpStatusCode StatusCode { get; set; }

                /// <summary>
                /// Gets or sets the HTTP reason phrase.
                /// </summary>
                public string ReasonPhrase { get; set; }

                public bool UseMemoryStream { get; set; } = true;

                public async Task WriteHeaders()
                {
                        string header = $"{Version} {(int)StatusCode} {ReasonPhrase}\r\n" +
                                        Headers.ToString() +
                                        "\r\n";

                        byte[] headerArray = Encoding.UTF8.GetBytes(header);
                        var socketStream = client.GetOutputStream();
                        await socketStream.WriteAsync(headerArray, 0, headerArray.Length);
                        await socketStream.FlushAsync();
                }

                public Stream GetSocketStream()
                {
                        return client.GetOutputStream();
                }



                /// <summary>
                /// Writes a string to OutputStream.
                /// </summary>
                /// <param name="text"></param>
                /// <returns></returns>
                public Task WriteContentAsync(string text)
                {
                        var buffer = Encoding.UTF8.GetBytes(text);
                        return OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }

                /// <summary>
                /// Closes this response and sends it.
                /// </summary>
                private async Task Close()
                {
                        if (UseMemoryStream)
                        {
                                try
                                {
                                        OutputStream.Seek(0, SeekOrigin.Begin);

                                        var socketStream = client.GetOutputStream();

                                        string header = $"{Version} {(int)StatusCode} {ReasonPhrase}\r\n" +
                                                        Headers.ToString() +
                                                        $"Content-Length: {OutputStream.Length}\r\n" +
                                                        "\r\n";

                                        byte[] headerArray = Encoding.UTF8.GetBytes(header);
                                        await socketStream.WriteAsync(headerArray, 0, headerArray.Length);
                                        await OutputStream.CopyToAsync(socketStream);
                                        await socketStream.FlushAsync();
                                }
                                catch (Exception e)
                                {
                                        Env.Logger.Log("Close http response exception", nameof(HttpListenerResponse), e.StackTrace);
                                        //TODO 如果在复制过程出错怎么办？ 已经没有机会改status code了。只能靠异常来解决。
                                }
                        }
                        CloseSocket();
                }

                internal void CloseSocket()
                {
                        _needDispose = false;
                        client.Dispose();
                }

                /// <summary>
                /// Writes a HTTP redirect response.
                /// </summary>
                /// <param name="redirectLocation"></param>
                /// <returns></returns>
                public async Task RedirectAsync(Uri redirectLocation)
                {
                        var outputStream = client.GetOutputStream();

                        StatusCode = HttpStatusCode.MovedPermanently;
                        ReasonPhrase = "Moved permanently";
                        Headers.Location = redirectLocation;

                        string header = $"{Version} {StatusCode} {ReasonPhrase}\r\n" +
                                        $"Location: {Headers.Location}" +
                                        $"Content-Length: 0\r\n" +
                                        "Connection: close\r\n" +
                                        "\r\n";

                        byte[] headerArray = Encoding.UTF8.GetBytes(header);
                        await outputStream.WriteAsync(headerArray, 0, headerArray.Length);
                        await outputStream.FlushAsync();

                        CloseSocket();
                }

                ~HttpListenerResponse()
                {
                        Dispose();
                }

                private bool _needDispose = true;
                public async void Dispose()
                {
                        if (!_needDispose) return;

                        //Env.Logger.Log("HttpListenerResponse.Dispose");
                        _needDispose = false;
                        try
                        {
                                await Close();
                        }
                        catch (Exception e)
                        {
                                Env.Logger.Log("Close response exception" + e.StackTrace);
                                CloseSocket();
                        }


                        //var t = Close();
                        //t.ConfigureAwait(false);
                        //try
                        //{
                        //    t.Wait();
                        //}
                        //catch (Exception e)
                        //{
                        //    Env.Logger.Log("Wait Close response exception");
                        //}

                        GC.SuppressFinalize(this);

                }
        }
}