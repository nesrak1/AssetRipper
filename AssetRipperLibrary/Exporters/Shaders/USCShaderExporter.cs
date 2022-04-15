using AssetRipper.Core.Classes.Shader;
using AssetRipper.Core.Classes.Shader.Enums;
using AssetRipper.Core.Classes.Shader.Enums.GpuProgramType;
using AssetRipper.Core.Classes.Shader.Parameters;
using AssetRipper.Core.Classes.Shader.SerializedShader;
using AssetRipper.Core.Classes.Shader.SerializedShader.Enum;
using AssetRipper.Core.Extensions;
using AssetRipper.Core.Interfaces;
using AssetRipper.Core.Parser.Files;
using AssetRipper.Core.Project;
using AssetRipper.Core.Project.Exporters;
using AssetRipper.Library.Configuration;
using DirectXDisassembler;
using DirectXDisassembler.Blocks;
using ShaderLabConvert;
using ShaderTextRestorer.Exporters;
using ShaderTextRestorer.Exporters.DirectX;
using ShaderTextRestorer.Exporters.USCDirectX;
using ShaderTextRestorer.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UltraShaderConverter;

namespace AssetRipper.Library.Exporters.Shaders
{
	public sealed class USCShaderExporter : BinaryAssetExporter
	{
		ShaderExportMode ExportMode { get; set; }

		public USCShaderExporter(LibraryConfiguration options)
		{
			ExportMode = options.ShaderExportMode;
		}

		public override bool IsHandle(IUnityObjectBase asset)
		{
			return asset is Shader && ExportMode == ShaderExportMode.Decompile;
		}

		public static bool IsDX11ExportMode(ShaderExportMode mode) => mode == ShaderExportMode.Disassembly;

		public override bool Export(IExportContainer container, IUnityObjectBase asset, string path)
		{
			Shader shader = (Shader)asset;

			//Importing Hidden/Internal shaders causes the unity editor screen to turn black
			if (shader.ParsedForm.Name?.StartsWith("Hidden/Internal") ?? false)
				return false;

			using Stream fileStream = File.Create(path);
			ExportBinary(shader, container, fileStream, ShaderExporterInstantiator);
			return true;
		}

		private ShaderTextExporter ShaderExporterInstantiator(UnityVersion version, GPUPlatform graphicApi)
		{
			switch (graphicApi)
			{
				case GPUPlatform.d3d11_9x:
				case GPUPlatform.d3d11:
				case GPUPlatform.d3d9:
					return new USCShaderDXExporter(graphicApi);

				case GPUPlatform.vulkan:
					return new ShaderVulkanExporter();

				case GPUPlatform.openGL:
				case GPUPlatform.gles:
				case GPUPlatform.gles3:
				case GPUPlatform.glcore:
					return new ShaderGLESExporter();

				case GPUPlatform.metal:
					return new ShaderMetalExporter();

				case GPUPlatform.unknown:
					return new ShaderTextExporter();

				default:
					return new ShaderUnknownExporter(graphicApi);
			}
		}

		public void ExportBinary(Shader shader, IExportContainer container, Stream stream, Func<UnityVersion, GPUPlatform, ShaderTextExporter> exporterInstantiator)
		{
			if (Shader.IsSerialized(container.Version))
			{
				using ShaderWriter writer = new ShaderWriter(stream, shader, exporterInstantiator);
				writer.WriteQuotesAroundProgram = false; // this can be removed after ESSWC is finished
				//((SerializedShader)shader.ParsedForm).Export(writer);
				ExportSerializedShaderDecomp((SerializedShader)shader.ParsedForm, writer);
			}
			else if (Shader.HasBlob(container.Version))
			{
				using ShaderWriter writer = new ShaderWriter(stream, shader, exporterInstantiator);
				writer.WriteQuotesAroundProgram = false;
				string header = Encoding.UTF8.GetString(shader.Script);
				if (shader.Blobs.Length == 0)
				{
					writer.Write(header);
				}
				else
				{
					shader.Blobs[0].Export(writer, header);
				}
			}
			else
			{
				using BinaryWriter writer = new BinaryWriter(stream);
				writer.Write(shader.Script);
			}
		}

