﻿using AssetRipper.Core.Classes.Shader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
	public class USILSamplerTypeFixer : IUSILOptimizer
	{
		private UShaderProgram _shader;
		private ShaderSubProgram _shaderData;

		// There's most likely a better way to handle this, but I don't care right now.
		public static readonly HashSet<string> BUILTIN_SAMPLER_TEXTURE_NAMES = new HashSet<string>()
		{
			"unity_Lightmap",
			"unity_ShadowMask",
			"unity_DynamicLightmap",
			"unity_SpecCube0",
			"unity_ProbeVolumeSH",
			"_ShadowMapTexture"
		};

		// TODO: move these two into a different file (where?)
		// TODO: there are more, but they aren't included by default
		public static readonly HashSet<string> BUILTIN_TEXTURE_NAMES = new HashSet<string>()
		{
			"unity_Lightmap",
			"unity_LightmapInd",
			"unity_ShadowMask",
			"unity_DynamicLightmap",
			"unity_DynamicDirectionality",
			"unity_DynamicNormal",
			"unity_SpecCube0",
			"unity_SpecCube1",
			"unity_ProbeVolumeSH",
			"_ShadowMapTexture"
		};

		public bool Run(UShaderProgram shader, ShaderSubProgram shaderData)
		{
			_shader = shader;
			_shaderData = shaderData;

			bool changes = false;

			List<USILInstruction> instructions = shader.instructions;
			foreach (USILInstruction instruction in instructions)
			{
				if (instruction.IsSampleType())
				{
					USILOperand sampleOperand = instruction.srcOperands[2];

					// USILSamplerMetadder couldn't find sampler metadata, skip
					if (sampleOperand.operandType == USILOperandType.SamplerRegister)
						break;

					// Shouldn't happen, but just in case
					if (!sampleOperand.metadataNameAssigned)
						break;

					if (BUILTIN_SAMPLER_TEXTURE_NAMES.Contains(sampleOperand.metadataName))
					{
						int samplerTypeIdx = GetSamplerTypeIdx(instruction.instructionType);
						instruction.srcOperands[samplerTypeIdx] = new USILOperand(1);
						changes = true;
					}
				}
			}
			return changes; // any changes made?
		}

		private int GetSamplerTypeIdx(USILInstructionType type)
		{
			return type switch
			{
				USILInstructionType.Sample => 3,
				USILInstructionType.SampleLODBias => 4,
				USILInstructionType.SampleComparison => 4,
				USILInstructionType.SampleComparisonLODZero => 4,
				USILInstructionType.SampleLOD => 4,
				_ => 0
			};
		}
	}
}
