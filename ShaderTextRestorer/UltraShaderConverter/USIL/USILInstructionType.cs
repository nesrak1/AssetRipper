using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
	public enum USILInstructionType
	{
		// math
		Move, //dx: mov
		MoveConditional, //dx: movc
		Add, //dx: add/iadd
		Subtract, //dx: --- (add/iadd)
		Multiply, //dx: mul/imul
		Divide, //dx: div
		MultiplyAdd, //dx: mad

		And, //dx: and
		Or, //dx: or
		Xor, //dx: xor
		Not, //dx: not

		ShiftLeft, //dx: ishl
		ShiftRight, //dx: ishr

		Floor, //dx: round_ni
		Ceiling, //dx: round_pi
		Round, //dx: round_ne
		IntToFloat, //dx: itof
		FloatToInt, //dx: ftoi

		Minimum, //dx: min
		Maximum, //dx: max

		SquareRoot, //dx: sqrt
		SquareRootReciprocal, //dx: rsq

		Exponential, //dx: exp
		Logarithm2, //dx: log

		Sine, //dx: --- (sincos)
		Cosine, //dx: --- (sincos)

		DotProduct2, //dx: dp2
		DotProduct3, //dx: dp3
		DotProduct4, //dx: dp4

		Reciprocal, //dx: rcp
		Fractional, //dx: frc

		// comparisons
		Equal, //dx: eq
		NotEqual, //dx: ne
		GreaterThan, //dx: --- (ge/lt)
		GreaterThanOrEqual, //dx: ge
		LessThan, //dx: lt
		LessThanOrEqual, //dx: --- (ge/lt)

		// branching
		IfTrue, //dx: if(_z)
		IfFalse, //dx: if(_nz)
		Else, //dx: else
		EndIf, //dx: endif
		Return, //dx: return
		Loop, //dx: loop
		ForLoop, //dx: --- (loop, ige for example)
		EndLoop, //dx: endloop
		Break, //dx: break
		Continue, //dx: continue

		// graphics
		Discard, //dx: discard
		Sample, //dx: sample
		SampleLODBias, //dx: sample_b
		SampleComparison, //dx: sample_c
		SampleComparisonLODZero, //dx: sample_c_lz
		SampleLOD, //dx: sample_l

		// artifical instructions
		// dx
		Negate,
		Saturate,
		AbsoluteValue,

		TempRegister,

		// math
		MultiplyMatrixByVector,

		// unity
		UnityObjectToClipPos,
		UnityObjectToWorldNormal,
		WorldSpaceViewDir,

		// extra
		Comment,
	}
}
