using System;
using System.Windows.Input;
using MR.Gestures;
using Xamarin.Forms;
using ContentView = Xamarin.Forms.ContentView;
using Label = Xamarin.Forms.Label;

namespace SuperDrive.Core.UI.Controls
{
        public partial class ImageButton : MR.Gestures.ContentView
        {
                private Color oldColor = Color.Transparent;
                private Color oldBackgroudColor = Color.Transparent;
                public ImageButton()
                {
                        InitializeComponent();
                        TextLabel.FontSize = this.FontSize;
                        //TextLabel.WidthRequest = ImageWidthRequest;
                        FrameBorder.Down += (sender, args) =>
                        {
                                FrameBorder.OutlineColor = Color.Red;
                                FrameBorder.BackgroundColor = Color.Silver;
                        };
                        oldColor = FrameBorder.OutlineColor;
                        oldBackgroudColor = FrameBorder.BackgroundColor;
                        FrameBorder.Up += (_, __) =>
                        {
                                FrameBorder.OutlineColor = oldColor;
                                FrameBorder.BackgroundColor = oldBackgroudColor;
                        };
                        //HeightRequest = ImageHeightRequest + TextLabel.HeightRequest;
                        PropertyChanged += (sender, args) =>
                        {
                                var im = sender as ImageButton;
                                if (im == null || args.PropertyName != nameof(IsEnabled)) return;

                                im.BackgroundColor = im.IsEnabled ? Color.Default : Color.Gray;
                        };
                }

                public static readonly BindableProperty ImageWidthRequestProperty = BindableProperty.Create(
                    nameof(ImageWidthRequest),
                    typeof(double),
                    typeof(ImageButton),
                    default(double),
                    propertyChanged: (bindable, oldValue, newValue) =>
                    {
                            ((ImageButton)bindable).Image.WidthRequest = (double)newValue;
                    });

                public static readonly BindableProperty CommandProperty = BindableProperty.Create(
                    nameof(Command),
                    typeof(ICommand),
                    typeof(ImageButton),
                    null,
                    propertyChanged: (bindable, oldValue, newValue) =>
                    {
                            //((ImageButton)bindable).Image.HeightRequest = (double)newValue;
                    });

                public static readonly BindableProperty ImageHeightRequestProperty = BindableProperty.Create(
                    nameof(ImageHeightRequest),
                    typeof(double),
                    typeof(ImageButton),
                    default(double),
                    propertyChanged: (bindable, oldValue, newValue) =>
                    {
                            ((ImageButton)bindable).Image.HeightRequest = (double)newValue;
                    });


                public static readonly BindableProperty SourceProperty = BindableProperty.Create(
                    nameof(Source),
                    typeof(ImageSource),
                    typeof(ImageButton),
                    null,
                    propertyChanged: (bindable, oldValue, newValue) =>
                    {
                            var me = (ImageButton)bindable;
                            var image = me.Image;
                            image.Source = (ImageSource)newValue;
                    });

                public double ImageHeightRequest
                {
                        get { return (double)GetValue(ImageHeightRequestProperty); }
                        set { SetValue(ImageHeightRequestProperty, value); }
                }

                public double ImageWidthRequest
                {
                        get { return (double)GetValue(ImageWidthRequestProperty); }
                        set { SetValue(ImageWidthRequestProperty, value); }
                }


                [TypeConverter(typeof(ImageSourceConverter))]
                public ImageSource Source
                {
                        get { return (ImageSource)GetValue(SourceProperty); }
                        set { SetValue(SourceProperty, value); }
                }

                public static readonly BindableProperty TextProperty =
                    BindableProperty.Create(
                        "Text",
                        typeof(string),
                        typeof(ImageButton),
                        null,
                        propertyChanged: (bindable, oldValue, newValue) =>
                        {
                                ((ImageButton)bindable).TextLabel.Text = (string)newValue;
                        });

                public static readonly BindableProperty FontSizeProperty =
                    BindableProperty.Create(
                        "FontSize",
                        typeof(double),
                        typeof(ImageButton),
                        Device.GetNamedSize(NamedSize.Default, typeof(Label)),
                        propertyChanged: (bindable, oldValue, newValue) =>
                        {
                                ImageButton me = (ImageButton)bindable;
                                me.TextLabel.FontSize = (double)newValue;
                        });

                public event EventHandler Clicked;


                public string Text
                {
                        set { SetValue(TextProperty, value); }
                        get { return (string)GetValue(TextProperty); }
                }

                [TypeConverter(typeof(FontSizeConverter))]
                public double FontSize
                {
                        set { SetValue(FontSizeProperty, value); }
                        get { return (double)GetValue(FontSizeProperty); }
                }

                public ICommand Command
                {
                        set { SetValue(CommandProperty, value); }
                        get { return (ICommand)GetValue(CommandProperty); }
                }
                private void FrameBorder_OnTapped(object sender, TapEventArgs e)
                {
                        Clicked?.Invoke(this, null);
                }
        }
}
