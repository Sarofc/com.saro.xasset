#define USE_SBP

#if USE_SBP

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEditor.Build.Pipeline.Utilities;

namespace Saro.XAsset.Build
{
    public static class SBPBuildAssetBundle
    {
        public static ReturnCode BuildAssetBundles(string outputPath, IList<AssetBundleBuild> assetBundleBuilds, BuildAssetBundleOptions options, BuildTarget buildTarget, out IBundleBuildResults results)
        {
            var buildContent = new BundleBuildContent(assetBundleBuilds);

            var buildParams = new BundleBuildParameters(buildTarget, BuildPipeline.GetBuildTargetGroup(buildTarget), outputPath);

            buildParams.ContiguousBundles = true;
            buildParams.WriteLinkXML = true;
            buildParams.AppendHash = false;


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

            var customTasks = AssetBundleBuiltInResourcesExtraction();

            ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results, customTasks);
            //ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results);
            return exitCode;
        }

        public static IList<IBuildTask> AssetBundleBuiltInResourcesExtraction()
        {
            var buildTasks = new List<IBuildTask>();

            // Setup
            buildTasks.Add(new SwitchToBuildPlatform());
            buildTasks.Add(new RebuildSpriteAtlasCache());

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
            buildTasks.Add(new CreateBuiltInResourcesBundle("unitybuiltindata", "unitybuiltinshader"));
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
            //buildTasks.Add(new AppendBundleHash());
            //buildTasks.Add(new PostWritingCallback());

            // Generate manifest files
            // TODO: IMPL manifest generation

            return buildTasks;
        }
    }
}

#endif