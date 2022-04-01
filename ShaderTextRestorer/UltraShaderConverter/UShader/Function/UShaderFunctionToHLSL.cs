using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
    public class UShaderFunctionToHLSL
    {
        private UShaderProgram _shader;
        private StringBuilder _stringBuilder;
        private string _baseIndent;
        private string _indent;
        private int _indentLevel;

        private delegate void InstHandler(USILInstruction inst);
        private Dictionary<USILInstructionType, InstHandler> _instructionHandlers;

        public UShaderFunctionToHLSL(UShaderProgram shader)
        {
            _shader = shader;

            _instructionHandlers = new()
            {
                { USILInstructionType.Move, new InstHandler(HandleMove) },
                { USILInstructionType.MoveConditional, new InstHandler(HandleMoveConditional) },
                { USILInstructionType.Add, new InstHandler(HandleAdd) },
                { USILInstructionType.Subtract, new InstHandler(HandleSubtract) },
                { USILInstructionType.Multiply, new InstHandler(HandleMultiply) },
                { USILInstructionType.Divide, new InstHandler(HandleDivide) },
                { USILInstructionType.MultiplyAdd, new InstHandler(HandleMultiplyAdd) },
                { USILInstructionType.And, new InstHandler(HandleAnd) },
                { USILInstructionType.Or, new InstHandler(HandleOr) },
                { USILInstructionType.Not, new InstHandler(HandleNot) },
                { USILInstructionType.Minimum, new InstHandler(HandleMinimum) },
                { USILInstructionType.Maximum, new InstHandler(HandleMaximum) },
                { USILInstructionType.SquareRoot, new InstHandler(HandleSquareRoot) },
                { USILInstructionType.SquareRootReciprocal, new InstHandler(HandleSquareRootReciprocal) },
                { USILInstructionType.Logarithm2, new InstHandler(HandleLogarithm2) },
                { USILInstructionType.Exponential, new InstHandler(HandleExponential) },
                { USILInstructionType.Reciprocal, new InstHandler(HandleReciprocal) },
                { USILInstructionType.Fractional, new InstHandler(HandleFractional) },
                { USILInstructionType.Floor, new InstHandler(HandleFloor) },
                { USILInstructionType.Ceiling, new InstHandler(HandleCeiling) },
                { USILInstructionType.Round, new InstHandler(HandleRound) },
                { USILInstructionType.Sine, new InstHandler(HandleSine) },
                { USILInstructionType.Cosine, new InstHandler(HandleCosine) },
                { USILInstructionType.ShiftLeft, new InstHandler(HandleShiftLeft) },
                { USILInstructionType.ShiftRight, new InstHandler(HandleShiftRight) },
                { USILInstructionType.DotProduct2, new InstHandler(HandleDotProduct) },
                { USILInstructionType.DotProduct3, new InstHandler(HandleDotProduct) },
                { USILInstructionType.DotProduct4, new InstHandler(HandleDotProduct) },
                { USILInstructionType.Sample, new InstHandler(HandleSample) },
                { USILInstructionType.SampleComparison, new InstHandler(HandleSample) },
                { USILInstructionType.SampleComparisonLODZero, new InstHandler(HandleSample) },
                { USILInstructionType.SampleLOD, new InstHandler(HandleSample) },
                { USILInstructionType.Discard, new InstHandler(HandleDiscard) },
                { USILInstructionType.IfFalse, new InstHandler(HandleIf) },
                { USILInstructionType.IfTrue, new InstHandler(HandleIf) },
                { USILInstructionType.Else, new InstHandler(HandleElse) },
                { USILInstructionType.EndIf, new InstHandler(HandleEndIf) },
                { USILInstructionType.Equal, new InstHandler(HandleEqual) },
                { USILInstructionType.NotEqual, new InstHandler(HandleNotEqual) },
                { USILInstructionType.LessThan, new InstHandler(HandleLessThan) },
                { USILInstructionType.LessThanOrEqual, new InstHandler(HandleLessThanOrEqual) },
                { USILInstructionType.GreaterThan, new InstHandler(HandleGreaterThan) },
                { USILInstructionType.GreaterThanOrEqual, new InstHandler(HandleGreaterThanOrEqual) },
                { USILInstructionType.Return, new InstHandler(HandleReturn) },
                // extra
                { USILInstructionType.MultiplyMatrixByVector, new InstHandler(MultiplyMatrixByVector) },
                { USILInstructionType.Comment, new InstHandler(HandleComment) }
            };
        }

        public string Convert(int indentDepth)
        {
            _baseIndent = new string(' ', indentDepth * 4);
            _indent = new string(' ', 4);

            _stringBuilder = new StringBuilder();

            WriteLocals();

            foreach (USILInstruction inst in _shader.instructions)
            {
                if (_instructionHandlers.ContainsKey(inst.instructionType))
                {
                    _instructionHandlers[inst.instructionType](inst);
                }
            }

            return _stringBuilder.ToString();
        }

        private void WriteLocals()
        {
            foreach (USILLocal local in _shader.locals)
            {
                if (local.defaultValues.Count > 0 && local.isArray)
                {
                    AppendLine($"{local.type} {local.name}[{local.defaultValues.Count}] {{");
                    if (local.defaultValues.Count > 0)
                    {
                        _indentLevel++;
                        for (int i = 0; i < local.defaultValues.Count; i++)
                        {
                            USILOperand operand = local.defaultValues[i];
                            string comma = i != local.defaultValues.Count - 1 ? "," : "";
                            AppendLine($"{operand}{comma}");
                        }
                        _indentLevel--;
                    }
                    AppendLine("};");
                }
                else
                {
                    AppendLine($"{local.type} {local.name};");
                }
            }
        }

        private void HandleMove(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMoveConditional(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} ? {srcOps[1]} : {srcOps[2]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleAdd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} + {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSubtract(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} - {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMultiply(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} * {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDivide(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} / {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMultiplyAdd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} * {srcOps[1]} + {srcOps[2]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleAnd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            int op1UintSize = srcOps[1].GetValueCount();
            string value = $"uint{op0UintSize}({srcOps[0]}) & uint{op1UintSize}({srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleOr(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            int op1UintSize = srcOps[1].GetValueCount();
            string value = $"uint{op0UintSize}({srcOps[0]}) | uint{op1UintSize}({srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNot(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            string value = $"~uint{op0UintSize}({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMinimum(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"min({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMaximum(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"max({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSquareRoot(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"sqrt({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSquareRootReciprocal(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"rsqrt({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLogarithm2(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"log({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleExponential(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"exp({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleReciprocal(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"rcp({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFractional(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"frac({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFloor(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"floor({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleCeiling(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"ceil({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleRound(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"round({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSine(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"sin({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleCosine(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"cos({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleShiftLeft(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} << {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleShiftRight(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} >> {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDotProduct(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"dot({srcOps[0]}, {srcOps[1]})");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSample(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand textureOperand = inst.srcOperands[2];
            string value = textureOperand.operandType switch
            {
                USILOperandType.Sampler2D => $"tex2D({srcOps[2]}, {srcOps[0]})",
                USILOperandType.Sampler3D => $"tex3D({srcOps[2]}, {srcOps[0]})",
                USILOperandType.Sampler4D => $"texCUBE({srcOps[2]}, {srcOps[0]})",
                _ => $"texND({srcOps[2]}, {srcOps[0]})" // unknown real type
            };
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDiscard(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}discard;");
        }

        private void HandleIf(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            if (inst.instructionType == USILInstructionType.IfTrue)
                AppendLine($"{comment}if ({srcOps[0]}) {{");
            else
                AppendLine($"{comment}if (!({srcOps[0]})) {{");

            _indentLevel++;
        }

        private void HandleElse(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}} else {{");
            _indentLevel++;
        }

        private void HandleEndIf(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}}");
        }

        private void HandleEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} == {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNotEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} != {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLessThan(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} < {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLessThanOrEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} <= {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleGreaterThan(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} > {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleGreaterThanOrEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} >= {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleReturn(USILInstruction inst)
        {
			string outputName = _shader.shaderFunctionType switch
			{
				UShaderFunctionType.Vertex => USILConstants.VERT_OUTPUT_LOCAL_NAME,
				UShaderFunctionType.Fragment => USILConstants.FRAG_OUTPUT_LOCAL_NAME,
				_ => "o" // ?
			};

			string value = $"return {outputName}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{value};");
        }

        private void MultiplyMatrixByVector(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"mul({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleComment(USILInstruction inst)
        {
            AppendLine($"//{inst.destOperand.comment};");
        }

        private string WrapSaturate(USILInstruction inst, string str)
        {
            if (inst.saturate)
            {
                str = $"saturate({str})";
            }
            return str;
        }

        private void AppendLine(string line)
        {
            _stringBuilder.Append(_baseIndent);

            for (int i = 0; i < _indentLevel; i++)
                _stringBuilder.Append(_indent);

            _stringBuilder.AppendLine(line);
        }

        // this is awful
        private string CommentString(USILInstruction inst)
        {
            return inst.commented ? "//" : "";
        }
    }
}
