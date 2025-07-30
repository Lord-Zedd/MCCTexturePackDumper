using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MCCTexturePackDumper
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		readonly TextureCollection _tc;
		public CollectionView _view;
		readonly string _title = "MCC Texture Pack Dumper";

		public MainWindow()
		{
			InitializeComponent();
			_tc = new TextureCollection();
			DataContext = _tc;
			Title = _title;
		}

		public bool DoFilter(object obj)
		{
			UIBitmap item = (UIBitmap)obj;

			if (!string.IsNullOrEmpty(filterBox.Text))
			{
				if (item.Name.Contains(filterBox.Text, StringComparison.InvariantCultureIgnoreCase))
					return true;

				return false;
			}

			return true;
		}

		private void filterBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			CollectionViewSource.GetDefaultView(texturesList.ItemsSource).Refresh();
		}

		private void Load_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.RestoreDirectory = true;
			ofd.Filter = "Texture Pack File (*.perm.bin)|*.perm.bin";
			if (!(bool)ofd.ShowDialog()!)
				return;

			_tc.LoadPack(ofd.FileName);

			_view = (CollectionView)CollectionViewSource.GetDefaultView(texturesList.ItemsSource);
			_view.Filter = DoFilter;
			Title = $"{_title} - {_tc.Name}";
		}

		private void SaveTex_Click(object sender, RoutedEventArgs e)
		{
			if (_tc.SelectedItem == null)
				return;
			UIBitmap selected = _tc.SelectedItem;

			SaveFileDialog sfd = new SaveFileDialog
			{
				RestoreDirectory = true,
				Title = "Save Texture",
				Filter = "PNG Image (*.png)|*.png;|Raw Data (Debug) (*.bin)|*.bin;",
				FileName = selected.Name,
			};
			if (!(bool)sfd.ShowDialog()!)
				return;

			switch (sfd.FilterIndex)
			{
				case 1:
					using (FileStream file = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
					{
						BitmapEncoder encoder = new PngBitmapEncoder();
						encoder.Frames.Add(BitmapFrame.Create(selected.Image));
						encoder.Save(file);
					}
					break;
				case 2:
					using (FileStream file = new FileStream(sfd.FileName, FileMode.Create, FileAccess.Write))
					{
						file.Write(selected.OriginalTexture.SourcePixelData, 0, selected.OriginalTexture.SourcePixelData.Length);
					}
					break;
			}

			_tc.Status = $"Saved {selected.Name}";
		}
		private void SaveAll_Click(object sender, RoutedEventArgs e)
		{
			if (_tc.Items.Count <= 0 || !_tc.CanSave)
				return;

			OpenFolderDialog ofd = new OpenFolderDialog()
			{
				Title = "Select Output Directory",
				FolderName = _tc.Name
			};
			if (!(bool)ofd.ShowDialog()!)
				return;

			_tc.SaveAllTextures(ofd.FolderName!);
		}

		private void CancelSaveAll_Click(object sender, RoutedEventArgs e)
		{
			if (!_tc.CanCancel)
				return;

			_tc.Cancel();
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
			{
				UseShellExecute = true,
			});
			e.Handled = true;
			_tc.Status = $"Loading Rick Roll...";
		}
	}
}