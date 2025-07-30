using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MCCTexturePackDumper
{
	public class UIBitmap
	{
		public ProcessedTexture OriginalTexture { get; private set; }

		public BitmapSource Image
		{
			get
			{
				return GetImage();
			}
		}

		public string Name
		{
			get { return OriginalTexture.Name; }
		}

		public int Width
		{
			get { return OriginalTexture.Width; }
		}

		public int Height
		{
			get { return OriginalTexture.Height; }
		}

		public TextureFormat Format
		{
			get { return OriginalTexture.Format; }
		}

		public string TextureSource
		{
			get { return $"{OriginalTexture.TextureSource}, 0x{OriginalTexture.TextureOffset:X} : 0x{OriginalTexture.TextureSize:X}"; }
		}

		public string TextureDataSource
		{
			get { return $"{OriginalTexture.TextureDataSource}, 0x{OriginalTexture.TextureDataOffset:X} : 0x{OriginalTexture.TextureDataSize:X}"; }
		}

		public BitmapSource GetImage()
		{
			byte[] pix = OriginalTexture.ConvertPixels();
			var result = BitmapSource.Create(OriginalTexture.Width, OriginalTexture.Height, 96, 96, PixelFormats.Bgra32, null, pix, 4 * OriginalTexture.Width);
			result.Freeze();
			return result;
		}

		public UIBitmap(ProcessedTexture originalTexture)
		{
			OriginalTexture = originalTexture;
		}
	}
}
