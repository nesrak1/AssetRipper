﻿using AssetRipper.Assets.Collections;
using AssetRipper.Assets.IO.Reading;
using AssetRipper.Assets.IO.Writing;
using AssetRipper.Import.IO.Extensions;
using AssetRipper.Import.Utils;
using K4os.Compression.LZ4;

namespace AssetRipper.Export.Modules.Shaders.ShaderBlob
{
	public sealed class ShaderSubProgramBlob
	{
		public void Read(AssetCollection shaderCollection, byte[] compressedBlob, uint[] offsets, uint[] compressedLengths, uint[] decompressedLengths)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				uint offset = offsets[i];
				uint compressedLength = compressedLengths[i];
				uint decompressedLength = decompressedLengths[i];

				ReadPlatformBlob(shaderCollection, compressedBlob, offset, compressedLength, decompressedLength, i);
			}
		}

		private void ReadPlatformBlob(AssetCollection shaderCollection, byte[] compressedBlob, uint offset, uint compressedLength, uint decompressedLength, int segment)
		{
			byte[] decompressedBuffer = new byte[decompressedLength];
			LZ4Codec.Decode(compressedBlob, (int)offset, (int)compressedLength, decompressedBuffer, 0, (int)decompressedLength);

			MemoryStream blobMem = new MemoryStream(decompressedBuffer);
			AssetReader blobReader = new AssetReader(blobMem, shaderCollection);
			if (segment == 0)
			{
				Entries = blobReader.ReadAssetArray<ShaderSubProgramEntry>();
				RawBlobReaders = new AssetReader[Entries.Max(e => e.Segment) + 1];
				BlobObjectCache = new object[Entries.Length];
				//SubPrograms = ArrayUtils.CreateAndInitializeArray<ShaderSubProgram>(Entries.Length);
			}
			RawBlobReaders[segment] = blobReader;
			//ReadBlob(blobReader, segment);
		}

		public ShaderSubProgram ReadBlobAsShaderSubProgram(int blobIndex, int paramBlobIndex = -1)
		{
			ShaderSubProgramEntry entry = Entries[blobIndex];

			if (BlobObjectCache[blobIndex] != null)
			{
				return BlobObjectCache[blobIndex] as ShaderSubProgram ?? throw new Exception("Object was not a ShaderSubProgram!");
			}
			AssetReader reader = RawBlobReaders[entry.Segment];
			reader.BaseStream.Position = entry.Offset;

			ShaderSubProgram shaderSubProgram = new();
			BlobObjectCache[blobIndex] = shaderSubProgram;
			shaderSubProgram.Read(reader);

			if (reader.BaseStream.Position != entry.Offset + entry.Length)
			{
				throw new Exception($"Read {reader.BaseStream.Position - entry.Offset} less than expected {entry.Length}");
			}

			if (paramBlobIndex != -1)
			{
				ShaderSubProgramEntry paramEntry = Entries[paramBlobIndex];

				AssetReader paramReader = RawBlobReaders[paramEntry.Segment];
				paramReader.BaseStream.Position = paramEntry.Offset;

				shaderSubProgram.ReadParams(paramReader, true);

				if (paramReader.BaseStream.Position != paramEntry.Offset + paramEntry.Length)
				{
					throw new Exception($"Read {paramReader.BaseStream.Position - paramEntry.Offset} less than expected {paramEntry.Length}");
				}
			}

			return shaderSubProgram;
		}

		/*
		public void Read(AssetCollection shaderCollection, byte[] compressedBlob, uint[] offsets, uint[] compressedLengths, uint[] decompressedLengths)
		{
			for (int i = 0; i < offsets.Length; i++)
			{
				uint offset = offsets[i];
				uint compressedLength = compressedLengths[i];
				uint decompressedLength = decompressedLengths[i];

				ReadBlob(shaderCollection, compressedBlob, offset, compressedLength, decompressedLength, i);
			}
		}

		private void ReadBlob(AssetCollection shaderCollection, byte[] compressedBlob, uint offset, uint compressedLength, uint decompressedLength, int segment)
		{
			byte[] decompressedBuffer = new byte[decompressedLength];
			LZ4Codec.Decode(compressedBlob, (int)offset, (int)compressedLength, decompressedBuffer, 0, (int)decompressedLength);

			using MemoryStream blobMem = new MemoryStream(decompressedBuffer);
			using AssetReader blobReader = new AssetReader(blobMem, shaderCollection);
			if (segment == 0)
			{
				Entries = blobReader.ReadAssetArray<ShaderSubProgramEntry>();
				SubPrograms = ArrayUtils.CreateAndInitializeArray<ShaderSubProgram>(Entries.Length);
			}
			ReadSegment(blobReader, segment);
		}

		private void ReadSegment(AssetReader reader, int segment)
		{
			for (int i = 0; i < Entries.Length; i++)
			{
				ShaderSubProgramEntry entry = Entries[i];
				if (entry.Segment == segment)
				{
					reader.BaseStream.Position = entry.Offset;
					SubPrograms[i].Read(reader);
					if (reader.BaseStream.Position != entry.Offset + entry.Length)
					{
						throw new Exception($"Read {reader.BaseStream.Position - entry.Offset} less than expected {entry.Length}");
					}
				}
			}
		}

		public void Write(AssetCollection shaderCollection, MemoryStream memStream, out uint[] offsets, out uint[] compressedLengths, out uint[] decompressedLengths)
		{
			int segmentCount = Entries.Length == 0 ? 0 : Entries.Max(t => t.Segment) + 1;
			offsets = new uint[segmentCount];
			compressedLengths = new uint[segmentCount];
			decompressedLengths = new uint[segmentCount];
			for (int i = 0; i < segmentCount; i++)
			{
				uint offset = (uint)memStream.Position;
				WriteBlob(shaderCollection, memStream, out uint compressedLength, out uint decompressedLength, i);

				offsets[i] = offset;
				compressedLengths[i] = compressedLength;
				decompressedLengths[i] = decompressedLength;
			}
		}

		private void WriteBlob(AssetCollection shaderCollection, MemoryStream memStream, out uint compressedLength, out uint decompressedLength, int segment)
		{
			using MemoryStream blobMem = new MemoryStream();
			using (AssetWriter blobWriter = new AssetWriter(blobMem, shaderCollection))
			{
				if (segment == 0)
				{
					blobWriter.WriteAssetArray(Entries);
				}

				WriteSegment(blobWriter, segment);
			}
			decompressedLength = (uint)blobMem.Length;

			byte[] source = blobMem.ToArray();

			byte[] target = new byte[LZ4Codec.MaximumOutputSize(source.Length)];
			int encodedLength = LZ4Codec.Encode(source, 0, source.Length, target, 0, target.Length);

			if (encodedLength < 0)
			{
				throw new Exception("Unable to compress sub program blob");
			}
			else
			{
				compressedLength = (uint)encodedLength;
				memStream.Write(target, 0, encodedLength);
			}
		}

		private void WriteSegment(AssetWriter writer, int segment)
		{
			for (int i = 0; i < Entries.Length; i++)
			{
				ShaderSubProgramEntry entry = Entries[i];
				if (entry.Segment == segment)
				{
					writer.BaseStream.Position = entry.Offset;
					SubPrograms[i].Write(writer);
				}
			}
		}
		*/

		public ShaderSubProgramEntry[] Entries { get; set; } = Array.Empty<ShaderSubProgramEntry>();
		public AssetReader[] RawBlobReaders { get; set; } = Array.Empty<AssetReader>();
		public object[] BlobObjectCache { get; set; } = Array.Empty<object[]>();
		//public ShaderSubProgram[] SubPrograms { get; set; } = Array.Empty<ShaderSubProgram>();

		public const string GpuProgramIndexName = "GpuProgramIndex";
	}
}
