using System.IO;

namespace MCCTexturePackDumper
{
	public static class TextureConversion
	{
		public static byte[] ConvertTexture(ProcessedTexture texture)
		{
			using (MemoryStream ms = new MemoryStream(texture.SourcePixelData))
			{
				using (BinaryReader reader = new BinaryReader(ms))
				{
					switch (texture.Format)
					{
						case TextureFormat.A8R8G8B8:
							return FromA8R8G8B8(reader);

						case TextureFormat.DXT1:
							return FromDXT1(reader, texture.Width, texture.Height);
						case TextureFormat.DXT3:
							return FromDXT3(reader, texture.Width, texture.Height);
						case TextureFormat.DXT5:
							return FromDXT5(reader, texture.Width, texture.Height);

						default:
							{
								byte[] hackBuffer = new byte[texture.Width * texture.Height * 4];
								Array.Copy(texture.SourcePixelData, hackBuffer, Math.Min(texture.SourcePixelData.Length, hackBuffer.Length));
								return hackBuffer;
							}
							
					}
				}
			}
		}

		private static byte ExtractTo8Bit(ulong color, int firstBit, ulong mask)
		{
			ulong val = (color >> firstBit) & mask;
			double valD = val / (double)mask;
			return (byte)(valD * 255d + 0.5d);
		}

		private static uint AssembleBGRAPixel(byte a, byte r, byte g, byte b)
		{
			return (uint)(b | (g << 8) | (r << 16) | (a << 24));
		}

