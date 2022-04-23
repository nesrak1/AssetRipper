using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetRipper.Library.Exporters.Shaders
{
	public static class UnityShaderConstants
	{
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

		// TODO: same here
		public static readonly HashSet<string> BUILTIN_CBUFFER_NAMES = new HashSet<string>()
		{
			"UnityPerCamera",
			"UnityPerCameraRare",
			"UnityLighting",
			"UnityLightingOld",
			"UnityShadows",
			"UnityPerDraw",
			"UnityStereoGlobals",
			"UnityStereoEyeIndices",
			"UnityStereoEyeIndex",
			"UnityPerDrawRare",
			"UnityPerFrame",
			"UnityFog",
			"UnityLightmaps",
			"UnityReflectionProbes",
			"UnityProbeVolume"
		};
	}
}
