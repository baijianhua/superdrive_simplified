using System;
using System.Linq;
using System.Reflection;
using SuperDrive.Core.UI.Controls;
using Xamarin.Forms;

namespace SuperDrive.Core.UI.Pages
{
	public partial class MainPageReal : AbsoluteLayout
	{
		private MainPage _mainPage;
		public MainPageReal()
		{
			InitializeComponent();
			//GalleryLayout.CellClicked += Gallery_ItemClicked;
			DeviceList.Children.Clear();
		}

		internal MainPage MainPage
		{
			get { return _mainPage; }
			set
			{
				_mainPage = value;
				_browserPage = new SettingPage(MainPage);
				ContentFrame.Content = _browserPage;
			}
		}
		public async void Login_Clicked(object sender, EventArgs args)
		{
			MainPage.ShowPage<LoginPage>(null);
		}

		private void Settings_Clicked(object sender, EventArgs e)
		{
			MainPage.ShowPage<SettingPage>(null);
		}

		protected override void LayoutChildren(double x, double y, double width, double height)
		{
			if (width == -1 || height == -1) return;

			Size req = OperationBar.Measure(width, height).Request;
			Size taskListReq = TaskListFrame.Measure(width, height).Request;
			Size currentToolbarReq = _curPage.MeasureToolBar(width, height).Request;

			if (currentToolbarReq.Width + req.Width < width)
			{
				ToolbarSeperator.IsVisible = currentToolbarReq.Width + req.Width > width - 30;
				OperationBar.Layout(new Rectangle(width - req.Width, 0, req.Width, req.Height));
				ContentFrame.Layout(new Rectangle(0, 0, width, height - taskListReq.Height));
			}
			else
			{
				ToolbarSeperator.IsVisible = false;
				OperationBar.Layout(new Rectangle(0, 0, req.Width, req.Height));
				ContentFrame.Layout(new Rectangle(0, req.Height, width, height - taskListReq.Height));
			}

			TaskListFrame.Layout(new Rectangle(0, height - taskListReq.Height, taskListReq.Width, taskListReq.Height));
		}

		private ToolbarContentView _curPage = null;
		private SettingPage _browserPage;
		internal void ShowPage<TNewPage>(object data) where TNewPage : ToolbarContentView
		{
			var ci = typeof(TNewPage).GetTypeInfo().DeclaredConstructors.FirstOrDefault(c =>
			{
				var parameters = c.GetParameters();
				return parameters.Length == 1 && parameters[0].ParameterType == typeof(MainPage);
			});
			if (ci == null) throw new Exception($"Requester must have a constructor of ({nameof(Pages.MainPage)})");
			_curPage = (TNewPage)ci.Invoke(new object[] { MainPage });
			_curPage.HorizontalOptions = LayoutOptions.Fill;
			_curPage.VerticalOptions = LayoutOptions.Fill;
			_curPage.BindingContext = _curPage.BindingContext ?? this.BindingContext;
			ContentFrame.Content =_curPage;
		}

		public Layout<View> TaskList => TaskListFrame;

		private void BtnAddDevice_OnClicked(object sender, EventArgs e)
		{

		}
	}
}
