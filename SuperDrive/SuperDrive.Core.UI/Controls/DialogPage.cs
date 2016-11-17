using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SuperDrive.Core.UI.Pages;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Xamarin.Forms.Xaml.Internals;
using Point = Xamarin.Forms.Point;
using XDevice = Xamarin.Forms.Device;

namespace SuperDrive.Core.UI.Controls
{
        [ContentProperty("Content")]
        public class DialogPage : TemplatedPage
        {
                private StackLayout _dialogFrame;
                private IPageController Pc => this as IPageController;
		private Label _toast = new Label {BackgroundColor = Color.Silver,FontSize = 16};
	        const string ToastAnimationName = "toastAnimation";

		public void ShowToast(string s)
		{
			if (_toast.IsVisible && _toast.Text == s) return;

			_toast.IsVisible = true;
			_toast.Text = s;
			if (_toast.AnimationIsRunning(ToastAnimationName)) _toast.AbortAnimation(ToastAnimationName);

			_toast.Animate(ToastAnimationName,d=> _toast.Opacity=d,1,0,length:2000u,finished:(v,r)=>_toast.IsVisible=false);
			ForceLayout();
	        }
                public DialogPage()

                {
                        _dialogFrame = new StackLayout { BackgroundColor = Color.FromHex("#CC777777"), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
			
                        //_toolbar.LoadFromXaml(typeof(ToolBar));
                        Pc.InternalChildren.Add(_dialogFrame);
			Pc.InternalChildren.Add(_toast);
                }
                
                protected override void LayoutChildren(double x, double y, double width, double height)
                {
                        //如果要求显示对话框，而且对话框也已经显示了，不再做布局。
                        if (_dialogIsShowing && _dialogFrame?.IsVisible == true) return;

                        Content.Layout(new Rectangle(x, y, width, height));
	                if (_toast.IsVisible)
	                {
				var size = _toast.Measure(width,height).Request;
				_toast.Layout(new Rectangle((width-size.Width)/2,70,size.Width,size.Height ));
			}
	                

                        if (_dialogIsShowing)
                        {
                                Debug.Assert(_dialogFrame != null, "_dialogFrame != null");
                                _dialogFrame.IsVisible = true;
                                _dialogFrame.Layout(new Rectangle(x, y, width, height));
                        }
                        else
                        {
                                Debug.Assert(_dialogFrame != null, "_dialogFrame != null");
                                _dialogFrame.IsVisible = false;
                        }
                }

              
		TaskCompletionSource<bool> _showDialogResult;
		
                public static readonly BindableProperty ContentProperty = BindableProperty.Create(nameof(Content), typeof(View), typeof(ContentPage), null, propertyChanged: OnContentChanged);
                private bool _dialogIsShowing;

                public View Content
                {
                        get { return (View)GetValue(ContentProperty); }
                        set { SetValue(ContentProperty, value); }
                }

                protected override void OnBindingContextChanged()
                {
                        base.OnBindingContextChanged();

                        View content = Content;
                        ControlTemplate controlTemplate = ControlTemplate;
                        if (content != null && controlTemplate != null)
                        {
                                SetInheritedBindingContext(content, BindingContext);
                        }
                }
                public static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
                {
                        var self = bindable as DialogPage;
                        var newElement = (Element)newValue;
                        if (self != null && self.ControlTemplate == null)
                        {
                                if (newValue != null)
                                {
                                        //每次都先绘制这个Content，才能让Dialog浮在最上层。
                                        self.Pc.InternalChildren.Insert(0, newElement);
                                }
                        }
                        else
                        {
                                if (newElement != null)
                                {
                                        SetInheritedBindingContext(newElement, bindable.BindingContext);
                                }
                        }
                }
        }
}
