using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Messages;
using SuperDrive.Core.Support;
using System.Net;
using System.Net.Http;
using static SuperDrive.Core.Support.Consts;


namespace SuperDrive.Core.Business
{
        //有remote和local两个实现。
        public interface IBrowse : IRemoteListableConversation
        {
                //因为返回的这个Thumbnail，有可能是UIImage, Bitmap, String, Stream,或者ImageSource.
                //很难定义
                Task<object> GetThumbnail(Item item);
                void CancelOperation();
                Device Peer { get; set; }
        }

        public class LocalBrowser : IBrowse
        {
                public LocalBrowser()
                {
                        Peer = SuperDriveCore.LocalDevice;
                }
                public async Task<object> GetThumbnail(Item item)
                {
                        var fi = item as AbstractFileItem;
                        return fi == null ? null : await Env.FileSystem.GetNativeThumbnailImage(fi);
                }

                private TaskCompletionSource<IEnumerable<Item>> _getItemsTcs;

                public Task<IEnumerable<Item>> GetDirChildren(DirItem dir)
                {
	                CurrentDir = dir;
			//为上一次的GetItems请求返回null。
                        _getItemsTcs?.TrySetResult(null);

                        _getItemsTcs = new TaskCompletionSource<IEnumerable<Item>>();
                        _getItemsTcs.SetValueWhenTimeout(TimeSpan.FromSeconds(DefaultConnectTimeoutSeconds), null);

                        Task.Run(() =>
                        {
                                var ch = dir.Children;
                                _getItemsTcs.SetResult(ch);
                        });
                        return _getItemsTcs.Task;
                }

	        public DirItem CurrentDir { get; private set; }

	        public Device Peer { get; set; }
                public void CancelOperation()
                {

                }
        }
        public class RemoteBrowser : Requester, IBrowse, IRemoteListableConversation
        {
                public object BrowseId { get; set; }

                public RemoteBrowser(Device device, string id) : base(device, id)
                {
                        _rbu = new BrowseRequestUtil(this);
                }
                //TODO 这个实现也别扭。Requester.Start本身有一个等待的Task,等待Conversation agree/reject
                // _rbu.GetItems也产生了一个等待。等待的是BrowseResponseMessage  这个两个等待并不等价，也没办法直接关联。
                //只能把所有路径考虑全。
                protected internal override void OnInitRequest() => _rbu.GetDirChildren(NamedFolders.ImageDir, Response);
                protected internal override void OnMessageReceived(ConversationMessage message)
                {
                        BrowseResponseMessage rep = message as BrowseResponseMessage;
                        if (rep != null) _rbu.ProcessResponse(rep);
                }

                public async Task<object> GetThumbnail(Item item)
                {
                        string suri = $"http://{Peer.DefaultIp}:{HttpPort}/{GetThumbnailUriPath}?{SessionId}={Id}&{ItemId}={item.Id}";
                        //Env.Logger.Log("suri="+suri);
                        //Uri uri = new Uri(suri);
                        //return uri;
                        HttpClient client = null;
                        HttpResponseMessage rep = null;
                        try
                        {
                                client = new HttpClient();
                                rep = await client.GetAsync(suri);
                                switch (rep.StatusCode)
                                {
                                        case HttpStatusCode.OK:
                                                //TODO HTTP image 缓存。
                                                using (var stream = await rep.Content.ReadAsStreamAsync())
                                                {
                                                        MemoryStream ms = new MemoryStream();
                                                        await stream.CopyToAsync(ms);
                                                        ms.Seek(0, SeekOrigin.Begin);
                                                        return ms;
                                                }
                                        default:
                                                return Env.FileSystem.DefaultFileIconStream;
                                }
                        }
                        catch (Exception)
                        {
                                Env.Logger.Log($"Get thumbnail exception path={item.AbsolutePath} name={item.Name}");
                                return Env.FileSystem.DefaultFileIconStream;
                        }
                        finally
                        {
                                try
                                {
                                        rep?.Dispose();
                                        client?.Dispose();
                                }
                                catch (Exception)
                                {
                                        // ignored
                                }
                        }
                }
                readonly BrowseRequestUtil _rbu;

                //TODO 这个地方很别扭。要在两个地方调用_rbu.ProcessResponse。因为Requester会在两个分支返回结果。
                //这是在Requester里面分发的。
                protected override void OnRequestAgreed(ConversationAgreeMessage conversationAgreeMessage)
                    => _rbu.ProcessResponse(conversationAgreeMessage as BrowseResponseMessage);
                public override void CancelOperation() => _rbu.Cancel();

	        public Task<IEnumerable<Item>> GetDirChildren(DirItem dir)=> _rbu.GetDirChildren(dir);
	        public DirItem CurrentDir => _rbu.CurrentDir;
        }
}
