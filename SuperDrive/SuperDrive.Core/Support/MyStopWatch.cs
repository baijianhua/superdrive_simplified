using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperDrive.Core.Support
{
    public class MyStopWatch
    {
        public string Name { get; set; } = "watch";
        private DateTime _start = DateTime.Now;

        public MyStopWatch(string name)
        {
            Name = name;
        }
        public MyStopWatch() { }

        public void Reset()
        {
            _start = DateTime.Now;
        }
        public int Elipsed => (int)(DateTime.Now - _start).TotalMilliseconds;
        public override string ToString()=>$"{Name}:{Elipsed}";
    }
}
