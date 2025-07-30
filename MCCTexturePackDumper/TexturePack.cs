using System.IO;
using System.Text;

namespace MCCTexturePackDumper
{
	public enum BlendState : uint
	{
		None = 0x2782CCE6,
		Blend = 0xA3833FDE,
		Modulate = 0xA3833FDE,
		Additive = 0x69DAE2D1,
		PunchThru = 0x2B068C0A,
		Premultiplied = 0xFAB11CA1,
		Overlay = 0xEDE83382,
		ModulatedRGBSrcAlpha = 0x6EBDEDA0,
		Screen = 0xD668AB18,
	}

	public enum TextureFormat
	{
		A8R8G8B8,
		DXT1,
		DXT3,
		DXT5,
		R5G6B5,
		A1R5G5B5,
		X8,
		X16,
		Unk8,
		Unk9,
		BC6H_UF16,
		BC6H_SF16,
		BC7,
		BC7_SRGB,//used on xbone but theres no legal/easy detile solution so why bother decoding
	}

	public class TexturePack
	{
		private static string _permExt = ".perm.bin";
		private static string _permIdxExt = ".perm.idx";
		private static string _tempExt = ".temp.bin";

		public string Name { get; set; }

		public TexturePackFile? TempBin { get; set; }
		public TexturePackFile PermBin { get; set; }
		//idx here but the values dont make much sense so skip it

		public List<ProcessedTexture> Textures { get; set; }

		public TexturePack(string permPath)
		{
			string dir = Path.GetDirectoryName(permPath)!;
			Name = Path.GetFileName(permPath).ToLowerInvariant().Replace(_permExt, "");

			string tempPath = Path.Combine(dir, Name + _tempExt);
			string permIdxPath = Path.Combine(dir, Name + _permIdxExt);

			PermBin = new TexturePackFile(permPath);
			TempBin = null;

			if (File.Exists(tempPath))
				TempBin = new TexturePackFile(tempPath);

			Textures = new List<ProcessedTexture>();

			if (TempBin != null)
			{
				//offsets for temp is absolute, ignoring the actual chunks
				byte[] temp = File.ReadAllBytes(tempPath);
				foreach (Texture chunk in PermBin.Chunks.Where(x => x.UID == Texture.ChunkUID))
					Textures.Add(new ProcessedTexture(chunk, temp));
			}
			else
			{
				//idx file might unlock the true secrets but assuming Textures comes after TextureDatas every time works good enough so far.
				List<Texture> textures = PermBin.Chunks.OfType<Texture>().ToList();
				List<TextureData> textureDatas = PermBin.Chunks.OfType<TextureData>().ToList();
				for (int i = 0; i < textures.Count; i++)
					Textures.Add(new ProcessedTexture(textures[i], textureDatas[i]));
			}
		}
	}

	public class TexturePackFile
	{
		public List<Chunk> Chunks = new List<Chunk>();

		public TexturePackFile(string filePath)
		{
			using (FileStream fs = new FileStream(filePath, FileMode.Open))
			{
				using (BinaryReader br = new BinaryReader(fs))
				{
					while (fs.Position < fs.Length)
					{
						uint chunkKey = br.ReadUInt32();

						switch (chunkKey)
						{
							case Texture.ChunkUID:
								Chunks.Add(new Texture(br));
								break;
							case TextureData.ChunkUID:
								Chunks.Add(new TextureData(br));
								break;
							case Padding.ChunkUID:
								Chunks.Add(new Padding(br));
								break;
							case Alignment.ChunkUID:
								Chunks.Add(new Alignment(br));
								break;
							case Reflect.ChunkUID:
								Chunks.Add(new Reflect(br));
								break;
							case Zero.ChunkUID:
								Chunks.Add(new Reflect(br));
								break;
							case Shader.ChunkUID:
								Chunks.Add(new Shader(br));
								break;
							case ShaderData.ChunkUID:
								Chunks.Add(new ShaderData(br));
								break;

							default:
								throw new InvalidDataException($"unknown chunk type {chunkKey:X8}!");
						}
					}
				}
			}
		}

