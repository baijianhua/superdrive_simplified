using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Business
{
        public class HttpDownloader
        {
                private readonly TaskWrapper _tw;
                private readonly List<Item> _internalItems = new List<Item>();
                private readonly AutoResetEvent _waiter = new AutoResetEvent(true);
                private int _downloaderCount;
                private const int MaxDownloaderCount = 5;

                public HttpDownloader()
                {
                        _tw = new TaskWrapper("HttpDownloader", Runner, TaskCreationOptions.LongRunning);
                }

#pragma warning disable CS1998 
                public async void PostItems([NotNull]IEnumerable<Item> items)
#pragma warning restore CS1998 
                {
                        bool isEmpty = true;
                        foreach (var item in items)
                        {
                                lock(_internalItems) _internalItems.Add(item);

                                item.StateChanged += (i, state) =>
                                {
                                        //如果任何Item的状态变成取消、错误、完成，都应该从这里面移除。
                                        if (!i.IsTransferEnd()) return;
                                        //TODO 这里还有不少问题。如果传输错误，这个Item该放在那里？ 
                                        //TODO 如果这个Item是一个文件夹的子项目，文件夹的状态该是什么样？
                                        var finishedItem = i as Item;
                                        lock (_internalItems) _internalItems.Remove(finishedItem);
                                        if (_internalItems.Count == 0) Stop();
                                };
                                isEmpty = false;
                        }

                        if (isEmpty) return;

                        _waiter.Set();
                        if (!_tw.IsRunning) _tw.Start();
                }
                public void Start() => _tw.Start();
                public void Stop()=>_tw.Stop();

                async void DownloadSingleFile(FileItem fi, CancellationToken token)
                {
                        var c = fi.Conversation as ITransferConversation;
                        if (c == null) return;

                        HttpClient client = new HttpClient();
                        var suri = $"http://{c.Peer.DefaultIp}:{Consts.HttpPort}/{Consts.GetItemUriPath}?{Consts.SessionId}={c.Id}&{Consts.ItemId}={fi.Id}";
                        _downloaderCount++;
                        HttpResponseMessage response = null;
                        Stream instream = null;
                        //如果用using, 怎么好像捕捉不到Cancel的Exception?还是用try catch finally吧。
                        try
                        {
                                response = await client.GetAsync(suri, HttpCompletionOption.ResponseHeadersRead, token);
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                        instream = await response.Content.ReadAsStreamAsync();
                                        // ReSharper disable once UnusedVariable
                                        using (var stream = fi.Open(FileMode.OpenOrCreate, FileAccess.Write))
                                        {
                                                var buffer = new byte[1024 * 1024];
                                                int count;

                                                while ((count = instream.Read(buffer, 0, buffer.Length)) != 0
                                                    && (fi.TransferState == TransferState.Transferring || fi.TransferState == TransferState.Idle)
                                                    && !token.IsCancellationRequested)
                                                {
                                                        await fi.WriteAsync(buffer, count);
                                                }
                                        }
                                }
                                else
                                {
                                        fi.TransferState = TransferState.Error;
                                        Env.Logger.Log($"http result not OK {response.StatusCode}");
                                }
                        }
                        catch (Exception e)
                        {
                                fi.TransferState = TransferState.Error;
                                Env.Logger.Log($"download file exception {fi}", stackTrace: e.StackTrace);
                        }
                        finally
                        {
                                response?.Dispose();
                                instream?.Dispose(); //TODO response设置了HttpCompletionOption.ResponseHeadersRead，dispose的时候还关闭instream不？
                                _downloaderCount--;
                                _waiter.Set();
                        }
                }
#pragma warning disable CS1998
                async Task DownloadSingleItem(Item item, CancellationToken token)
#pragma warning restore CS1998
                {
                        var conv = item.Conversation as IRemoteListableConversation;
                        Debug.Assert(conv != null, "conv != null");

                        var fi = item as FileItem;
                        var di = item as DirItem;

                        if (fi != null)
                        {
                                //不需要await，异步运行即可。
                                DownloadSingleFile(fi, token);
                        }
                        else if (di != null)
                        {
                                //递归处理目录。
                                //TODO 单个item的传输状态改变，对其父目录是什么影响？
	                        if (!Env.FileSystem.GetOrCreateDir(di))
	                        {
		                        di.TransferState = TransferState.Error;
		                        return;
	                        }

				var children = di.Children;
		                if (children.Any())
					PostItems(di.Children); 
		                else
					di.TransferState = TransferState.Completed;
                        }
                }

#pragma warning disable 1998
                private async Task Runner(CancellationToken token)
#pragma warning restore 1998
                {
                        //PostItems会检查是否已经启动，没启动的话会重启，所以重试只要PostItems即可。
                        while (true)
                        {
                                _waiter.WaitOne();
                                bool isAllDone;
                                lock (_internalItems) isAllDone = _internalItems.All(i => i.TransferState != TransferState.Idle);
                                if (_downloaderCount >= MaxDownloaderCount || isAllDone) continue;
                                if (token.IsCancellationRequested) break;

                                IEnumerable<Item> tmp;
                                //TODO 怎么按会话的优先级排序？
                                lock (_internalItems) tmp = _internalItems.Where(i => i.TransferState == TransferState.Idle).OrderBy(i => i.Priority);
                                if (!tmp.Any()) continue;

                                var item = tmp.First();
                                item.TransferState = TransferState.Transferring;

#pragma warning disable 4014
                                Task.Run(() => DownloadSingleItem(item, token), token);
#pragma warning restore 4014
                        }
                }
        }
}
