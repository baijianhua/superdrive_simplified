using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;

namespace SuperDrive.Util
{
    public class MTask
    {
        private object locker = new object();
        private bool isCanceled = false;
        public virtual bool IsValid()
        {
            lock (locker)
            {
                return !isCanceled || (_isValid?.Invoke() ?? true);
            }
        }

        public void Cancel()
        {
            lock (locker)
            {
                isCanceled = true;
            }
        }

        public Action ActualTask { get; set; }

        public MTask()
        {
            
        }

        private Func<string> _toStringImpl; 
        private Func<bool> _isValid;
        public MTask(Action actualTask, Func<bool> isValid,Func<string> toStringImpl = null )
        {
            ActualTask = actualTask;
            _isValid = isValid;
            _toStringImpl = toStringImpl;
        }

        public override string ToString()
        {
            if (_toStringImpl != null)
            {
                return _toStringImpl();
            }
            else
            {
                return base.ToString();
            }
            
        }
    }
}
