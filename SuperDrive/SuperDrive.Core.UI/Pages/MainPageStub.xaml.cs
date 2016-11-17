using SuperDrive.Core.UI.Controls;
using Xamarin.Forms;

// ReSharper disable once CheckNamespace
namespace SuperDrive.Core.UI.Pages
{

	public sealed partial class MainPage 
        {
                private DialogPage _currentPage;
                public MainPage()
                {
                        InitializeComponent();
                        MainPageContainer.MainPage = this;
                        //GalleryLayout.CellClicked += Gallery_ItemClicked;
                        ShowPage<SettingPage>(null);
                        //ShowPage<SettingPage>(null);
                }


                internal void ShowPage<T>(object data = null) where T : ToolbarContentView
                {
                        MainPageContainer.ShowPage<T>(data);
                }
                
                internal void ShowTask(View taskView)
                {
                        MainPageContainer.TaskList.Children.Insert(0, taskView);
                }

                internal void RemoveTask(View taskView)
                {
                        MainPageContainer.TaskList.Children.Remove(taskView);
                }
        }
}
