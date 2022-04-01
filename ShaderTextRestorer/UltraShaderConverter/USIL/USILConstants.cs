using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShaderLabConvert
{
	// Should be renamed UConstants probably?
    public static class USILConstants
    {
        public static readonly int[] XYZW_MASK = new int[] { 0, 1, 2, 3 };
        public static readonly char[] MASK_CHARS = new char[] { 'x', 'y', 'z', 'w' };
        public static readonly string[] MATRIX_MASK_CHARS = new string[] {
            "_m00", "_m01", "_m02", "_m03",
            "_m10", "_m11", "_m12", "_m13",
            "_m20", "_m21", "_m22", "_m23",
            "_m30", "_m31", "_m32", "_m33"
        };
        public static readonly string[] TMATRIX_MASK_CHARS = new string[] {
            "_m00", "_m10", "_m20", "_m30",
            "_m01", "_m11", "_m21", "_m31",
            "_m02", "_m12", "_m22", "_m32",
            "_m03", "_m13", "_m23", "_m33"
        };

		public static string VERT_INPUT_NAME = "v";
		public static string VERT_OUTPUT_LOCAL_NAME = "o";
		public static string VERT_TO_FRAG_STRUCT_NAME = "v2f";

		public static string FRAG_INPUT_NAME = "i";
		public static string FRAG_OUTPUT_LOCAL_NAME = "o";
		public static string FRAG_OUTPUT_STRUCT_NAME = "fout";
	}
}
