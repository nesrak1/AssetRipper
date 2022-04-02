using AssetRipper.Core.Classes.Shader;
using AssetRipper.Core.Classes.Shader.Parameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
    public class USILSamplerMetadder : IUSILOptimizer
    {
        private UShaderProgram _shader;
        private ShaderSubProgram _shaderData;

        public bool Run(UShaderProgram shader, ShaderSubProgram shaderData)
        {
            _shader = shader;
            _shaderData = shaderData;

            List<USILInstruction> instructions = shader.instructions;
            foreach (USILInstruction instruction in instructions)
            {
                foreach (USILOperand operand in instruction.srcOperands)
                {
                    if (operand.operandType == USILOperandType.SamplerRegister)
                    {
                        TextureParameter sampSlot = _shaderData.TextureParameters.FirstOrDefault(
                            p => p.SamplerIndex == operand.registerIndex
                        );

                        int dimension = sampSlot.Dim;
                        switch (dimension)
                        {
                            case 2:
                                operand.operandType = USILOperandType.Sampler2D;
                                break;
                            case 3:
                                operand.operandType = USILOperandType.Sampler3D;
                                break;
                            case 4:
                                operand.operandType = USILOperandType.SamplerCube;
                                break;
                            case 5:
                                operand.operandType = USILOperandType.Sampler2DArray;
                                break;
                            case 6:
                                operand.operandType = USILOperandType.SamplerCubeArray;
                                break;
                        }

                        if (sampSlot != null)
                        {
                            operand.metadataName = sampSlot.Name;
                            operand.metadataNameAssigned = true;
                        }
                    }
                }
            }
            return true; // any changes made?
        }
    }
}