		private static byte[] FromA8R8G8B8(BinaryReader reader)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(ms))
				{
					while (reader.BaseStream.Position < reader.BaseStream.Length)
					{
						uint pix = reader.ReadUInt32();
						byte a = (byte)((pix >> 24) & 0xFF);
						byte b = (byte)((pix >> 16) & 0xFF);
						byte g = (byte)((pix >> 8) & 0xFF);
						byte r = (byte)(pix & 0xFF);

						writer.Write(AssembleBGRAPixel(a, r, g, b));
					}

					return ms.ToArray();
				}
			}
		}

		private static byte[] FromDXT1(BinaryReader reader, int width, int height)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(ms))
				{
					int xBlocks = (width + 3) / 4;
					int yBlocks = (height + 3) / 4;

					for (int y = 0; y < yBlocks; y++)
					{
						for (int x = 0; x < xBlocks; x++)
						{
							Decode565ColorBlock(reader, writer, width, x, y, null);
						}
					}

					return ms.ToArray();
				}
			}
		}

		private static byte[] FromDXT3(BinaryReader reader, int width, int height)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(ms))
				{
					int xBlocks = (width + 3) / 4;
					int yBlocks = (height + 3) / 4;

					for (int y = 0; y < yBlocks; y++)
					{
						for (int x = 0; x < xBlocks; x++)
						{
							var alpha = DecodePackedChannelBlock(reader);
							Decode565ColorBlock(reader, writer, width, x, y, alpha);
						}
					}

					return ms.ToArray();
				}
			}
		}

		private static byte[] FromDXT5(BinaryReader reader, int width, int height)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(ms))
				{
					int xBlocks = (width + 3) / 4;
					int yBlocks = (height + 3) / 4;

					for (int y = 0; y < yBlocks; y++)
					{
						for (int x = 0; x < xBlocks; x++)
						{
							var alpha = Decode8BitChannelBlock(reader);
							Decode565ColorBlock(reader, writer, width, x, y, alpha);
						}
					}

					return ms.ToArray();
				}
			}
		}

		private static void Decode565ColorBlock(BinaryReader reader, BinaryWriter writer, int width, int xBlock, int yBlock, byte[,]? alpha = null)
		{
			ushort color0 = reader.ReadUInt16();
			ushort color1 = reader.ReadUInt16();

			byte[] codeBytes = reader.ReadBytes(4);
			uint code = (uint)((codeBytes[2] << 24) | codeBytes[3] << 16 | (codeBytes[0] << 8) | codeBytes[1]);

			byte r0 = ExtractTo8Bit(color0, 11, 0x1F);
			byte g0 = ExtractTo8Bit(color0, 5, 0x3F);
			byte b0 = ExtractTo8Bit(color0, 0, 0x1F);

			byte r1 = ExtractTo8Bit(color1, 11, 0x1F);
			byte g1 = ExtractTo8Bit(color1, 5, 0x3F);
			byte b1 = ExtractTo8Bit(color1, 0, 0x1F);

			for (int k = 0; k < 4; k++)
			{
				int j = k ^ 1;
				for (int i = 0; i < 4; i++)
				{
					int pixDataStart = (width * (yBlock * 4 + j) * 4) + ((xBlock * 4 + i) * 4);
					uint codeDec = code & 0x3;
					code >>= 2;

					writer.BaseStream.Position = pixDataStart;

					byte a = (alpha == null) ? byte.MaxValue : alpha[i, j];

					if (codeDec == 0)
						writer.Write(AssembleBGRAPixel(a, r0, g0, b0));
					else if (codeDec == 1)
						writer.Write(AssembleBGRAPixel(a, r1, g1, b1));
					else if (color0 <= color1)
					{
						if (codeDec == 2)
						{
							byte r = Lerp(r0, r1, (codeDec - 1) / 2f);
							byte g = Lerp(g0, g1, (codeDec - 1) / 2f);
							byte b = Lerp(b0, b1, (codeDec - 1) / 2f);
							writer.Write(AssembleBGRAPixel(a, r, g, b));
						}
						else
							writer.Write(AssembleBGRAPixel(0, 0, 0, 0));
					}
					else
					{
						byte r = Lerp(r0, r1, (codeDec - 1) / 3f);
						byte g = Lerp(g0, g1, (codeDec - 1) / 3f);
						byte b = Lerp(b0, b1, (codeDec - 1) / 3f);
						writer.Write(AssembleBGRAPixel(a, r, g, b));
					}
				}
			}
		}

		private static byte[,] DecodePackedChannelBlock(BinaryReader reader)
		{
			ulong code = reader.ReadUInt16();
			code |= (ulong)reader.ReadUInt16() << 16;
			code |= (ulong)reader.ReadUInt16() << 32;
			code |= (ulong)reader.ReadUInt16() << 48;

			byte[,] channel = new byte[4, 4];

			for (int j = 0; j < 4; j++)
			{
				for (int i = 0; i < 4; i++)
				{
					ulong codeDec = code & 0xF;
					code >>= 4;

					channel[i, j] = ExtractTo8Bit(codeDec, 0, 0xF);
				}
			}

			return channel;
		}

		private static byte[,] Decode8BitChannelBlock(BinaryReader reader)
		{
			ushort first = reader.ReadUInt16();

			byte first0 = (byte)(first & 0xFF);
			byte first1 = (byte)((first & 0xFF00) >> 8);

			ulong code = reader.ReadUInt16();
			code |= (ulong)reader.ReadUInt16() << 16;
			code |= (ulong)reader.ReadUInt16() << 32;

			byte[,] channel = new byte[4, 4];

			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					ulong codeDec = code & 7;
					code >>= 3;

					if (codeDec == 0)
						channel[j, i] = first0;
					else if (codeDec == 1)
						channel[j, i] = first1;
					else if (first0 < first1)
					{
						if (codeDec == 6)
							channel[j, i] = byte.MinValue;
						else if (codeDec == 7)
							channel[j, i] = byte.MaxValue;
						else
							channel[j, i] = Lerp(first0, first1, (codeDec - 1) / 5f);
					}
					else
						channel[j, i] = Lerp(first0, first1, (codeDec - 1) / 7f);
				}
			}

			return channel;
		}

		private static byte Lerp(byte a, byte b, float fraction)
		{
			return (byte)((a * (1 - fraction)) + (b * fraction));
		}
	}
}
