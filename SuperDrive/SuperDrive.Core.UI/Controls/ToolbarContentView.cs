using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuperDrive.Core.UI.Pages;
using Xamarin.Forms;

namespace SuperDrive.Core.UI.Controls
{
        public class ToolbarContentView: ContentView
        {
                protected MainPage MainPage;
                protected ToolbarContentView(MainPage mainPage)
                {
                        MainPage = mainPage;
                }
                protected internal virtual SizeRequest MeasureToolBar(double width, double height)
                {
                        return new SizeRequest(new Size(width,0));
                }
                protected internal virtual void UpdateData(object data) { }
        }
}
