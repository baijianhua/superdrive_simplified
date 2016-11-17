using System.Collections.ObjectModel;

namespace SuperDrive.Core.UI.vm
{
	public class MItem : ViewModelBase
	{
		string name;
		public string Name
		{
			set { SetProperty(ref name, value); }
			get { return name; }
		}
		string detail;
		public string Detail
		{
			set { SetProperty(ref detail, value); }
			get { return detail; }
		}
		//public string ImagePath { get; set; } = "SuperDriveCore.Images.test1.jpg";//"d:/temp/test.bmp";
		string path;
		public string ImagePath { get; set; } = "http://www2.autoimg.cn/newsdfs/g16/M03/38/7C/620x0_0_autohomecar__wKgH11fWbwCATMtNAAtzxXjRWys782.jpg";
	}
	public class TestVM : ViewModelBase
	{
		ObservableCollection<MItem> items = new ObservableCollection<MItem>();

		public ObservableCollection<MItem> Items
		{
			get { return items; }
		}
		public string VMName { get; set; } = "VM Name";
		public string MyImageString { get; set; } = "http://www2.autoimg.cn/newsdfs/g16/M03/38/7C/620x0_0_autohomecar__wKgH11fWbwCATMtNAAtzxXjRWys782.jpg";//"file:///d:/temp/test1.jpg";

		public TestVM()
		{

			//MyImage = new BitmapImage(new Uri());
		}

		internal void Load(int max)
		{
			Items.Clear();
			for (var i = 0; i < max; i++)
			{
				var item = new MItem()
				{
					Name = $"name[{i}]",
					Detail = "Detail1",
					ImagePath = MyImageString
				};
				Items.Add(item);
			}
		}
	}
}
