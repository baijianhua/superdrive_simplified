using System;
using System.Collections.Generic;
using System.Text;

namespace SharableCode
{
    class FileItemImpl:FileItem
    {
        private FileInfo _fileInfo = null;
        public FileInfo FileInfo
        {
            get
            {
                if (_fileInfo == null)
                {
                    if (AbsolutePath != null && Name != null)
                    {
                        _fileInfo = new FileInfo(AbsolutePath);
                    }
                }
                return _fileInfo;
            }
            private set
            {
                _fileInfo = value;
            }
        }
        private FileStream filestream;

        public override bool Exists
        {
            get
            {
                return FileInfo == null ? false : FileInfo.Exists;
            }
        }

        public FileItemImpl(FileInfo fileInfo) : this()
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            AbsolutePath = fileInfo.FullName;
            Length = fileInfo.Length;
        }
        bool createFileMessageSent = false;
        //TODO 在Send结束后，需要启动一个等待动作，等待对方回复确认消息。
        protected override Message GetNextMessageImpl()
        {
            Debug.Assert(TransferState != TransferState.Error, @"Item状态已经出错，为什么还到了这里呢？");

            //上次的包!=null, 而且已经PostComplted
            if (IsPostCompleted)
            {
                return null;
            }
            FileDataMessage message = null;

            if (filestream == null)
            {
                try
                {
                    if (new FileInfo(AbsolutePath).Exists)
                    {
                        filestream = new FileStream(AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                }
                catch (Exception ex)
                {
                    //下面检查filestream==null,会进入错误状态。
                }
            }

            if (filestream == null)
            {
                ErrorCode = TransferErrorCode.NullFileStream;
                TransferState = TransferState.Error;
                return null;
            }


            if (!createFileMessageSent)
            {
                message = new FileDataMessage();
                message.Data = new byte[0];
                message.Offset = 0;
                message.Length = 0;
                message.ItemID = ID;
                message.ConversationID = ConversationID;
                createFileMessageSent = true;
                ((IProgressable)this).Progress(0);
            }
            else
            {
                try
                {
                    filestream.Seek(TransferredLength, SeekOrigin.Begin);
                    byte[] buffer = new byte[FileDataMessage.BUFFER_SIZE];
                    int count = filestream.Read(buffer, 0, FileDataMessage.BUFFER_SIZE);

                    if (count > 0)
                    {
                        message = new FileDataMessage();
                        if (count == FileDataMessage.BUFFER_SIZE)
                        {
                            message.Data = buffer;
                        }
                        else
                        {
                            byte[] tmp = new byte[count];
                            Array.Copy(buffer, tmp, count);
                            message.Data = tmp;
                        }

                        message.Offset = TransferredLength;
                        message.Length = count;
                        message.ItemID = ID;
                        message.ConversationID = ConversationID;
#if DEBUG
                        Env.Instance.CheckFileDataMessage(message,this);
#endif
                    }
                    else
                    {
                        ErrorCode = TransferErrorCode.ReadFileDataFailed;
                        TransferState = TransferState.PostCompleted;
                    }
                }
                catch (Exception e)
                {
                    TransferState = TransferState.Error;
                    Env.Instance.Logger.Log(LogLevel.Error, e, "FileItemRead Exception");
                    message = null;
                }
            }


            if (message != null)
            {
                if (message.Length > 0)
                {
                    //注意：不是每个类型的GetNextMessage的Message.SendCompleted都适合触发Progress。
                    //如果把这个方法添加到基类(SequencableItem.GetNextMessage中),因为那个方法会被递归调用，例如，文件夹会调用File的GetNextMessage(通过SequencableItem) ，message.SendComplted会被处理一次
                    //然后外层的东西调用文件夹的GetNextMessage返回的时候,message.SendCompleted又被调用了一次。
                    message.SendCompleted += o => ((IProgressable)this).Progress((int)message.Length);
                }
                //写在这里是为了处理空文件的情况。空文件发第一个消息，就会进入这里。

            }
            if (message == null)
            {
                Debug.WriteLine("Get a null file message");
            }
            if (TransferredLength + (message == null ? 0 : message.Length) == Length)
            {
                TransferState = TransferState.PostCompleted;
                //SendCompeleted会在父类中处理
            }
            return message;
        }

        internal void RenameExistingAndCreateNew()
        {
            if (FileInfo != null && FileInfo.Exists)
            {
                var name = Path.GetFileNameWithoutExtension(FileInfo.FullName);
                var ext = FileInfo.Extension;
                var dirName = FileInfo.DirectoryName;
                var newNameFormat = Util.CombinePath(dirName, name + "({0})" + ext);
                var newName = "";
                for (int i = 1; i < 10000; i++)
                {
                    newName = string.Format(newNameFormat, i);
                    if (!File.Exists(newName)) break;
                }

                FileInfo.MoveTo(newName);
            }
            FileInfo = new FileInfo(AbsolutePath);
        }

        public void UpdateLength()
        {
            if (FileInfo != null)
                Length = FileInfo.Length;
        }

        protected override void OnPostCompleted()
        {
            CloseFile();
        }

        protected override void OnErrored()
        {
            CloseFile();
        }

        protected override void OnCanceled()
        {
            CloseFile();
        }

        private void CloseFile()
        {
            filestream?.Close();
            filestream = null;
        }
        protected override void OnCompleted()
        {
            //Env.Instance.Logger.Error("Put fi " + ID + " Completed");
            CloseFile();
        }

        internal void Write(long offset, int length, byte[] data)
        {
            if (filestream == null)
            {
                OpenFileStream();
            }

            if (filestream != null)
            {
                filestream.Seek(offset, SeekOrigin.Begin);
                filestream.Write(data, 0, length);
                filestream.Flush();
                ((IProgressable)this).Progress(length);
            }

        }

        public void SeekTo(long position)
        {
            if (filestream == null)
            {
                OpenFileStream();
            }
            //如果这里不检查，那外面就得检查。因为异常总要有人处理。    
            if (filestream != null)
            {
                filestream.Seek(position, SeekOrigin.Begin);
            }

        }

        void OpenFileStream()
        {
            if (filestream == null)
            {
                try
                {
                    filestream = new FileStream(AbsolutePath, FileMode.OpenOrCreate);
                }
                catch (Exception ex)
                {
                    //打开文件错误，稍后处理。
                    ErrorCode = TransferErrorCode.OpenFileError;
                    TransferState = TransferState.Error;
                }
            }
        }

        public override object Clone()
        {
            if (this.FileInfo != null)
                return new FileItem(this.FileInfo);
            else
            {
                return new FileItem()
                {
                    ID = this.ID,
                    Name = this.Name,
                    RelativePath = this.RelativePath,
                    Length = this.Length,
                };
            }
        }
    }
}
