using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Messages;


namespace SuperDrive.Core.Enitity
{
        public enum FileType
        {
                Image,
                Music,
                Video,
                Document,
                Others
        }
        [JsonObject(MemberSerialization.OptIn)]
        public class FileItem : AbstractFileItem, ISeekable
        {
                public override string ToString()
                {
                        return $"{TransferState} FileItem:name={Name} path={AbsolutePath}";
                }

                private FileInfo _fileInfo;
                public FileInfo FileInfo
                {
                        get
                        {
                                if (AbsolutePath != null && Name != null)
                                {
                                        _fileInfo = new FileInfo(AbsolutePath);
                                }
                                return _fileInfo;
                        }
                        private set
                        {
                                _fileInfo = value;
                        }
                }
                private FileStream _filestream;

                public override bool Exists => FileInfo?.Exists ?? false;

                public string Extension
                {
                        get
                        {
                                string ret = "";
                                var idx = Name.LastIndexOf(".", StringComparison.Ordinal);
                                if (idx != -1 && idx != Name.Length)
                                {
                                        ret = Name.Substring(idx + 1);
                                }
                                return ret;
                        }
                }

                public FileType FileType { get; set; }
                public object Data { get; set; }


                //无参数构造函数用于反序列化
                public FileItem() : base(ItemType.File)
                {

                }
                public FileItem(FileInfo fileInfo) : this()
                {
                        FileInfo = fileInfo;
                        Name = fileInfo.Name;
                        AbsolutePath = fileInfo.FullName;
                }

                public override long GetLength()
                {
                        if (Length == -1) Length = FileInfo.Length;
                        return Length;
                }

                public override string FolderPathString => FileInfo?.DirectoryName;


                internal void RenameCurrent()
                {
                        if (FileInfo == null || !FileInfo.Exists) return;

                        var name = Path.GetFileNameWithoutExtension(FileInfo.FullName);
                        var ext = FileInfo.Extension;
                        var dirName = FileInfo.DirectoryName;
                        var newNameFormat = Support.Util.CombinePath(dirName, name + "({0})" + ext);
                        var newName = "";
                        for (int i = 1; i < 10000; i++)
                        {
                                newName = string.Format(newNameFormat, i);
                                if (!File.Exists(newName)) break;
                        }
                        Name = newName;

                        FileInfo = new FileInfo(newName);
                        Name = FileInfo.Name;
                        AbsolutePath = FileInfo.FullName;
                }

                protected override void OnPostCompleted()=>Close();
                protected override void OnErrored() => Close();
                protected override void OnCanceled() => Close();
                protected override void OnCompleted() => Close();
                internal FileStream Open(FileMode mode, FileAccess access)
                {
                        _filestream = FileInfo.Open(mode, access);
                        return _filestream;
                } 

                private void Close()
                {
                        _filestream?.Dispose();
                        _filestream = null;
                }

                internal async Task<int> ReadAsync(byte[] buffer)
                {
                        var count = await _filestream.ReadAsync(buffer, 0, buffer.Length);
                        ((IProgressable)this).Progress(count);
                        return count;
                }

                internal async Task WriteAsync(byte[] data, int? count = null)
                {
                        if (_filestream == null) return;

                        await _filestream.WriteAsync(data, 0, count ?? data.Length);
                        ((IProgressable)this).Progress(count ?? data.Length);
                }
                public void SeekTo(long position) => _filestream?.Seek(position, SeekOrigin.Begin);

    
                public override object Clone()
                {
                        if (FileInfo != null)
                                return new FileItem(FileInfo);
                        return new FileItem
                        {
                                Id = Id,
                                Name = Name,
                                RelativePath = RelativePath,
                                Length = Length,
                        };
                }
        }
}
