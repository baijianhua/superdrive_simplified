using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Business;
using SuperDrive.Core.Enitity;
using SuperDrive.Core.Support;

namespace SuperDrive.Core
{
        public abstract class Env
        {
                static Env _instance;
                private readonly SequencialTaskPool _sequencialTaskPool = new SequencialTaskPool("Env.TaskPool", 10);
                public static Logger Logger { get; private set; }
                public static FileSystem FileSystem { get; protected set; }
                public static Network Network { get; protected set; }
                public static DeviceType DeviceType { get; protected set; }
                public static Config Config { get; protected set; }
                public static string AppWorkingPath { get; protected set; }
                public static string DefaultConfigPath { get; protected set; }
                public static string UserName { get; set; }
                public static string DeviceName { get; set; }

                public static JsonSerializerSettings JsonSetting { get; private set; }

                public static void PostSequencialTask(Func<Task> task, Func<bool> isValid = null, Func<string> toStringImpl = null)
                {
                        var mtask = new MTask(task, isValid, toStringImpl);
                        _instance._sequencialTaskPool.PostTask(mtask);
                }
                public static void ShowMessage(string v) =>  _instance.ShowMessageImpl(v);
                protected abstract void ShowMessageImpl(string v);
                public static void Init(Env envImpl)
                {
                        if (_instance != null) throw new Exception("Env只应创建一个实例。有其他方法可重构");
                        _instance = envImpl;

                        //在这里要检查一下各个变量是否都设置好了。
                        //而C#的构造函数执行顺序是:先引用对象，再父类，再子类.
                        Util.CheckParam(Network != null);
                        Util.CheckParam(FileSystem != null);
                        Util.CheckParam(AppWorkingPath != null);
                        Util.CheckParam(DefaultConfigPath != null);
                        Util.CheckParam(UserName != null);
                        Util.CheckParam(DeviceName != null);

                        //Log和App逻辑没有关系，所以放在Env中是合适。
                        //Config呢？和逻辑有关系，但其实只是一个永久性存储。是不是也适合放在Env里面？
                        //Env最先初始化，然后应该初始化Config,最后初始化Log.其间有顺序依赖。因为未来Log有可能依赖Config.
                        _instance = envImpl;
                        JsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                        var configPath = Path.Combine(DefaultConfigPath, Config.CONFIG_FILENAME);
                        Config = Config.Load(envImpl, configPath);
                        Logger = new Logger();
                        Logger.Log($"Configpath={configPath}", nameof(Env));
                        _instance._sequencialTaskPool.Start();
                }
                public static void Dispose()
                {
                        Logger.Log("Dispose", nameof(Env));
                        Logger.Log("Stop sequencial task pool", nameof(Env));
                        _instance._sequencialTaskPool.Stop();
                        Logger.Log("After Stop sequencial task pool", nameof(Env));
                        Logger?.Dispose();
                        GC.SuppressFinalize(_instance);
                }

                public static async void DoLater(Func<bool> p, TimeSpan timeSpan)
                {
                        await Task.Delay(timeSpan);
                        p?.Invoke();
                }
        }

        public abstract class Network
        {
                public bool CanDiscover { get; internal set; } = true;

                public event Action<string> NetworkChanged = delegate { };
                public void OnNetworkChanged(string ips)
                {
                        _ips = GetIpAddressesImpl();
                        NetworkChanged?.Invoke(ips);
                }

                private IReadOnlyCollection<string> _ips;
                public IReadOnlyCollection<string> GetIpAddresses() => _ips ?? (_ips = GetIpAddressesImpl());

                protected virtual IReadOnlyCollection<string> GetIpAddressesImpl()
                {
                        throw new NotImplementedException();
                }
        }
        public abstract class FileSystem
        {
                public string DefaultDir { get; protected set; }
                public abstract Task<Stream> GetThumbnailStream(AbstractFileItem item);

                public virtual Task<object> GetNativeThumbnailImage(AbstractFileItem item)
                {
                        throw new NotImplementedException();
                }

                internal async Task<List<AbstractFileItem>> GetChildren(DirItem dir)
                {
                        var result = await GetChildrenImpl(dir);
                        foreach (var af in result)
                        {
                                af.ConversationID = dir.ConversationID;
                        }
                        return result;
                }

                protected abstract Task<List<AbstractFileItem>> GetChildrenImpl(DirItem dir);

                //AndroidMediaStore返回的文件长度和实际的不一致。所以这样定义。
                public virtual long NativeGetFileLength(FileInfo fileInfo)
                {
                        return fileInfo.Length;
                }

                public virtual Stream GetBitmapStream()
                {
                        return null;
                }

                private Stream GetImageStreamFromImageFolder(string name) => typeof(Env).GetTypeInfo().Assembly.GetManifestResourceStream($"SuperDrive.Core.Images.{name}");

		/// <summary>
		/// 如果已经存在，或者创建成功，都返回true
		/// </summary>
		/// <param name="dir"></param>
		/// <returns></returns>
		internal bool GetOrCreateDir(DirItem dir) 
		{
			DirectoryInfo di =  Directory.CreateDirectory(dir.AbsolutePath);
			return di != null;
		}

		public virtual void OpenLocation(string defaultDir)
                {
                        throw new NotImplementedException();
                }

                public Stream DefaultMusicIconStream => GetImageStreamFromImageFolder("music128.png");
                public Stream DefaultImageIconStream => GetImageStreamFromImageFolder("image128.png");
                public Stream DefaultFileIconStream => GetImageStreamFromImageFolder("file128.png");
                public Stream DefaultFolderIconStream => GetImageStreamFromImageFolder("folder128.png");
                public Stream DefaultDeviceStream => GetImageStreamFromImageFolder("mac64.png");
                public Stream DefaultVideoIconStream => GetImageStreamFromImageFolder("movie128.png");
        }
}