		/////////////////////////////////////////////////////
		// we need to lay this out in a specific way that
		// isn't compatible with the way SerializedExtensions
		// does it

		private static void ExportSerializedShaderDecomp(SerializedShader _this, ShaderWriter writer)
		{
			writer.Write("Shader \"{0}\" {{\n", _this.Name);

			_this.PropInfo.Export(writer);

			for (int i = 0; i < _this.SubShaders.Length; i++)
			{
				ExportSubShaderDecomp(_this.SubShaders[i], writer);
			}

			if (_this.FallbackName.Length != 0)
			{
				writer.WriteIndent(1);
				writer.Write("Fallback \"{0}\"\n", _this.FallbackName);
			}

			if (_this.CustomEditorName.Length != 0)
			{
				writer.WriteIndent(1);
				writer.Write("CustomEditor \"{0}\"\n", _this.CustomEditorName);
			}

			writer.Write('}');
		}

		private static void ExportSubShaderDecomp(SerializedSubShader _this, ShaderWriter writer)
		{
			writer.WriteIndent(1);
			writer.Write("SubShader {\n");
			if (_this.LOD != 0)
			{
				writer.WriteIndent(2);
				writer.Write("LOD {0}\n", _this.LOD);
			}
			_this.Tags.Export(writer, 2);
			for (int i = 0; i < _this.Passes.Length; i++)
			{
				ExportPassDecomp(_this.Passes[i], writer);
			}
			writer.WriteIndent(1);
			writer.Write("}\n");
		}

