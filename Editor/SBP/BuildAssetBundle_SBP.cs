﻿using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;

namespace Saro.MoonAsset.Build
{
    public static class BuildAssetBundle_SBP
    {
        public static ReturnCode BuildAssetBundles(
            string outputPath,
            IList<AssetBundleBuild> assetBundleBuilds,
            BuildAssetBundleOptions options,
            BuildTarget buildTarget,
            out IBundleBuildResults results)
        {
            var buildContent = new BundleBuildContent(assetBundleBuilds);

            var buildParams = new BundleBuildParameters(buildTarget, BuildPipeline.GetBuildTargetGroup(buildTarget), outputPath);

            buildParams.ContiguousBundles = true;
            buildParams.WriteLinkXML = true;
            //buildParams.AppendHash = options.HasFlag(BuildAssetBundleOptions.AppendHashToAssetBundleName); // 使用了MoonAsset的appendhash，这个设置无效

            if (options.HasFlag(BuildAssetBundleOptions.ChunkBasedCompression))
                buildParams.BundleCompression = UnityEngine.BuildCompression.LZ4;
            else if (options.HasFlag(BuildAssetBundleOptions.UncompressedAssetBundle))
                buildParams.BundleCompression = UnityEngine.BuildCompression.Uncompressed;

            if (options.HasFlag(BuildAssetBundleOptions.DisableWriteTypeTree))
                buildParams.ContentBuildFlags |= ContentBuildFlags.DisableWriteTypeTree;

            if (!options.HasFlag(BuildAssetBundleOptions.ForceRebuildAssetBundle))
            {
                // TODO 需要搞CacheServer

                //BuildCache

                //Set build parameters for connecting to the Cache Server
                buildParams.UseCache = true;
                //buildParams.CacheServerHost = "buildcache.unitygames.com";
                //buildParams.CacheServerPort = 8126;
            }

            var customTasks = CustomBuildTasks();
            ReturnCode exitCode =
                ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, customTasks);
            return exitCode;
        }

        public static IList<IBuildTask> CustomBuildTasks()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            //buildTasks.Add(new SwitchToBuildPlatform()); // 不需要了，每次打包前，自己手动切，一般打包机一个平台一个工程。
            //buildTasks.Add(new RebuildSpriteAtlasCache()); // 在 BuildGroups.ProcessSpriteAtlases 提前调用过了，不需要再调用了

            // Player Scripts
            buildTasks.Add(new BuildPlayerScripts());
            //buildTasks.Add(new PostScriptsCallback());

            // Dependency
            buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
            buildTasks.Add(new CalculateCustomDependencyData());
#endif
            buildTasks.Add(new CalculateAssetDependencyData());
            buildTasks.Add(new StripUnusedSpriteSources());
            buildTasks.Add(new CreateBuiltInResourcesBundle(
                "builtindata" + MoonAssetConfig.k_AssetExtension,
                "builtinshader" + MoonAssetConfig.k_AssetExtension));
            //buildTasks.Add(new PostDependencyCallback());

            // Packing
            buildTasks.Add(new GenerateBundlePacking());
            buildTasks.Add(new UpdateBundleObjectLayout());
            buildTasks.Add(new GenerateBundleCommands());
            buildTasks.Add(new GenerateSubAssetPathMaps());
            buildTasks.Add(new GenerateBundleMaps());
            //buildTasks.Add(new PostPackingCallback());

            // Writing
            buildTasks.Add(new WriteSerializedFiles());
            buildTasks.Add(new ArchiveAndCompressBundles());
            //buildTasks.Add(new AppendBundleHash()); // 使用了MoonAsset的appendhash
            //buildTasks.Add(new PostWritingCallback());

            // Generate manifest files
            // IMPL manifest generation，使用了MoonAsset的manifest

            return buildTasks;
        }
    }
}