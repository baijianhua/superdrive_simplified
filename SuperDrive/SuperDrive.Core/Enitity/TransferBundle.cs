using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SuperDrive.Core.Support;

namespace SuperDrive.Core.Enitity
{
	public class TransferBundle : SendableBase, IProgressable
	{
		private ObservableCollection<Item> ItemsInternal { get; set; } = new ObservableCollection<Item>();
		private long _length = -1;
		public event Action<IProgressable, long> Progressed;
		public long TransferredLength { get; set; }

		public void Progress(int length)
		{
			if (TransferredLength == 0) TransferState = TransferState.Transferring;

			TransferredLength += length;
			Progressed?.Invoke(this, length);

			if (TransferredLength >= Length) TransferState = TransferState.Completed;
		}

		public override long Length
		{
			get
			{
				if (_length != -1) return _length;

				_length = ItemsInternal.Sum(i => i.Length);
				return _length;
			}
			set { _length = value; }
		}
		private ReadOnlyObservableCollection<Item> _roItems;
		public ReadOnlyObservableCollection<Item> Items
			=> _roItems ?? (_roItems = new ReadOnlyObservableCollection<Item>(ItemsInternal));

		internal void SetItems(IEnumerable<Item> items) => ItemsInternal = new ObservableCollection<Item>(items);

		public void AddItem(Item item)
		{
			item.StateChanged += (ii, state) =>
			{
				if (!ii.IsTransferEnd() || !ItemsInternal.All(iii => iii.IsTransferEnd())) return;

				TransferState = ItemsInternal.Any(iii => iii.TransferState == TransferState.Error) 
					?TransferState.Error 
					: TransferState.Completed;
				if (TransferState == TransferState.Completed) 
					TransferredLength = Length;
			};
			ItemsInternal.Add(item);
		}
		public DirItem FindTopLevelDir(DirectoryInfo dir)
		{
			while (dir != null)
			{
				var parent = ItemsInternal.FirstOrDefault(p => p.AbsolutePath == dir.FullName);
				if (parent != null)
				{
					return parent as DirItem;
				}
				dir = dir.Parent;
			}
			return null;
		}

		public AbstractFileItem FindItem(string itemId)
		{
			var item = ItemsInternal.FirstOrDefault(c => c.Id == itemId);
			if (item != null) return item as AbstractFileItem;
			//如果不是顶层的Item,先找到顶层的Dir.
			var path = Util.FromBase64(itemId);
			var fi = new FileInfo(path);
			var dir = fi.Exists ? fi.Directory : new DirectoryInfo(path);
			if (!dir.Exists) return null;

			var topLevelDir = FindTopLevelDir(dir);
			var af1 = topLevelDir?.FindChildRecursive(dir, itemId);
			return af1;
		}
	}
}