		private static void ExportPassDecomp(SerializedPass _this, ShaderWriter writer)
		{
			writer.WriteIndent(2);
			writer.Write("{0} ", _this.Type.ToString());

			if (_this.Type == SerializedPassType.UsePass)
			{
				writer.Write("\"{0}\"\n", _this.UseName);
			}
			else
			{
				writer.Write("{\n");

				if (_this.Type == SerializedPassType.GrabPass)
				{
					if (_this.TextureName.Length > 0)
					{
						writer.WriteIndent(3);
						writer.Write("\"{0}\"\n", _this.TextureName);
					}
				}
				else if (_this.Type == SerializedPassType.Pass)
				{
					_this.State.Export(writer);

					bool hasVertex = (_this.ProgramMask & ShaderType.Vertex.ToProgramMask()) != 0;
					bool hasFragment = (_this.ProgramMask & ShaderType.Fragment.ToProgramMask()) != 0;

					List<ShaderSubProgram> vertexSubPrograms = null;
					List<ShaderSubProgram> fragmentSubPrograms = null;

					if (hasVertex)
					{
						vertexSubPrograms = GetSubPrograms(writer.Shader, _this.ProgVertex, writer.Version, writer.Platform, ShaderType.Vertex, _this);
					}
					if (hasFragment)
					{
						fragmentSubPrograms = GetSubPrograms(writer.Shader, _this.ProgFragment, writer.Version, writer.Platform, ShaderType.Fragment, _this);
					}

					ShaderSubProgram vertexSubProgram = null;
					ShaderSubProgram fragmentSubProgram = null;
					USCShaderConverter vertexConverter = null;
					USCShaderConverter fragmentConverter = null;

					if (hasVertex)
					{
						vertexSubProgram = vertexSubPrograms[0];

						byte[] trimmedProgramData = TrimShaderBytes(vertexSubProgram, writer.Version, writer.Platform);

						vertexConverter = new USCShaderConverter();
						vertexConverter.LoadDirectXCompiledShader(new MemoryStream(trimmedProgramData));
					}
					if (hasFragment)
					{
						fragmentSubProgram = fragmentSubPrograms[0];

						byte[] trimmedProgramData = TrimShaderBytes(fragmentSubProgram, writer.Version, writer.Platform);

						fragmentConverter = new USCShaderConverter();
						fragmentConverter.LoadDirectXCompiledShader(new MemoryStream(trimmedProgramData));
					}

					writer.WriteIndent(3);
					writer.WriteLine("CGPROGRAM");

					if (hasVertex)
					{
						writer.WriteIndent(3);
						writer.WriteLine("#pragma vertex vert");
					}
					if (hasFragment)
					{
						writer.WriteIndent(3);
						writer.WriteLine("#pragma fragment frag");
					}
					if (hasVertex || hasFragment)
					{
						writer.WriteIndent(3);
						writer.WriteLine("");
					}

					writer.WriteIndent(3);
					writer.WriteLine("#include \"UnityCG.cginc\"");

					if (hasVertex)
					{
						// v2f struct (vert output/frag input)
						writer.WriteIndent(3);
						writer.WriteLine($"struct {USILConstants.VERT_TO_FRAG_STRUCT_NAME}");
						writer.WriteIndent(3);
						writer.WriteLine("{");

						DirectXCompiledShader dxShader = vertexConverter.DxShader;
						foreach (OSGN.Output output in dxShader.Osgn.outputs)
						{
							string format = DXShaderNamingUtils.GetOSGNFormatName(output);
							string type = output.name + output.index;
							string name = DXShaderNamingUtils.GetOSGNOutputName(output);

							writer.WriteIndent(4);
							writer.WriteLine($"{format} {name} : {type};");
						}

						writer.WriteIndent(3);
						writer.WriteLine("};");
					}
					if (hasFragment)
					{
						// fout struct (frag output)
						writer.WriteIndent(3);
						writer.WriteLine($"struct {USILConstants.FRAG_OUTPUT_STRUCT_NAME}");
						writer.WriteIndent(3);
						writer.WriteLine("{");

						DirectXCompiledShader dxShader = fragmentConverter.DxShader;
						foreach (OSGN.Output output in dxShader.Osgn.outputs)
						{
							string format = DXShaderNamingUtils.GetOSGNFormatName(output);
							string type = output.name + output.index;
							string name = DXShaderNamingUtils.GetOSGNOutputName(output);

							writer.WriteIndent(4);
							writer.WriteLine($"{format} {name} : {type};");
						}

						writer.WriteIndent(3);
						writer.WriteLine("};");
					}

					HashSet<string> declaredBufs = new HashSet<string>();

					if (hasVertex)
					{
						writer.WriteIndent(3);
						writer.WriteLine("// $Globals ConstantBuffers for Vertex Shader");

						ConstantBuffer globalsCb = vertexSubProgram.ConstantBuffers.FirstOrDefault(cb => cb.Name == "$Globals");

						if (globalsCb != null)
						{
							NumericShaderParameter[] allParams = globalsCb.AllNumericParams;
							foreach (NumericShaderParameter param in allParams)
							{
								string typeName = DXShaderNamingUtils.GetConstantBufferParamType(param);
								string name = param.Name;

								if (!declaredBufs.Contains(name))
								{
									writer.WriteIndent(3);
									writer.WriteLine($"{typeName} {name};");
									declaredBufs.Add(name);
								}
							}
						}
					}

					if (hasFragment)
					{
						writer.WriteIndent(3);
						writer.WriteLine("// $Globals ConstantBuffers for Fragment Shader");

						ConstantBuffer globalsCb = fragmentSubProgram.ConstantBuffers.FirstOrDefault(cb => cb.Name == "$Globals");

						if (globalsCb != null)
						{
							NumericShaderParameter[] allParams = globalsCb.AllNumericParams;
							foreach (NumericShaderParameter param in allParams)
							{
								string typeName = DXShaderNamingUtils.GetConstantBufferParamType(param);
								string name = param.Name;

								if (!declaredBufs.Contains(name))
								{
									writer.WriteIndent(3);
									writer.WriteLine($"{typeName} {name};");
									declaredBufs.Add(name);
								}
							}
						}
					}

					bool anyGlobalSlots = false;
					if (hasVertex)
					{
						writer.WriteIndent(3);
						writer.WriteLine("// Texture params for Vertex Shader");
						foreach (TextureParameter param in vertexSubProgram.TextureParameters)
						{
							string name = param.Name;
							if (!declaredBufs.Contains(name))
							{
								writer.WriteIndent(3);
								if (param.Dim == 2)
								{
									writer.WriteLine($"sampler2D {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 3)
								{
									writer.WriteLine($"sampler3D {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 4)
								{
									writer.WriteLine($"samplerCUBE {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 5)
								{
									writer.WriteLine($"UNITY_DECLARE_TEX2DARRAY({name});");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 6)
								{
									writer.WriteLine($"UNITY_DECLARE_TEXCUBEARRAY({name});");
									anyGlobalSlots = true;
								}
								else
								{
									writer.WriteLine($"sampler2D {name}; // Unsure of real type ({param.Dim})");
									anyGlobalSlots = true;
								}
								declaredBufs.Add(name);
							}
						}
					}

					if (hasFragment)
					{
						writer.WriteIndent(3);
						writer.WriteLine("// Texture params for Fragment Shader");
						foreach (TextureParameter param in fragmentSubProgram.TextureParameters)
						{
							string name = param.Name;
							if (!declaredBufs.Contains(name))
							{
								writer.WriteIndent(3);
								if (param.Dim == 2)
								{
									writer.WriteLine($"sampler2D {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 3)
								{
									writer.WriteLine($"sampler3D {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 4)
								{
									writer.WriteLine($"samplerCUBE {name};");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 5)
								{
									writer.WriteLine($"UNITY_DECLARE_TEX2DARRAY({name});");
									anyGlobalSlots = true;
								}
								else if (param.Dim == 6)
								{
									writer.WriteLine($"UNITY_DECLARE_TEXCUBEARRAY({name});");
									anyGlobalSlots = true;
								}
								else
								{
									writer.WriteLine($"sampler2D {name}; // Unsure of real type ({param.Dim})");
									anyGlobalSlots = true;
								}
								declaredBufs.Add(name);
							}
						}
					}

					if (anyGlobalSlots)
					{
						writer.WriteIndent(3);
						writer.WriteLine("");
					}

					if (hasVertex)
					{
						writer.WriteIndent(3);
						writer.WriteLine($"{USILConstants.VERT_TO_FRAG_STRUCT_NAME} vert(appdata_full {USILConstants.VERT_INPUT_NAME})");
						writer.WriteIndent(3);
						writer.WriteLine("{");

						vertexConverter.ConvertShaderToUShaderProgram();
						vertexConverter.ApplyMetadataToProgram(vertexSubProgram, writer.Version);
						string progamText = vertexConverter.CovnertUShaderProgramToHLSL(4);
						writer.Write(progamText);

						writer.WriteIndent(3);
						writer.WriteLine("}");
					}

					if (hasFragment)
					{
						writer.WriteIndent(3);
						writer.WriteLine($"{USILConstants.FRAG_OUTPUT_STRUCT_NAME} frag({USILConstants.VERT_TO_FRAG_STRUCT_NAME} {USILConstants.FRAG_INPUT_NAME})");
						writer.WriteIndent(3);
						writer.WriteLine("{");

						fragmentConverter.ConvertShaderToUShaderProgram();
						fragmentConverter.ApplyMetadataToProgram(fragmentSubProgram, writer.Version);
						string progamText = fragmentConverter.CovnertUShaderProgramToHLSL(4);
						writer.Write(progamText);

						writer.WriteIndent(3);
						writer.WriteLine("}");
					}

					writer.WriteIndent(3);
					writer.WriteLine("ENDCG");
				}
				else
				{
					throw new NotSupportedException($"Unsupported pass type {_this.Type}");
				}

				writer.WriteIndent(2);
				writer.Write("}\n");
			}
		}

		private static void ExportSerializedProgramDecomp(SerializedProgram _this, ShaderWriter writer, ShaderType type)
		{
			if (_this.SubPrograms.Length == 0)
			{
				return;
			}

			writer.WriteIndent(3);
			writer.Write("Program \"{0}\" {{\n", type.ToProgramTypeString());
			int tierCount = _this.GetTierCount();
			for (int i = 0; i < _this.SubPrograms.Length; i++)
			{
				_this.SubPrograms[i].Export(writer, type, tierCount > 1);
			}
			writer.WriteIndent(3);
			writer.Write("}\n");
		}

		private static List<ShaderSubProgram> GetSubPrograms(Shader shader, SerializedProgram program, UnityVersion version, Platform platform, ShaderType shaderType, SerializedPass pass)
		{
			List<ShaderSubProgram> matchingPrograms = new List<ShaderSubProgram>();
			for (int i = 0; i < program.SubPrograms.Length; i++)
			{
				SerializedSubProgram subProgram = program.SubPrograms[i];
				ShaderGpuProgramType programType = subProgram.GetProgramType(version);
				GPUPlatform graphicApi = programType.ToGPUPlatform(platform);

				if (graphicApi != GPUPlatform.d3d11)
					continue;

				bool matched = false;

				switch (programType)
				{
					case ShaderGpuProgramType.DX11VertexSM40:
					case ShaderGpuProgramType.DX11VertexSM50:
						if (shaderType == ShaderType.Vertex)
							matched = true;
						break;
					case ShaderGpuProgramType.DX11PixelSM40:
					case ShaderGpuProgramType.DX11PixelSM50:
						if (shaderType == ShaderType.Fragment)
							matched = true;
						break;
				}

				// skip instanced shaders
				if (subProgram.GlobalKeywordIndices != null)
				{
					for (int j = 0; j < subProgram.GlobalKeywordIndices.Length; j++)
					{
						if (pass.NameIndices["INSTANCING_ON"] == subProgram.GlobalKeywordIndices[j])
							matched = false;
					}
				}
				if (subProgram.LocalKeywordIndices != null)
				{
					for (int j = 0; j < subProgram.LocalKeywordIndices.Length; j++)
					{
						if (pass.NameIndices["INSTANCING_ON"] == subProgram.LocalKeywordIndices[j])
							matched = false;
					}
				}

				if (matched)
				{
					int platformIndex = shader.Platforms.IndexOf(graphicApi);
					matchingPrograms.Add(shader.Blobs[platformIndex].SubPrograms[subProgram.BlobIndex]);
				}
			}

			return matchingPrograms;
		}

		// this is necessary because sometimes vertex programs can have more
		// variations than frag programs and vice versa. we need to collect
		// all possible combos for both. if one has more combos than the other,
		// they will always be at the end (I think).
		private static List<string[]> GetAllVariantCombos(List<ShaderSubProgram> subPrograms, UnityVersion version, Platform platform, List<GPUPlatform> progPlats)
		{
			List<string[]> combos = new List<string[]>();
			HashSet<string> comboHashes = new HashSet<string>();
			foreach (ShaderSubProgram subProgram in subPrograms)
			{
				ShaderGpuProgramType programType = subProgram.GetProgramType(version);
				GPUPlatform graphicApi = programType.ToGPUPlatform(platform);

				if (!progPlats.Contains(graphicApi))
					continue;

				List<string> keywords = new List<string>();

				if (subProgram.GlobalKeywords != null)
					keywords.AddRange(subProgram.GlobalKeywords);
				if (subProgram.LocalKeywords != null)
					keywords.AddRange(subProgram.LocalKeywords);

				// don't have to worry about order I don't think
				// although it would probably be safer to sort
				string keywordsStr = string.Join(',', keywords);

				if (!comboHashes.Contains(keywordsStr))
				{
					comboHashes.Add(keywordsStr);
					combos.Add(keywords.ToArray());
				}
			}

			return combos;
		}

		// DUPLICATE CODE!!!!
		private static byte[] TrimShaderBytes(ShaderSubProgram subProgram, UnityVersion version, Platform platform)
		{
			ShaderGpuProgramType programType = subProgram.GetProgramType(version);
			GPUPlatform graphicApi = programType.ToGPUPlatform(platform);

			int dataOffset = 0;
			if (DXDataHeader.HasHeader(graphicApi))
			{
				dataOffset = DXDataHeader.GetDataOffset(version, graphicApi);
			}

			return GetRelevantData(subProgram.ProgramData, dataOffset);
		}

		private static byte[] GetRelevantData(byte[] bytes, int offset)
		{
			if (bytes == null)
				throw new ArgumentNullException(nameof(bytes));
			if (offset < 0 || offset > bytes.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));
			int size = bytes.Length - offset;
			byte[] result = new byte[size];
			for (int i = 0; i < size; i++)
			{
				result[i] = bytes[i + offset];
			}
			return result;
		}
	}
}