		private void GenerateIDXPaddingChunks(int pad)
		{
			Padding padChunk;
			Alignment alignment;
			if (pad <= 0x3F0)
			{
				padChunk = new Padding(pad);
			}
			else
			{
				padChunk = new Padding(0x3F0);
				alignment = new Alignment(pad - 0x3F0);
			}
			//add them or whatever if rebuilding becomes a thing
		}

		public static uint HashName(string name)
		{
			//for if rebuilding/adding becomes a thing
			byte[] capsName = Encoding.UTF8.GetBytes(name.ToUpperInvariant());
			return CRC32MPEG.CountCRC(capsName);
		}
	}

	public abstract class Chunk
	{
		public uint FileOffset { get; set; }
		public uint DataStart
		{ get { return FileOffset + 0x10; } }

		public uint UID { get; set; }
		public int Size1 { get; set; }
		public int Size2 { get; set; }
		public int Unknown { get; set; }

		public Chunk(BinaryReader br, uint uid)
		{
			FileOffset = (uint)br.BaseStream.Position - 4;
			UID = uid;
			Size1 = br.ReadInt32();
			Size2 = br.ReadInt32();
			Unknown = br.ReadInt32();
		}

		public Chunk()
		{ }
	}

	public class Texture : Chunk
	{
		public const uint ChunkUID = 0xCDBFA090;

		public uint NameHash { get; set; }//CRC32MPEG of name string
		public uint UnknownUID { get; set; }//possibly some generic "texture" identifier? always the same
		public string Name { get; set; }
		public TextureFormat Format { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int MipCount { get; set; }
		public int Depth { get; set; }
		public BlendState Blending { get; set; }
		public long Unknown3 { get; set; }
		public long TextureSize { get; set; }
		public long TextureOffset { get; set; }
		public uint MediaUID { get; set; }//does not appear to be a hash of the texture pack filename?
		public int Unknown4 { get; set; }
		public int Unknown5 { get; set; }
		public float Unknown6 { get; set; }//always 11.0?

		public long TextureChunkOffset
		{ get { return TextureOffset - 0x10; } }

		public Texture(BinaryReader br) : base(br, ChunkUID)
		{
			br.BaseStream.Seek(0x18, SeekOrigin.Current);
			NameHash = br.ReadUInt32();
			br.BaseStream.Seek(0x14, SeekOrigin.Current);
			UnknownUID = br.ReadUInt32();

			byte[] namebytes = br.ReadBytes(0x20);
			Name = Encoding.ASCII.GetString(namebytes).TrimEnd('\0');

			br.BaseStream.Seek(0x8, SeekOrigin.Current);

			int format = br.ReadInt32();
			if (!Enum.IsDefined(typeof(TextureFormat), format))
				throw new InvalidDataException($"unknown format {format}!");

			Format = (TextureFormat)format;

			br.BaseStream.Seek(0x4, SeekOrigin.Current);
			Width = br.ReadInt16();
			Height = br.ReadInt16();
			MipCount = br.ReadInt16();
			Depth = br.ReadInt16();
			Blending = (BlendState)br.ReadUInt32();
			Unknown3 = br.ReadInt64();
			TextureSize = br.ReadInt64();
			TextureOffset = br.ReadInt64();

			br.BaseStream.Seek(0x28, SeekOrigin.Current);
			MediaUID = br.ReadUInt32();

			br.BaseStream.Seek(0x1C, SeekOrigin.Current);
			Unknown4 = br.ReadInt32();

			br.BaseStream.Seek(0xEC, SeekOrigin.Current);
			Unknown5 = br.ReadInt32();
			Unknown6 = br.ReadSingle();

			br.BaseStream.Seek(0x8, SeekOrigin.Current);
		}
	}

	public class TextureData : Chunk
	{
		public const uint ChunkUID = 0x5E73CDD7;

		public byte[] PixelData { get; set; }

		public TextureData(BinaryReader br) : base(br, ChunkUID)
		{
			PixelData = br.ReadBytes(Size1);
		}
	}

	public class Padding : Chunk
	{
		public const uint ChunkUID = 0xDEADB0FF;

