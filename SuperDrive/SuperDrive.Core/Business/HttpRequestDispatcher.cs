using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
        public class HttpRequestDispatcher
        {
                //private static SequencialTaskPool _stp;
                //private static SequencialTaskPool Stp
                //{
                //        get
                //        {
                //                if (_stp != null) return _stp;

                //                _stp = new SequencialTaskPool("Thumbnail Provider", 5);
                //                _stp.Start();
                //                return _stp;
                //        }
                //}
                public static async void Dispatch(object sender, HttpListenerRequestEventArgs e)
                {
                        var request = e.Request;
                        var response = e.Response;
                        if (request.Method == HttpMethods.Get)
                        {
                                var uri = request.RequestUri;
                                var path = uri.AbsolutePath;
                                path = path.StartsWith("/") ? path.Substring(1) : path;
                                switch (path)
                                {
                                        case Consts.GetItemUriPath:
                                                await ProvideItem(request, response);
                                                break;
                                        case Consts.GetThumbnailUriPath:
                                                //这样做还不行哦，因为http服务器接着会走下去，然后就把response给关掉了。
                                                Env.PostSequencialTask(async () => await ProvideThumbnail(request, response));
                                                //Env.Logger.Log("After break of get thumbnail",nameof(HttpRequestDispatcher));
                                                break;
                                        default:
                                                response.NotFound();
                                                response.Dispose();
                                                break;
                                }
                        }
                        else if (request.Method == HttpMethods.Post)
                        {
                                //Env.Logger.Log($"Hi, {name}! Nice to meet you.");
                                response.NotImplemented();
                                response.Dispose();
                        }
                        else
                        {
                                response.MethodNotAllowed();
                                response.Dispose();
                        }
                }

                private static readonly BrowseResponseUtil Bru = new BrowseResponseUtil();
                private static async Task ProvideItem(HttpListenerRequest request, HttpListenerResponse response)
                {
                        var param = request.RequestUri.ParseQueryParameters();
                        var sessionId = param.GetByKey(Consts.SessionId);
                        var itemId = param.GetByKey(Consts.ItemId);
                        bool provided = false;
                        //能够提供文件的会话，可能是SendItemsRequester,或者是BrowseResponder,或者是GetItemsResponder(不再需要这个了？)
                        IItemProviderConversation br = SuperDriveCore.Conversations.GetByKey(sessionId) as IItemProviderConversation;
                        FileItem fi = br?.FindItem(itemId) as FileItem;

                        if (fi != null)
                        {
                                try
                                {
                                        response.Headers.ContentLength = fi.Length;
                                        response.UseMemoryStream = false;
                                        await response.WriteHeaders();
                                        var buffer = new byte[1024 * 1024];
                                        using (var stream = fi.Open(FileMode.Open, FileAccess.Read))
                                        {
                                                int count;
                                                while (!fi.IsTransferEnd() && (count = await fi.ReadAsync(buffer)) != 0 )
                                                {
                                                        await response.GetSocketStream().WriteAsync(buffer, 0, count);
                                                        provided = true;
                                                        Env.Logger.Log("Get Item End " + itemId, "Http");
                                                }
                                        }
                                        response.Dispose();
                                }
                                catch (Exception e)
                                {
                                        Env.Logger.Log($"Provide item[{fi}] exception", stackTrace: e.StackTrace);
                                        //TODO 这里没机会改Stats code了。只能强制关闭socket。
                                        response.CloseSocket();
                                }
                        }

                        if (!provided)
                        {
                                response.Forbidden();
                                response.Dispose();
                        }
                }

                private static async Task ProvideThumbnail(HttpListenerRequest request, HttpListenerResponse response)
                {
                        var param = request.RequestUri.ParseQueryParameters();
                        var sessionId = param.GetByKey(Consts.SessionId);
                        var itemId = param.GetByKey(Consts.ItemId);

                        //Env.Logger.Log("Get Thumbnail start " + itemId, "Http");
                        bool provided = false;
                        BrowseResponder br = SuperDriveCore.Conversations.GetByKey(sessionId) as BrowseResponder;
                        if (br != null)
                        {
                                DirItem dir = br.CurrentFolder;
                                var f = dir?.Children.FirstOrDefault(d => d.Id == itemId) as FileItem;
                                if (f != null)
                                {
                                        //不能在这里关闭流。因为response的close函数里面会使用这个流。想要关闭，除非
                                        //把内容复制给OutputStream.
                                        using (var rep = response)
                                        {
                                                response.OutputStream = await Env.FileSystem.GetThumbnailStream(f);
                                                provided = true;
                                                Env.Logger.Log("Get Thumbnail End " + itemId, "Http");
                                        }
                                }
                        }

                        if (!provided)
                        {
                                response.NotFound();
                                response.Dispose();
                        }
                }
        }
}
