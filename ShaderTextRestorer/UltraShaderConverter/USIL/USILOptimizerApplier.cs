﻿using AssetRipper.Core.Classes.Shader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
    public static class USILOptimizerApplier
    {
        // order is important
        // they should probably be separated into different lists in the future
        // when I work out what categories there will be
        private static readonly List<Type> OPTIMIZER_TYPES = new()
        {
            // do metadders first
            typeof(USILCBufferMetadder),
            typeof(USILSamplerMetadder),
            typeof(USILInputOutputMetadder),

            // do detection optimizers which usually depend on metadders
            typeof(USILMatrixMulOptimizer),
			
            // do simplification optimizers last when detection has been finished
            typeof(USILCompareOrderOptimizer),
            typeof(USILAddNegativeOptimizer),
            typeof(USILAndOptimizer),
			typeof(USILForLoopOptimizer)
        };

        public static void Apply(UShaderProgram shader, ShaderSubProgram shaderData)
        {
            foreach (Type optimizerType in OPTIMIZER_TYPES)
            {
                IUSILOptimizer optimizer = (IUSILOptimizer)Activator.CreateInstance(optimizerType);
                optimizer.Run(shader, shaderData);
            }
        }
    }
}
