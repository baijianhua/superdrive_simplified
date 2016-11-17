using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using Xamarin.Forms;

namespace SuperDrive.Core.UI.Controls
{
        public static class UiUtil
        {
                public const int Transparent = 0;
                public const int NotTransparent = 1;
                public static readonly string DefaultNs = typeof(App).Namespace;

                internal static ImageSource ImageSourceFromResource(string source)
                {
                        var imageSource = ImageSource.FromResource($"{DefaultNs}.Images.{source}");
                        return imageSource;
                }
        }
}
