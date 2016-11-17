using System;
using System.IO;
using System.Collections.Generic;
using ConnectTo.Foundation.Core;
using ConnectTo.Foundation.Messages;
using ConnectTo.Foundation.Helper;

namespace ConnectTo.Foundation.Business
{
    public class BrowseRequester : Requester
    {

        public event Action<string, List<Item>> itemListReceived;

        public event Action<string, byte[]> thumbnailReceived;

        private string browserId = string.Empty;

        private BrowseRequestMessage brequestMessage = null;

        private BrowseResponseMessage breponseMessage = null;

        private ThumbnailRequestMessage thumbMessage = null;

        private bool isAgreed = false;

        public void Browse(BrowseCondition condition)
        {
            if (condition != null)
            {
                browserId = StringHelper.NewRandomGUID();
                brequestMessage = new BrowseRequestMessage()
                {
                    browserId = browserId,
                    path = condition.Path,
                    maxItemCount = condition.MaxItemCount,
                    offset = condition.Offset
                };

                if (isAgreed)
                {
                    PostMessage(brequestMessage);
                }
            }
        }

        public void GetThumbnail(string itemID)
        {
            if (breponseMessage!= null 
                && breponseMessage.Items.Exists(item => itemID.Equals(item.ID)))
            {
                List<string> list = new List<string>();
                list.Add(itemID);
                thumbMessage = new ThumbnailRequestMessage()
                {
                    itemIDList = list
                };
                PostMessage(thumbMessage);
            }
        }

        public void GetThumbnail(List<string> ItemIDList)
        { 
            if (breponseMessage != null && ItemIDList != null)
            {
                List<string> list = new List<string>();
                foreach (var itemID in ItemIDList)
                {
                    if (breponseMessage.Items.Exists(item => itemID.Equals(item.ID)))
                    {
                        list.Add(itemID);
                    }
                }
                if (list.Count > 0)
                {
                    thumbMessage = new ThumbnailRequestMessage()
                    {
                        itemIDList = list
                    };
                    PostMessage(thumbMessage);
                }
            }            
        }

        protected internal override void OnInitRequest()
        {
            if (brequestMessage == null)
            {
                browserId = StringHelper.NewRandomGUID();
                brequestMessage = new BrowseRequestMessage()
                {
                    browserId = browserId,
                    path = BrowseLocation.CONNECT2_DEFAULT,
                    maxItemCount = 0,
                    offset = 0
                };
            }
            PostMessage(brequestMessage);
        }

        protected internal override void OnAgreed()
        {
            isAgreed = true;
            base.OnAgreed();
        }

        protected internal override void OnMessageReceived(ConversationMessage message)
        {
            if (message is BrowseResponseMessage)
            {
                BrowseResponseMessage brMessage = (BrowseResponseMessage)message;

                if (browserId.Equals(brMessage.browserId))
                {
                    if (brMessage.Items != null)
                    {
                        breponseMessage = brMessage;
                        itemListReceived?.Invoke(breponseMessage.path, breponseMessage.Items);
                    }                    
                }
            }
            else if (message is ThumbnailResponseMessage)
            {
                ThumbnailResponseMessage trMessage = message as ThumbnailResponseMessage;
                thumbnailReceived?.Invoke(trMessage.ID, trMessage.Data);
            }
        }
    }

    public class BrowseCondition
    {
        public string Path { get; set; }
        public int MaxItemCount { get; set; }
        public int Offset { get; set; }
    }
    //把这些已知资料类型定义成string，是为了复用请求浏览某一路径的逻辑。相当于是发了一个特殊路径。双方收到这些特殊路径时做特殊处理。
    public class BrowseLocation
    {

        public const string IMAGE = "__PATH_IMAGE__";
        public const string MUSIC = "__PATH_MUSIC__";
        public const string VIDEO = "__PATH_VIDEO__";
        public const string DOCUMENT = "__PATH_DOCUMENT__";
        public const string APP = "__PATH_APP__";
        /// <summary>
        /// Connect默认的存储路径。
        /// </summary>
        public const string CONNECT2_DEFAULT = "__PATH_DEFAULT__";
        public const string DOWNLOAD = "__PATH_DOWNLOAD__";

    }
}