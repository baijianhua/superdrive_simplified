using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Core;
using System.IO;
using System.Collections.Generic;
using ConnectTo.Foundation.Common;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using SuperDrive.Library;

namespace ConnectTo.Foundation.Business
{
    public class BrowseResponder : Responder
    {
        private BrowseResponseMessage respondMessage = null;
        private BackgroundWorker work = null;

        public bool FillItems(List<Item> items)
        {
            if(respondMessage == null || respondMessage.Items == null)
            {
                return false;
            }

            var browsedItems = respondMessage.Items;
            foreach(var item in items)
            {
                var browsedItem = browsedItems.Find(bri => bri.ID == item.ID);
                if(browsedItem != null)
                {
                    item.AbsolutePath = browsedItem.AbsolutePath;
                    item.Name = browsedItem.Name;
                    item.Length = browsedItem.Length;
                }
            }

            return true;
        }

        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            BrowseRequestMessage brMessage = message as BrowseRequestMessage;
            if (brMessage != null)
            {                
                if (respondMessage == null
                    || !respondMessage.browserId.Equals(brMessage.browserId))
                {
                    #region comment
                    //TODO 如果是安卓又该如何处理？首先需要搞清楚这是在分类浏览，还是在浏览目录，如果是在浏览目录，这个代码可用
                    //如果是在分类浏览，那么需要去数据库中查询，并返回列表。

                    //如果安卓做分类浏览，那么分类浏览中有没有子目录的概念？有的图库中有多个相册，是如何处理的？多个相册看起来像是文件夹的概念。

                    //TODO 新拍摄的图片之类的，没有立即存储到MediaStore，如何处理？
                    //TODO 如果有生成并缓存缩略图，那些缩略图要被MediaStore忽略才行。

                    //http://blog.csdn.net/yhcelebrite/article/details/11714925
                    //http://blog.csdn.net/bgc525725278/article/details/8131657              
                    #endregion

                    if (work != null && work.IsBusy)
                    {
                        work.CancelAsync();
                    }
                    var browseList = Env.Instance.GetBrowseList(brMessage.path);
                    respondMessage = new BrowseResponseMessage();
                    respondMessage.browserId = brMessage.browserId;
                    respondMessage.Items = browseList;
                    respondMessage.path = brMessage.path;
                    PostMessage(respondMessage); 
                }

                return;
            }

            ThumbnailRequestMessage thumbRequestMessage = message as ThumbnailRequestMessage;
            if (thumbRequestMessage != null)
            {
                work = new BackgroundWorker() { WorkerSupportsCancellation = true };
                work.DoWork += Work_DoWork;
                work.RunWorkerAsync(thumbRequestMessage);

                return;
            }
        }

        private void Work_DoWork(object sender, DoWorkEventArgs e)
        {
            var work = sender as BackgroundWorker;
            var thumbRequestMessage = e.Argument as ThumbnailRequestMessage;
            if (work == null || thumbRequestMessage == null) return;
            
            while (!work.CancellationPending)
            {
                foreach (var itemID in thumbRequestMessage.itemIDList)
                {
                    if (respondMessage.Items.Exists(item1 => itemID == item1.ID))
                    {
                        var item = respondMessage.Items.Find(item2 => itemID == item2.ID);
                        if (item.Type == ItemType.File)
                        {
                            byte[] bt = Env.Instance.GetThumbnailStream(respondMessage.path, item);
                            if (bt != null)
                            {
                                ThumbnailResponseMessage thumbnailReponseMessage = new ThumbnailResponseMessage()
                                {
                                    ID = item.ID,
                                    Name = item.Name,
                                    Length = bt.Length,
                                    Data = bt
                                };
                                
                                PostAsSendable(thumbnailReponseMessage);
                            };
                        }
                    }
                }
                work.CancelAsync();
            }
        }
    }
}