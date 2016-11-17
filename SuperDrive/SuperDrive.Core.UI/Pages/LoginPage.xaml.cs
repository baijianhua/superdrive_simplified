using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SuperDrive.Core.UI.Controls;
using Xamarin.Forms;

namespace SuperDrive.Core.UI.Pages
{
        public partial class LoginPage : ToolbarContentView
        {
                public LoginPage(MainPage parent) : base(parent)
                {
                        InitializeComponent();
                }

                private void Button_OnClicked(object sender, EventArgs e)
                {
                        
                }

                private void BtnReturn_OnClicked(object sender, EventArgs e)
                {
                        MainPage.ShowPage<SettingPage>(null);
                }

                protected internal override SizeRequest MeasureToolBar(double width, double height)
                {
                        return ToolBar.Measure(width, height);
                }
        }
}
