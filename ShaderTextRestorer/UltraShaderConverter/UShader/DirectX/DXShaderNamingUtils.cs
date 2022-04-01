using AssetRipper.Core.Classes.Shader.Parameters;
using DirectXDisassembler;
using DirectXDisassembler.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
    public static class DXShaderNamingUtils
    {
		// these two are useless now
		public static string GetConstantBufferParamType(VectorParameter param) => GetConstantBufferParamType(param.Dim, 1, false);
		public static string GetConstantBufferParamType(MatrixParameter param) => GetConstantBufferParamType(param.RowCount, param.ColumnCount, true);

		public static string GetConstantBufferParamType(NumericShaderParameter param) => GetConstantBufferParamType(param.RowCount, param.ColumnCount, true);

        public static string GetConstantBufferParamType(int rowCount, int columnCount, bool isMatrix)
        {
            string paramType = $"unknownType";

            if (columnCount == 1)
            {
                if (rowCount == 1)
                    paramType = "float";
                if (rowCount == 2)
                    paramType = "float2";
                if (rowCount == 3)
                    paramType = "float3";
                if (rowCount == 4)
                    paramType = "float4";
            }
            else if (columnCount == 4)
            {
                if (rowCount == 4 && isMatrix)
                    paramType = "float4x4";
            }

            return paramType;
        }

        public static string GetISGNInputName(ISGN.Input input)
        {
            string type;
            if (input.index > 0)
                type = input.name + input.index;
            else
                type = input.name;

            string name = input.name switch
            {
                "SV_POSITION" => "position",
                "POSITION" => "vertex",
                _ => type.ToLower(),
            };
            return name;
        }

        public static string GetOSGNOutputName(OSGN.Output output)
        {
            string type;
            if (output.index > 0)
                type = output.name + output.index;
            else
                type = output.name;

			if (HasSpecialInputOutputName(output.name))
			{
				return GetSpecialInputOutputName(output.name);
			}

			string name = output.name switch
            {
                "SV_POSITION" => "position",
                "POSITION" => "vertex",
                _ => type.ToLower(),
            };

            return name;
		}

		public static bool HasSpecialInputOutputName(string typeName) => GetSpecialInputOutputName(typeName) != string.Empty;
		public static string GetSpecialInputOutputName(string typeName)
		{
			switch (typeName)
			{
				case "SV_Depth":
				{
					return "oDepth";
				}
				case "SV_Coverage":
				{
					return "oMask";
				}
				case "SV_DepthGreaterEqual":
				{
					return "oDepthGE";
				}
				case "SV_DepthLessEqual":
				{
					return "oDepthLE";
				}
				case "SV_StencilRef":
				{
					return "oStencilRef"; // not in 3dmigoto
				}
			}

			return string.Empty;
		}

		public static bool HasSpecialInputOutputName(USILOperandType operandType) => GetSpecialInputOutputName(operandType) != string.Empty;
		public static string GetSpecialInputOutputName(USILOperandType operandType)
		{
			switch (operandType)
			{
				case USILOperandType.InputCoverageMask:
				{
					return "vCoverage";
				}
				case USILOperandType.InputThreadGroupID:
				{
					return "vThreadGroupID";
				}
				case USILOperandType.InputThreadID:
				{
					return "vThreadID";
				}
				case USILOperandType.InputThreadIDInGroup:
				{
					return "vThreadIDInGroup";
				}
				case USILOperandType.InputThreadIDInGroupFlattened:
				{
					return "vThreadIDInGroupFlattened";
				}
				case USILOperandType.InputPrimitiveID:
				{
					return "vPrim";
				}
				case USILOperandType.InputForkInstanceID:
				{
					return "vForkInstanceID";
				}
				case USILOperandType.InputGSInstanceID:
				{
					return "vGSInstanceID";
				}
				case USILOperandType.InputDomainPoint:
				{
					return "vDomain";
				}
				case USILOperandType.OutputControlPointID:
				{
					return "outputControlPointID"; // not in 3dmigoto
				}
				case USILOperandType.OutputDepth:
				{
					return "oDepth";
				}
				case USILOperandType.OutputCoverageMask:
				{
					return "oMask";
				}
				case USILOperandType.OutputDepthGreaterEqual:
				{
					return "oDepthGE";
				}
				case USILOperandType.OutputDepthLessEqual:
				{
					return "oDepthLE";
				}
				case USILOperandType.StencilRef:
				{
					return "oStencilRef"; // not in 3dmigoto
				}
			}

			return string.Empty;
		}

		public static string GetOSGNFormatName(OSGN.Output output)
		{
			return ((FormatType)output.format).ToString() + GetMaskSize(output.mask);
		}

		public static int GetMaskSize(byte mask)
		{
			int p = 0;
			for (int i = 0; i < 4; i++)
			{
				if (((mask >> i) & 1) == 1)
				{
					p++;
				}
			}
			return p;
		}
	}
}
