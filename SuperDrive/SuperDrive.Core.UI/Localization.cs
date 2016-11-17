using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using SuperDrive.Core.UI.Controls;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;


namespace SuperDrive.Core.UI
{
    interface ILocalize
    {
        CultureInfo GetCurrentCultureInfo();
        void SetLocale(CultureInfo ci);
    }

    [ContentProperty("Text")]
    public class NlExtension : IMarkupExtension
    {
        readonly CultureInfo _ci;
        readonly string _resourceId = UiUtil.DefaultNs + ".Strings";// "UsingResxLocalization.Resx.AppResources";
        private ResourceManager resmgr; 
        public NlExtension()
        {
            if (Device.OS == TargetPlatform.iOS || Device.OS == TargetPlatform.Android)
            {
                _ci = DependencyService.Get<ILocalize>().GetCurrentCultureInfo();
            }

            resmgr = new ResourceManager(_resourceId, typeof(NlExtension).GetTypeInfo().Assembly);
        }

        public string Text { get; set; }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Text == null) return "";
            var translation = resmgr.GetString(Text, _ci);
            if (translation == null)
            {
#if DEBUG
                throw new ArgumentException(
                    String.Format("Key '{0}' was not found in resources '{1}' for culture '{2}'.", Text, _resourceId, _ci.Name),
                    "Text");
#else
                translation = Text; // HACK: returns the key, which GETS DISPLAYED TO THE USER
#endif
            }
            return translation;
        }
    }
}
