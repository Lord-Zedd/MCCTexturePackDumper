using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace MCCTexturePackDumper
{
	class TextureCollection : INotifyPropertyChanged
	{
		public ObservableCollection<UIBitmap> Items { get; private set; }
		public TexturePack? Pack { get; private set; }

		private CancellationTokenSource? _cts;
		private UIBitmap? _selectedItem;
		private string? _name;
		private string? _status;
		private bool _canSave;
		private bool _canCancel;

		public UIBitmap? SelectedItem
		{
			get => _selectedItem;
			set
			{
				_selectedItem = value;
				OnPropertyChanged();
			}
		}

		public string? Name
		{
			get => _name;
			set
			{
				_name = value;
				OnPropertyChanged();
			}
		}

		public string? Status
		{
			get => _status;
			set
			{
				_status = value;
				OnPropertyChanged();
			}
		}

		public bool CanSave
		{
			get => _canSave;
			set
			{
				_canSave = value;
				OnPropertyChanged();
			}
		}

		public bool CanCancel
		{
			get => _canCancel;
			set
			{
				_canCancel = value;
				OnPropertyChanged();
			}
		}

		public TextureCollection()
		{
			Items = new ObservableCollection<UIBitmap>();
			Status = "Initialized.";
		}

		public void LoadPack(string permPath)
		{
			Items.Clear();
			Pack = new TexturePack(permPath);
			Name = Pack.Name;
			foreach (ProcessedTexture texture in Pack.Textures)
				Items.Add(new UIBitmap(texture));

			Status = $"Loaded {Name}";
			CanSave = true;
		}

		public async void SaveAllTextures(string outDir)
		{
			try
			{
				CanSave = false;
				CanCancel = true;
				Status = "Saving all textures...";
				_cts = new CancellationTokenSource();

				var progress = new Progress<(int current, int total)>(p =>
				{
					Status = $"Saving all textures... [{p.current} / {p.total}]";
				});

				await SaveAllInternal(outDir, progress, _cts.Token);

				Status = $"Saved all textures!";
			}
			catch (OperationCanceledException)
			{
				Status = "Save all cancelled.";
			}
#if !DEBUG
			catch (Exception ex)
			{
				Status = "Save all stopped. (Error)";
			}
#endif
			finally
			{
				CanSave = true;
				CanCancel = false;
				_cts?.Dispose();
				_cts = null;
			}
		}

		public void Cancel()
		{
			_cts?.Cancel();
		}

		private async Task SaveAllInternal(string outDir, IProgress<(int current, int total)> progress, CancellationToken cancellationToken)
		{
			List<string> dupeList = new List<string>();

			int total = Items.Count;
			int current = 0;

			foreach (UIBitmap tex in Items)
			{
				cancellationToken.ThrowIfCancellationRequested();

				BitmapSource? src = await Task.Run(() => tex.GetImage(), cancellationToken);

				if (src == null)
					continue;

				string name = tex.Name;
				string nameLower = name.ToLowerInvariant();
				if (dupeList.Contains(nameLower))
					name = $"{tex.Name}_{tex.OriginalTexture.NameHash:X8}";
				else
					dupeList.Add(nameLower);

				using (MemoryStream ms = new MemoryStream())
				{
					BitmapEncoder encoder = new PngBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(src));
					encoder.Save(ms);
					await File.WriteAllBytesAsync(Path.Combine(outDir, name + ".png"), ms.ToArray(), cancellationToken);
				}

				current++;
				progress?.Report((current,  total));
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