		public Padding(BinaryReader br) : base(br, ChunkUID)
		{
			//just padded to size with 0xBF, no need to read anything
			//but maxes out at x3F0?
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public Padding(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = size;
			Unknown = 0;
		}
	}

	public class Alignment : Chunk
	{
		public const uint ChunkUID = 0xC9DC5F62;

		public Alignment(BinaryReader br) : base(br, ChunkUID)
		{
			//just padded to size with 0x00, no need to read anything, though Unknown is also the size.
			//the usage in idx-based bins is odd but i guess it takes where padding leaves off to round the next chunk to x1000
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public Alignment(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = size;
			Unknown = size;
		}
	}

	public class Reflect : Chunk
	{
		public const uint ChunkUID = 0x616A903F;

		public Reflect(BinaryReader br) : base(br, ChunkUID)
		{
			//todo
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public Reflect(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = size;
			Unknown = 0;
		}
	}

	public class Zero : Chunk
	{
		public const uint ChunkUID = 0x0;

		public Zero(BinaryReader br) : base(br, ChunkUID)
		{
			//wtf
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public Zero(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = -1;
			Unknown = -1;
		}
	}

	public class Shader : Chunk
	{
		public const uint ChunkUID = 0x0C46AEEF;

		public Shader(BinaryReader br) : base(br, ChunkUID)
		{
			//todo
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public Shader(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = size;
			Unknown = 0;
		}
	}

	public class ShaderData : Chunk
	{
		public const uint ChunkUID = 0x985BE50C;

		public ShaderData(BinaryReader br) : base(br, ChunkUID)
		{
			//todo
			br.BaseStream.Seek(Size1, SeekOrigin.Current);
		}

		public ShaderData(int size)
		{
			UID = ChunkUID;
			Size1 = size;
			Size2 = size;
			Unknown = 0;
		}
	}

	public class ProcessedTexture
	{
		public uint NameHash { get; set; }
		public string Name { get; set; }
		public TextureFormat Format { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public int MipCount { get; set; }
		public int Depth { get; set; }
		public BlendState Blending { get; set; }
		public uint MediaUID { get; set; }

		public byte[] SourcePixelData { get; set; }

		public string TextureSource { get; set; }
		public uint TextureOffset { get; set; }
		public int TextureSize { get; set; }

		public string TextureDataSource { get; set; }
		public uint TextureDataOffset { get; set; }
		public int TextureDataSize { get; set; }


		public ProcessedTexture(Texture tex, TextureData texDat)
		{
			NameHash = tex.NameHash;
			Name = tex.Name;
			Format = tex.Format;
			Width = tex.Width;
			Height = tex.Height;
			MipCount = tex.MipCount;
			Depth = tex.Depth;
			Blending = tex.Blending;
			MediaUID = tex.MediaUID;

			SourcePixelData = new byte[tex.TextureSize];
			Array.Copy(texDat.PixelData, SourcePixelData, SourcePixelData.Length);

			TextureSource = TextureDataSource = "perm.bin";
			TextureOffset = tex.DataStart;
			TextureSize = tex.Size1;
			TextureDataOffset = texDat.DataStart;
			TextureDataSize = texDat.Size1;
		}

		public ProcessedTexture(Texture tex, byte[] pixelDat)
		{
			NameHash = tex.NameHash;
			Name = tex.Name;
			Format = tex.Format;
			Width = tex.Width;
			Height = tex.Height;
			MipCount = tex.MipCount;
			Depth = tex.Depth;
			Blending = tex.Blending;
			MediaUID = tex.MediaUID;

			SourcePixelData = new byte[tex.TextureSize];
			Array.Copy(pixelDat, tex.TextureOffset, SourcePixelData, 0, SourcePixelData.Length);

			TextureSource = "perm.bin";
			TextureDataSource = "temp.bin";
			TextureOffset = tex.DataStart;
			TextureSize = tex.Size1;
			TextureDataOffset = (uint)tex.TextureOffset + 0x10;
			TextureDataSize = (int)tex.TextureSize;
		}

		public byte[] ConvertPixels()
		{
			return TextureConversion.ConvertTexture(this);
		}
	}
}
