using System.Threading;
using Xamarin.Forms;
using XDevice = Xamarin.Forms.Device;

namespace SuperDrive.Core.UI
{

	public partial class App : Application
	{
		public App()
		{
			InitializeComponent();
			if (XDevice.OS == TargetPlatform.iOS || XDevice.OS == TargetPlatform.Android)
			{
				var ci = DependencyService.Get<ILocalize>().GetCurrentCultureInfo();
				Strings.Culture = ci; // set the RESX for resource localization
				DependencyService.Get<ILocalize>().SetLocale(ci); // set the Thread for locale-aware methods
			}

			MainPage = new Pages.MainPage();
			//MainPage = new Pages.TestPage();
			//MainPage = new Pages.Page2();
		}

		protected override void OnStart()
		{
			// Handle when your app starts
		}

		protected override void OnSleep()
		{
			// Handle when your app sleeps
		}

		protected override void OnResume()
		{
			// Handle when your app resumes
		}
	}
}
