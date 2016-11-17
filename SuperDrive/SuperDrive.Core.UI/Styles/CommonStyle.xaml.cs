using Xamarin.Forms;

namespace SuperDrive.Core.UI.Styles
{
    public partial class CommonStyle : ResourceDictionary
    {
        //要定义这个么一个类，才能找到这个Style，否则找不到。从Xamarin的源代码学来的方法。
        //让CommonStyle.Xaml与这个cs绑定，要不是创建Crossplat form - forms xaml page，要不是修改project文件，模仿page设置depend on.

        //找不到InitializeComponent往往是因为Xaml里面的class指错了。

        //不能编译嵌入资源，往往是因为项目本身的依赖项有问题，没编译成功。
        public CommonStyle()
        {
            InitializeComponent();
        }

        public CommonStyle(bool useCompiledXaml)
        {
            
        }
    }
}
