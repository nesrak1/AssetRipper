using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
    public enum USILOperandType
    {
        None,
        Comment,

        TempRegister,
        InputRegister,
        OutputRegister,
        ResourceRegister,
        SamplerRegister,

        ConstantBuffer,

        Sampler2D,
        Sampler3D,
        Sampler4D,

        ImmediateInt,
        ImmediateFloat,
		ImmediateConstantBuffer,
		Matrix,

        Multiple // i.e. fixed2(cb1, cb2)
    }
}
