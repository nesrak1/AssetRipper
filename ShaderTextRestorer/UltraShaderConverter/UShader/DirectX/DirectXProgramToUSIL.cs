using DirectXDisassembler;
using DirectXDisassembler.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShaderLabConvert
{
    public class DirectXProgramToUSIL
    {
        private DirectXCompiledShader _dxShader;

        public UShaderProgram shader;

        private List<USILLocal> Locals => shader.locals;
        private List<USILInstruction> Instructions => shader.instructions;
        private List<USILInputOutput> Inputs => shader.inputs;
        private List<USILInputOutput> Outputs => shader.outputs;

        private delegate void InstHandler(SHDRInstruction inst);
        private Dictionary<Opcode, InstHandler> _instructionHandlers;

        public DirectXProgramToUSIL(DirectXCompiledShader dxShader)
        {
            _dxShader = dxShader;

            shader = new UShaderProgram();

            _instructionHandlers = new()
            {
                { Opcode.mov, new InstHandler(HandleMov) },
                { Opcode.movc, new InstHandler(HandleMovc) },
                { Opcode.add, new InstHandler(HandleAdd) },
                { Opcode.iadd, new InstHandler(HandleAdd) },
                { Opcode.mul, new InstHandler(HandleMul) },
                { Opcode.div, new InstHandler(HandleDiv) },
                { Opcode.mad, new InstHandler(HandleMad) },
                { Opcode.and, new InstHandler(HandleAnd) },
                { Opcode.or, new InstHandler(HandleOr) },
                { Opcode.not, new InstHandler(HandleNot) },
                { Opcode.ftoi, new InstHandler(HandleFtoi) },
                { Opcode.ftou, new InstHandler(HandleFtoi) },
                { Opcode.min, new InstHandler(HandleMin) },
                { Opcode.max, new InstHandler(HandleMax) },
                { Opcode.sqrt, new InstHandler(HandleSqrt) },
                { Opcode.rsq, new InstHandler(HandleRsq) },
                { Opcode.log, new InstHandler(HandleLog) },
                { Opcode.exp, new InstHandler(HandleExp) },
                { Opcode.rcp, new InstHandler(HandleRcp) },
                { Opcode.frc, new InstHandler(HandleFrc) },
                { Opcode.round_ni, new InstHandler(HandleRoundNi) },
                { Opcode.round_pi, new InstHandler(HandleRoundPi) },
                { Opcode.ishl, new InstHandler(HandleIShl) },
                { Opcode.ishr, new InstHandler(HandleIShr) },
                { Opcode.dp2, new InstHandler(HandleDp2) },
                { Opcode.dp3, new InstHandler(HandleDp3) },
                { Opcode.dp4, new InstHandler(HandleDp4) },
                { Opcode.sample, new InstHandler(HandleSample) },
                { Opcode.sample_c, new InstHandler(HandleSampleC) },
                { Opcode.sample_c_lz, new InstHandler(HandleSampleC) },
                { Opcode.sample_l, new InstHandler(HandleSampleL) },
                { Opcode.sample_b, new InstHandler(HandleSampleL) },
                { Opcode.discard, new InstHandler(HandleDiscard) },
                { Opcode.@if, new InstHandler(HandleIf) },
                { Opcode.@else, new InstHandler(HandleElse) },
                { Opcode.endif, new InstHandler(HandleEndIf) },
                { Opcode.eq, new InstHandler(HandleEq) },
                { Opcode.ieq, new InstHandler(HandleEq) }, // hoping there are no floats in this
                { Opcode.ne, new InstHandler(HandleNeq) },
                { Opcode.ine, new InstHandler(HandleNeq) },
                { Opcode.lt, new InstHandler(HandleLt) },
                { Opcode.ilt, new InstHandler(HandleLt) },
                { Opcode.ge, new InstHandler(HandleGe) },
                { Opcode.ret, new InstHandler(HandleRet) },
                ////dec
                { Opcode.dcl_temps, new InstHandler(HandleTemps) },
                { Opcode.customdata, new InstHandler(HandleCustomData) }
            };
        }

        public void Convert()
        {
            ConvertInstructions();
            ConvertInputs();
            ConvertOutputs();
        }

        private void ConvertInstructions()
        {
            SHDR shdr = _dxShader.Shdr;

			AddOutputLocal();

			List<SHDRInstruction> instructions = shdr.shaderInstructions;
            for (int i = 0; i < instructions.Count; i++)
            {
                SHDRInstruction inst = instructions[i];
                if (_instructionHandlers.ContainsKey(inst.opcode))
                {
                    _instructionHandlers[inst.opcode](inst);
                }
                else if (!SHDRInstruction.IsDeclaration(inst.opcode))
                {
                    Instructions.Add(new USILInstruction
                    {
                        instructionType = USILInstructionType.Comment,
                        destOperand = new USILOperand
                        {
                            comment = $"unsupported_{inst.opcode}",
                            operandType = USILOperandType.Comment
                        },
                        srcOperands = new List<USILOperand>()
                    });
                }
            }
        }

        private void ConvertInputs()
        {
            ISGN isgn = _dxShader.Isgn;

            ISGN.Input[] dxInputs = isgn.inputs;
            for (int i = 0; i < dxInputs.Length; i++)
            {
                ISGN.Input dxInput = dxInputs[i];

                string inputType = dxInput.name;
                string inputName = DXShaderNamingUtils.GetISGNInputName(dxInput);
                int register = dxInput.register;
                int mask = dxInput.mask;

                USILInputOutput usilInput = new USILInputOutput()
                {
                    type = inputType,
                    name = inputName,
                    register = register,
                    mask = mask,
                    isOutput = false
                };

                Inputs.Add(usilInput);
            }
        }

        private void ConvertOutputs()
        {
            OSGN osgn = _dxShader.Osgn;

            OSGN.Output[] dxOutputs = osgn.outputs;
            for (int i = 0; i < dxOutputs.Length; i++)
            {
                OSGN.Output dxOutput = dxOutputs[i];

                string outputType = dxOutput.name;
                string outputName = DXShaderNamingUtils.GetOSGNOutputName(dxOutput);
                int register = dxOutput.register;
                int mask = dxOutput.mask;

                USILInputOutput usilOutput = new USILInputOutput()
                {
                    type = outputType,
                    name = outputName,
                    register = register,
                    mask = mask,
                    isOutput = true
                };

                Outputs.Add(usilOutput);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            int depth = 4;
            foreach (var instruction in Instructions)
            {
                if (instruction.instructionType == USILInstructionType.EndIf ||
                    instruction.instructionType == USILInstructionType.Else)
                {
                    depth--;
                }

                sb.Append(new string(' ', depth * 4));
                sb.AppendLine(instruction.ToString());
                
                if (instruction.instructionType == USILInstructionType.IfFalse ||
                    instruction.instructionType == USILInstructionType.IfTrue ||
                    instruction.instructionType == USILInstructionType.Else)
                {
                    depth++;
                }
            }

            return sb.ToString();
        }

        ///////////////////////
        // internal stuff

        private void FillUSILOperand(SHDRInstructionOperand dxOperand, USILOperand usilOperand, int[] mask, bool immIsInt)
        {
            usilOperand.mask = mask;

            usilOperand.negative = ((dxOperand.extendedData & 0x40) >> 6) == 1;
            usilOperand.absoluteValue = ((dxOperand.extendedData & 0x80) >> 7) == 1;

            switch (dxOperand.operand)
            {
                case Operand.ConstantBuffer:
                {
                    // figure out cb names in a later pass
                    int cbSlotIdx = dxOperand.arraySizes[0];
                    int cbArrIdx = dxOperand.arraySizes[1];

                    usilOperand.operandType = USILOperandType.ConstantBuffer;
                    usilOperand.registerIndex = cbSlotIdx;
                    usilOperand.arrayIndex = cbArrIdx;
                    break;
                }
                case Operand.Input:
                {
                    int inRegIdx = dxOperand.arraySizes[0];

                    usilOperand.operandType = USILOperandType.InputRegister;
                    usilOperand.registerIndex = inRegIdx;
                    break;
                }
                case Operand.Output:
                {
                    int outRegIdx = dxOperand.arraySizes[0];

                    usilOperand.operandType = USILOperandType.OutputRegister;
                    usilOperand.registerIndex = outRegIdx;
                    break;
                }
                case Operand.Temp:
                {
                    int tmpRegIdx;
                    if (dxOperand.arraySizes.Length > 0)
                        tmpRegIdx = dxOperand.arraySizes[0];
                    else
                        tmpRegIdx = 0;

                    usilOperand.operandType = USILOperandType.TempRegister;
                    usilOperand.registerIndex = tmpRegIdx;
                    break;
                }
                case Operand.Resource:
                {
                    int rscRegIdx = dxOperand.arraySizes[0];

                    usilOperand.operandType = USILOperandType.ResourceRegister;
                    usilOperand.registerIndex = rscRegIdx;
                    break;
                }
                case Operand.Sampler:
                {
                    int smpRegIdx = dxOperand.arraySizes[0];

                    usilOperand.operandType = USILOperandType.SamplerRegister;
                    usilOperand.registerIndex = smpRegIdx;
                    break;
                }
                case Operand.Immediate32:
                {
                    usilOperand.immIsInt = immIsInt;
                    usilOperand.operandType = immIsInt ? USILOperandType.ImmediateInt : USILOperandType.ImmediateFloat;

                    if (dxOperand.immValues.Length == 1)
                    {
                        if (immIsInt)
                        {
                            usilOperand.immValueInt = new int[1]
                            {
                                ConvertFloatToInt((float)dxOperand.immValues[0])
                            };
                        }
                        else
                        {
                            usilOperand.immValueFloat = new float[1]
                            {
                                (float)dxOperand.immValues[0]
                            };
                        }
                    }
                    else if (dxOperand.immValues.Length == 4)
                    {
                        if (immIsInt)
                        {
                            usilOperand.immValueInt = new int[mask.Length];
                            for (int i = 0; i < mask.Length; i++)
                            {
                                usilOperand.immValueInt[i] = ConvertFloatToInt((float)dxOperand.immValues[mask[i]]);
                            }
                        }
                        else
                        {
                            usilOperand.immValueFloat = new float[mask.Length];
                            for (int i = 0; i < mask.Length; i++)
                            {
                                usilOperand.immValueFloat[i] = (float)dxOperand.immValues[mask[i]];
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"{dxOperand.immValues.Length} imm values not possible");
                    }
                    break;
                }
                case Operand.ImmediateConstantBuffer:
                {
                    int icbSlotIdx = dxOperand.arraySizes[0];
                    int icbOff = dxOperand.registerNumber;
                    SHDRInstructionOperand icbOperand = dxOperand.subOperands[0];

                    usilOperand.operandType = USILOperandType.ImmediateConstantBuffer;
                    usilOperand.registerIndex = icbSlotIdx;
                    usilOperand.children = new USILOperand[1];
                    usilOperand.children[0] = new USILOperand();
                    FillUSILOperand(icbOperand, usilOperand.children[0], icbOperand.swizzle, false);
                    usilOperand.arrayIndex = icbOff;
                    break;
                }
                default:
                {
                    throw new NotSupportedException($"operand type {dxOperand.operand} not supported");
                }
            }
        }

        // there is a better way to check this, but this works too
        private int[] MapMask(int[] destMask, int[] srcMask)
		{
			if (srcMask == null || srcMask.Length == 0)
			{
				// immediates won't have a mask, so match them to destination values
				return destMask;
			}
			else if (destMask.Length == 0 || destMask.Length == 4)
            {
                // 0 shouldn't happen, but with mask 4 (xyzw) don't worry about mapping mask
                return srcMask;
            }
            else if (destMask.Length == 1)
            {
                // with mask size 1, all inputs are also only size 1
                return new int[] { srcMask[0] };
            }
            else //dest.mask.Length == (2 || 3)
            {
                // when mask size is 2 or 3, inputs will be size 4. map the masks correctly
                int[] mask = new int[destMask.Length];
                for (int i = 0; i < mask.Length; i++)
                {
                    mask[i] = srcMask[destMask[i]];
                }
                return mask;
            }
        }

        private void HandleMov(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0];
            SHDRInstructionOperand src0 = inst.operands[1];

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Move;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleMovc(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation. If src0, then dest = src1 else dest = src2
            SHDRInstructionOperand src0 = inst.operands[1]; // The components on which to test the condition.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components to move. 
            SHDRInstructionOperand src2 = inst.operands[3]; // The components to move. 

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();
            USILOperand usilSrc2 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);
            FillUSILOperand(src2, usilSrc2, MapMask(dest.swizzle, src2.swizzle), false);

            usilInst.instructionType = USILInstructionType.MoveConditional;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1,
                usilSrc2
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleAdd(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The (vector/number) to add to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The (vector/number) to add to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool immIsInt = inst.opcode == Opcode.iadd;

            FillUSILOperand(dest, usilDest, dest.swizzle, immIsInt);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), immIsInt);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), immIsInt);

            usilInst.instructionType = USILInstructionType.Add;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleMul(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = src0 * src1
            SHDRInstructionOperand src0 = inst.operands[1]; // The multiplicand.
            SHDRInstructionOperand src1 = inst.operands[2]; // The multiplier.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.Multiply;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleDiv(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The dividend.
            SHDRInstructionOperand src1 = inst.operands[2]; // The divisor.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.Divide;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleMad(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = src0 * src1 + src2
            SHDRInstructionOperand src0 = inst.operands[1]; // The multiplicand.
            SHDRInstructionOperand src1 = inst.operands[2]; // The multiplier.
            SHDRInstructionOperand src2 = inst.operands[3]; // The addend.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();
            USILOperand usilSrc2 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);
            FillUSILOperand(src2, usilSrc2, MapMask(dest.swizzle, src2.swizzle), false);

            usilInst.instructionType = USILInstructionType.MultiplyAdd;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1,
                usilSrc2
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleAnd(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The 32-bit value to AND with src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The 32-bit value to AND with src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.And;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleOr(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components to OR with src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components to OR with src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.Or;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleNot(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The original components.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Not;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleFtoi(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation. dest = round_z(src0)
            SHDRInstructionOperand src0 = inst.operands[1]; // The component to convert.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Floor;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleMin(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = src0 < src1 ? src0 : src1
            SHDRInstructionOperand src0 = inst.operands[1]; // The components to compare to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components to compare to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.Minimum;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleMax(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = src0 >= src1 ? src0 : src1
            SHDRInstructionOperand src0 = inst.operands[1]; // The components to compare to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components to compare to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), false);

            usilInst.instructionType = USILInstructionType.Maximum;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleSqrt(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = sqrt(src0)
            SHDRInstructionOperand src0 = inst.operands[1]; // The components for which to take the square root.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.SquareRoot;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleRsq(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // Contains the results of the operation. dest = 1.0f / sqrt(src0)
            SHDRInstructionOperand src0 = inst.operands[1]; // The components for the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.SquareRootReciprocal;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleLog(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation. dest = log2(src0)
            SHDRInstructionOperand src0 = inst.operands[1]; // The value for the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Logarithm2;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleExp(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation. dest = 2src0
            SHDRInstructionOperand src0 = inst.operands[1]; // The exponent.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Exponential;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleRcp(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the results dest = 1.0f / src0.
            SHDRInstructionOperand src0 = inst.operands[1]; // The number to take the reciprocal of.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Reciprocal;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleFrc(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation. dest = src0 - round_ni(src0)
            SHDRInstructionOperand src0 = inst.operands[1]; // The component in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Fractional;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleRoundNi(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the results of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Floor;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleRoundPi(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the results of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), false);

            usilInst.instructionType = USILInstructionType.Ceiling;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleIShl(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // Contains the values to be shifted.
            SHDRInstructionOperand src1 = inst.operands[2]; // Contains the shift amount.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, true);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), true);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), true);

            usilInst.instructionType = USILInstructionType.ShiftLeft;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleIShr(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // Contains the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // Contains the value to be shifted.
            SHDRInstructionOperand src1 = inst.operands[2]; // Contains the shift amount.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, dest.swizzle, true);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), true);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), true);

            usilInst.instructionType = USILInstructionType.ShiftRight;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleDp2(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation. dest = src0.r * src1.r + src0.g * src1.g
            SHDRInstructionOperand src0 = inst.operands[1]; // The components in the operation.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            int[] mask = new int[] { 0, 1 };
            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, mask, false);
            FillUSILOperand(src1, usilSrc1, mask, false);

            usilInst.instructionType = USILInstructionType.DotProduct2;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleDp3(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components in the operation.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            int[] mask = new int[] { 0, 1, 2 };
            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, mask, false);
            FillUSILOperand(src1, usilSrc1, mask, false);

            usilInst.instructionType = USILInstructionType.DotProduct3;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleDp4(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components in the operation.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components in the operation.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            int[] mask = new int[] { 0, 1, 2, 3 };
            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(src0, usilSrc0, mask, false);
            FillUSILOperand(src1, usilSrc1, mask, false);

            usilInst.instructionType = USILInstructionType.DotProduct4;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };
            usilInst.saturate = inst.saturated;

            Instructions.Add(usilInst);
        }

        private void HandleSample(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand srcAddress = inst.operands[1]; // A set of texture coordinates. For more information, see the Remarks section.
            SHDRInstructionOperand srcResource = inst.operands[2]; // A texture register. For more information, see the Remarks section.
            SHDRInstructionOperand srcSampler = inst.operands[3]; // A sampler register. For more information, see the Remarks section.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrcAddress = new USILOperand();
            USILOperand usilSrcResource = new USILOperand();
            USILOperand usilSrcSampler = new USILOperand();

            int[] mask = new int[] { 0, 1, 2, 3 };
            int[] uvMask = new int[] { 0, 1 }; // todo: read dx dcl data

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(srcAddress, usilSrcAddress, MapMask(uvMask, srcAddress.swizzle), false);
            FillUSILOperand(srcResource, usilSrcResource, MapMask(dest.swizzle, srcResource.swizzle), false);
            FillUSILOperand(srcSampler, usilSrcSampler, mask, false);

            usilInst.instructionType = USILInstructionType.Sample;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrcAddress,
                usilSrcResource,
                usilSrcSampler
            };

            Instructions.Add(usilInst);
        }

        private void HandleSampleC(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the results of the operation.
            SHDRInstructionOperand srcAddress = inst.operands[1]; // A set of texture coordinates. For more information see the sample instruction.
            SHDRInstructionOperand srcResource = inst.operands[2]; // A texture register. For more information see the sample instruction.
            SHDRInstructionOperand srcSampler = inst.operands[3]; // A sampler register. For more information see the sample instruction.
            SHDRInstructionOperand srcReferenceValue = inst.operands[4]; // A register with a single component selected, which is used in the comparison.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrcAddress = new USILOperand();
            USILOperand usilSrcResource = new USILOperand();
            USILOperand usilSrcSampler = new USILOperand();
            USILOperand usilSrcReferenceValue = new USILOperand();

            int[] mask = new int[] { 0, 1, 2, 3 };
            int[] uvMask = new int[] { 0, 1 }; // todo: read dx dcl data

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(srcAddress, usilSrcAddress, MapMask(uvMask, srcAddress.swizzle), false);
            FillUSILOperand(srcResource, usilSrcResource, MapMask(dest.swizzle, srcResource.swizzle), false);
            FillUSILOperand(srcSampler, usilSrcSampler, mask, false);
            FillUSILOperand(srcReferenceValue, usilSrcReferenceValue, mask, false);

            if (inst.opcode == Opcode.sample_c)
                usilInst.instructionType = USILInstructionType.SampleComparison;
            else //if (inst.opcode == Opcode.sample_c_lz)
                usilInst.instructionType = USILInstructionType.SampleComparisonLODZero;

            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrcAddress,
                usilSrcResource,
                usilSrcSampler,
                usilSrcReferenceValue
            };

            Instructions.Add(usilInst);
        }

        private void HandleSampleL(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand srcAddress = inst.operands[1]; // A set of texture coordinates. For more information, see the Remarks section.
            SHDRInstructionOperand srcResource = inst.operands[2]; // A texture register. For more information, see the Remarks section.
            SHDRInstructionOperand srcSampler = inst.operands[3]; // A sampler register. For more information, see the Remarks section.
            SHDRInstructionOperand srcLOD = inst.operands[4]; // A sampler register. For more information, see the Remarks section.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrcAddress = new USILOperand();
            USILOperand usilSrcResource = new USILOperand();
            USILOperand usilSrcSampler = new USILOperand();
            USILOperand usilSrcLOD = new USILOperand();

            int[] mask = new int[] { 0, 1, 2, 3 };
            int[] uvMask = new int[] { 0, 1 }; // todo: read dx dcl data

            FillUSILOperand(dest, usilDest, dest.swizzle, false);
            FillUSILOperand(srcAddress, usilSrcAddress, MapMask(uvMask, srcAddress.swizzle), false);
            FillUSILOperand(srcResource, usilSrcResource, MapMask(dest.swizzle, srcResource.swizzle), false);
            FillUSILOperand(srcSampler, usilSrcSampler, mask, false);
            FillUSILOperand(srcLOD, usilSrcLOD, mask, false);

            if (inst.opcode == Opcode.sample_b)
                usilInst.instructionType = USILInstructionType.SampleLODBias;
            else //if (inst.opcode == Opcode.sample_l)
                usilInst.instructionType = USILInstructionType.SampleLOD;

            usilInst.instructionType = USILInstructionType.Sample;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrcAddress,
                usilSrcResource,
                usilSrcSampler,
                usilSrcLOD
            };

            Instructions.Add(usilInst);
        }

        private void HandleDiscard(SHDRInstruction inst)
        {
            SHDRInstructionOperand src0 = inst.operands[0]; // The value that determines whether to discard the current pixel being processed.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(src0, usilSrc0, src0.swizzle, false);

            usilInst.instructionType = USILInstructionType.Discard;
            usilInst.destOperand = null;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            // if works the same way, so we can pretend the discard is an if
            HandleIf(inst);
            Instructions.Add(usilInst);
            HandleEndIf(inst);
        }

        private void HandleIf(SHDRInstruction inst)
        {
            SHDRInstructionOperand src0 = inst.operands[0]; // The value that determines whether to discard the current pixel being processed.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(src0, usilSrc0, src0.swizzle, false);

            int testType = (inst.instData & 0x40000) >> 18;
            if (testType == 0)
                usilInst.instructionType = USILInstructionType.IfFalse;
            else if (testType == 1)
                usilInst.instructionType = USILInstructionType.IfTrue;

            usilInst.destOperand = null;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0
            };

            Instructions.Add(usilInst);
        }

        private void HandleElse(SHDRInstruction inst)
        {
            USILInstruction usilInst = new USILInstruction();

            usilInst.instructionType = USILInstructionType.Else;
            usilInst.destOperand = null;
            usilInst.srcOperands = new List<USILOperand>
            {
            };

            Instructions.Add(usilInst);
        }

        private void HandleEndIf(SHDRInstruction inst)
        {
            USILInstruction usilInst = new USILInstruction();

            usilInst.instructionType = USILInstructionType.EndIf;
            usilInst.destOperand = null;
            usilInst.srcOperands = new List<USILOperand>
            {
            };

            Instructions.Add(usilInst);
        }

        private void HandleEq(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The component to comapre to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The component to comapre to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = inst.opcode == Opcode.ieq;

            FillUSILOperand(dest, usilDest, dest.swizzle, isInt);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), isInt);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), isInt);

            usilInst.instructionType = USILInstructionType.Equal;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleNeq(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The components to compare to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The components to compare to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = inst.opcode == Opcode.ine;

            FillUSILOperand(dest, usilDest, dest.swizzle, isInt);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), isInt);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), isInt);

            usilInst.instructionType = USILInstructionType.NotEqual;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleLt(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The value to compare to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The value to compare to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = inst.opcode == Opcode.ilt;

            FillUSILOperand(dest, usilDest, dest.swizzle, isInt);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), isInt);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), isInt);

            usilInst.instructionType = USILInstructionType.LessThan;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleGe(SHDRInstruction inst)
        {
            SHDRInstructionOperand dest = inst.operands[0]; // The address of the result of the operation.
            SHDRInstructionOperand src0 = inst.operands[1]; // The value to compare to src1.
            SHDRInstructionOperand src1 = inst.operands[2]; // The value to compare to src0.

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = inst.opcode == Opcode.ige;

            FillUSILOperand(dest, usilDest, dest.swizzle, isInt);
            FillUSILOperand(src0, usilSrc0, MapMask(dest.swizzle, src0.swizzle), isInt);
            FillUSILOperand(src1, usilSrc1, MapMask(dest.swizzle, src1.swizzle), isInt);

            usilInst.instructionType = USILInstructionType.GreaterThanOrEqual;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand>
            {
                usilSrc0,
                usilSrc1
            };

            Instructions.Add(usilInst);
        }

        private void HandleRet(SHDRInstruction inst)
		{
			USILInstruction usilInst = new USILInstruction();
			usilInst.instructionType = USILInstructionType.Return;
			usilInst.srcOperands = new List<USILOperand>();
			Instructions.Add(usilInst);
		}

        private void HandleTemps(SHDRInstruction inst)
        {
            int tempCount = inst.declData.numTemps;
            for (int i = 0; i < tempCount; i++)
            {
                string tmpString = USILOperand.GetTypeShortForm(USILOperandType.TempRegister) + i;
                USILLocal local = new USILLocal("float4", tmpString, USILLocalType.Vector4);
                Locals.Add(local);
            }
        }

        private void HandleCustomData(SHDRInstruction inst)
        {
            USILLocal local = new USILLocal("const float4", "icb", USILLocalType.Vector4, true);
            foreach (float[] vector in inst.declData.customDataArray)
            {
                USILOperand vectorOperand = new USILOperand();
                vectorOperand.operandType = USILOperandType.ImmediateFloat;
                vectorOperand.immValueFloat = vector;
                vectorOperand.mask = USILConstants.XYZW_MASK;
                local.defaultValues.Add(vectorOperand);
            }
            Locals.Add(local);
        }

		private void AddOutputLocal()
		{
			string outputName = _dxShader.Shdr.shaderType switch
			{
				DirectXDisassembler.Type.Vertex => USILConstants.VERT_OUTPUT_LOCAL_NAME,
				DirectXDisassembler.Type.Pixel => USILConstants.FRAG_OUTPUT_LOCAL_NAME,
				_ => "o" // ?
			};
			string outputStructName = _dxShader.Shdr.shaderType switch
			{
				DirectXDisassembler.Type.Vertex => USILConstants.VERT_TO_FRAG_STRUCT_NAME,
				DirectXDisassembler.Type.Pixel => USILConstants.FRAG_OUTPUT_STRUCT_NAME,
				_ => "o" // ?
			};

			Locals.Add(new USILLocal(outputStructName, outputName, USILLocalType.Vector4));
		}

        ///////////////////////
        // stuff to be moved to other classes

        private int ConvertFloatToInt(float f)
        {
            return BitConverter.SingleToInt32Bits(f);
        }
    }
}
