using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace SuperDrive.Core.UI
{
    public interface IScreenshotService
    {
        //http://forums.xamarin.com/discussion/comment/67380/#Comment_67380
        Stream Capture(View ve);
    }
}
