using System;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    public class MTask
    {
        //将这个定义为Function，而不是action，应该是有的地方用了async.而且可以追踪和取消、await顺序执行。
        public Func<Task> ActualTask { get; set; }
        public Func<bool> IsValidImpl { get; set; }
        public Func<string> ToStringImpl { get; set; }
        public object Key { get; set; }

        public MTask()
        {
        }

        public MTask(Func<Task> action)
        {
            ActualTask = action;
        }

        public MTask(Func<Task> actualTask, Func<bool> isValid,Func<string> toStringImpl = null )
        {
            ActualTask = actualTask;
            IsValidImpl = isValid;
            ToStringImpl = toStringImpl;
        }
        public virtual bool IsValid()
        {
            return IsValidImpl?.Invoke() ?? true;
        }

        public override string ToString()
        {
            return ToStringImpl == null? base.ToString() : ToStringImpl();
        }

        public override bool Equals(object obj)
        {
            var t1 = obj as MTask;
            bool ret = Key == null? this == obj : Key.Equals(t1?.Key);
            return ret;
        }
    }
}
