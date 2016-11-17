using System.IO;

namespace ConnectTo.Foundation.Core
{
    public abstract class AbstractFileItem : SequencableItem
    {
        private const int LengthPriorityAdjustor = 1024 * 1024 * 1; //1MB
        private const int MaxAdjustor = 2;

        internal AbstractFileItem(ItemType type):base(type)
        {
        }
        public override int DynamicPriority
        {
            get
            {
                //根据文件大小降低优先级。但这样做对不对？这不是把文件类型的优先级降的太低了？
                var pro = base.DynamicPriority;
                var adjustor = (int)(Length / LengthPriorityAdjustor);
                adjustor = adjustor > MaxAdjustor ? MaxAdjustor : adjustor;
                pro += adjustor;
                return pro;
            }
        }

        internal void Create()
        {
            if(Type == ItemType.Directory)
            {
                Directory.CreateDirectory(AbsolutePath);
            }
            else if(Type == ItemType.File)
            {
                FileStream f = File.Create(AbsolutePath);
                f.Close();
            }
        }
    }
}