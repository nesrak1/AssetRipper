﻿using AssetRipper.Assets;
using AssetRipper.Export.UnityProjects.Project.Collections;
using AssetRipper.Export.UnityProjects.Project.Exporters;
using AssetRipper.SourceGenerated.Classes.ClassID_91;
using AssetRipper.SourceGenerated.Extensions;

namespace AssetRipper.Export.UnityProjects.AnimatorControllers
{
	public sealed class AnimatorControllerExportCollection : AssetsExportCollection
	{
		public AnimatorControllerExportCollection(IAssetExporter assetExporter, IAnimatorController controller) : base(assetExporter, controller)
		{
			foreach (IUnityObjectBase? dependency in controller.FetchEditorHierarchy())
			{
				if (dependency is not null && dependency != controller)
				{
					AddAsset(dependency);
				}
			}
		}
	}
}
