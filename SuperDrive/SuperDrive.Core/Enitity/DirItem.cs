using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using SuperDrive.Core.Annotations;
using SuperDrive.Core.Business;
using Util = SuperDrive.Core.Support.Util;

namespace SuperDrive.Core.Enitity
{
        [JsonObject(MemberSerialization.OptIn)]
        public class DirItem : AbstractFileItem
        {
                DirectoryInfo _dirInfo;
                List<AbstractFileItem> _children;
                //无参数构造函数用于反序列化
                public DirItem() : base(ItemType.Directory)
                {
                }

                public override string ToString() => $"Dir:{AbsolutePath},[{Id}]";

                public DirItem([NotNull] DirectoryInfo dirInfo) : this()
                {
                        _dirInfo = dirInfo;
                        Name = dirInfo.Name;
                        AbsolutePath = dirInfo.FullName;
                }

                public override bool Exists => DirInfo?.Exists ?? false;

                public override long GetLength()
                {
                        if (Length == -1) Length = Children.Sum(c => c.GetLength());
                        return Length;
                }

                public IEnumerable<AbstractFileItem> Children
                {
                        get
                        {
                                if (_children != null) return _children;
                                if (IsRemote)
                                {
                                        var conv = Conversation as IRemoteListableConversation;
                                        if (conv == null) return null;

                                        var task = conv.GetDirChildren(this);
                                        task.ConfigureAwait(false);
                                        var list = task.Result;
                                        //在内部调用set很奇怪？目的是为了利用Set中对child的设置。
                                        Children = list.Select(c => c as AbstractFileItem).Where(c => c != null);
                                }
                                else
                                {
                                        if (!NamedFolders.IsNamedFolder(Name) && DirInfo?.Exists != true) return null;

                                        //这个Get的实现默认是按照本地目录来的。但NamedFolder这项检测有问题。即便是NamedFolder，如果是远程的，也不应该在本地获取。
                                        var t = Env.FileSystem.GetChildren(this);
                                        t.ConfigureAwait(false);
                                        Children = t.Result; //get内部调用set?!    
                                }
                                return _children;
                        }
                        set
                        {
                                _children = new List<AbstractFileItem>(value);
                                foreach (var af in _children)
                                {
                                        af.AbsolutePath = af.AbsolutePath ?? Path.Combine(AbsolutePath, af.Name);
                                        af.IsRemote = IsRemote;
	                                af.Parent = this;
                                        AddChild(af);

	                                af.StateChanged += (sendable, state) =>
	                                {
		                                var af1 = sendable as AbstractFileItem;
		                                if (af1 == null || !af1.IsTransferEnd()) return;

		                                //如果单单一个文件传输错误，文件夹会继续传输。当所有item都不在传输的时候会改变整个文件夹的状态。
		                                if (!Children.All(c => c.IsTransferEnd())) return;

		                                TransferState = Children.Any(c => c.TransferState == TransferState.Error)
			                                ? TransferState.Error
			                                : TransferState.Completed;
		                                if (TransferState == TransferState.Completed)
			                                TransferredLength = Length;
	                                };
                                }
                        }
                }
		
                public void AddChild(AbstractFileItem af)
                {
                        af.TopLevelDir = TopLevelDir ?? this;
                        af.ConversationID = ConversationID;
                }

                DirectoryInfo DirInfo
                {
                        get
                        {
                                if (_dirInfo == null && AbsolutePath != null)
                                {
                                        _dirInfo = new DirectoryInfo(AbsolutePath);
                                }
                                return _dirInfo;
                        }

                }

                public override object Clone()
                {
                        if (DirInfo != null)
                                return new DirItem(DirInfo);

                        return new DirItem
                        {
                                Id = Id,
                                Name = Name,
                                RelativePath = RelativePath,
                                Length = Length,
                        };
                }

                public string StartFolderName
                {
                        get
                        {
                                if (AbsolutePath == null) return null;
                                int index = AbsolutePath.IndexOf('/');
                                return index == -1 ? AbsolutePath : AbsolutePath.Substring(0, index);
                        }
                }

                public override string FolderPathString => AbsolutePath;

                internal bool IsTopLevelDir()
                {
                        return NamedFolders.IsNamedFolder(AbsolutePath);
                }

                public bool IsAbsolutePath()
                {
                        return NamedFolders.IsNamedFolder(StartFolderName);
                }

                internal AbstractFileItem FindChildRecursive(DirectoryInfo dir, string itemId)
                {
                        Stack<DirectoryInfo> qpaths = new Stack<DirectoryInfo>();
                        var curDir = dir;
                        //把要查找的目录，逐级添加到堆栈里面。添加这个目录本身，但不添加DirItem这一级。
                        while (curDir != null && curDir.Name != Name)
                        {
                                qpaths.Push(curDir);
                                curDir = dir.Parent;
                        }
                        //要查找的目录的父目录里面，没有当前目录，说明没有隶属关系。
                        if (curDir == null) return null;

                        DirItem curDirItem = this;
                        while (true)
                        {
                                var af = curDirItem.Children.FirstOrDefault(c => c.Id == itemId);
                                if (af != null)
                                {
                                        af.TopLevelDir = this;
                                        return af;
                                }

                                if (qpaths.Count == 0) return null;
                                curDir = qpaths.Pop();
                                curDirItem = curDirItem.Children.FirstOrDefault(c => c.AbsolutePath == curDir.FullName) as DirItem;
                                if (curDirItem == null) return null;
                        }
                }
        }

        public class NamedFolders
        {
                public const string Image = "__IMAGE__";
                public const string Music = "__MUSIC__";
                public const string Video = "__VIDEO__";
                public const string Default = "__DEFAULT__";

                public static DirItem ImageDir = new DirItem { Name = Image, AbsolutePath = Image };
                public static DirItem MusicDir = new DirItem { Name = Music, AbsolutePath = Music };
                public static DirItem VideoDir = new DirItem { Name = Video, AbsolutePath = Video };
                public static DirItem DefaultDir = new DirItem { Name = Default, AbsolutePath = Default };

                public static bool IsNamedFolder(string name)
                {
                        return NamesList.Contains(name);
                }

                internal static DirItem GetFolderById(string dirItemId)
                {
                        return FolderList.FirstOrDefault(d => Util.ToBase64(d.AbsolutePath) == dirItemId) ?? ImageDir;
                }

                internal static bool IsNamedFolderId(string dirItemId)
                {
                        return NamesList.Any(s => Util.ToBase64(s) == dirItemId);
                }

                private static IReadOnlyList<DirItem> _namedFolderList;
                private static IReadOnlyList<DirItem> FolderList
                {
                        get
                        {
                                return _namedFolderList ?? (_namedFolderList = typeof(NamedFolders).GetTypeInfo()
                                    .DeclaredFields
                                    .Where(fi => fi.FieldType == typeof(DirItem))
                                    .Select(fi => fi.GetValue(null) as DirItem)
                                    .ToList());
                        }
                }

                private static IReadOnlyList<string> _names;
                private static IReadOnlyList<string> NamesList
                {
                        get
                        {
                                return _names ?? (_names = typeof(NamedFolders).GetTypeInfo()
                                    .DeclaredFields
                                    .Where(fi => fi.FieldType == typeof(string)) //fi.IsPublic && fi.IsStatic && fi.IsLiteral && !fi.IsInitOnly && 
                                    .Select(fi => fi.GetValue(null).ToString())
                                    .ToList());
                        }
                }


        }
}
