using System;
using System.Reflection;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SuperDrive.Core.UI.Controls
{
    [ContentProperty("Source")]
    public class ImageLoaderExtension : IMarkupExtension
    {
        public string Source { get; set; }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Source == null)
            {
                return null;
            }
            return UiUtil.ImageSourceFromResource(Source);
        }
    }
}
