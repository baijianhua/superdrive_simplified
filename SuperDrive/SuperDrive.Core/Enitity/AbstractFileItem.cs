using System.IO;
using System.Security.Cryptography;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Enitity
{
        public abstract class AbstractFileItem : Item
        {
                private const int LengthPriorityAdjustor = 1024 * 1024 * 1; //1MB
                private const int MaxAdjustor = 2;

                public override int GetHashCode() => Id.GetHashCode();
                public virtual long GetLength() => Length;


                public override bool Equals(object obj)
                {
                        var af = obj as AbstractFileItem;
                        return af != null && af.Id == Id;
                }

                public static bool operator ==(AbstractFileItem rec1, AbstractFileItem rec2)
                {
                        return Equals(rec1, rec2);
                }

                public static bool operator !=(AbstractFileItem rec1, AbstractFileItem rec2)
                {
                        return !(rec1 == rec2);
                }


                private string _fileId;
                //远程item的absolute path是null，但Id不是，Id会在传输的时候，序列化过来。
                public override string Id
                {
                        get
                        {
                                //为什么要把路径变成Id? 
                                //*路径在远程没有意义。
                                //*为了加密？这个太容易解密。
                                return _fileId ?? (_fileId = Util.ToBase64(AbsolutePath));
                        }
                        set
                        {
                                _fileId = value;
                        }
                }
		
		/// <summary>
		/// 如果是目录，返回目录的绝对路径，如果是文件，返回文件所在目录。
		/// </summary>
                public abstract string FolderPathString { get; }
                internal AbstractFileItem(ItemType type) : base(type)
                {
                        // ReSharper disable once VirtualMemberCallInContructor
                        Length = -1; //故意如此。这样GetLength函数才会重新计算长度。在要求获取详细的信息之前，都不会有。
                }
                //public override int DynamicPriority
                //{
                //    get
                //    {
                //        //根据文件大小降低优先级。但这样做对不对？这不是把文件类型的优先级降的太低了？
                //        var pro = base.DynamicPriority;
                //        var adjustor = (int)(Length / LengthPriorityAdjustor);
                //        adjustor = adjustor > MaxAdjustor ? MaxAdjustor : adjustor;
                //        pro += adjustor;
                //        return pro;
                //    }
                //}

                internal void Create()
                {
                        if (Type == ItemType.Directory)
                        {
                                Directory.CreateDirectory(AbsolutePath);
                        }
                        else if (Type == ItemType.File)
                        {
                                FileStream f = File.Create(AbsolutePath);
                                f.Dispose();
                        }
                }
        }
}