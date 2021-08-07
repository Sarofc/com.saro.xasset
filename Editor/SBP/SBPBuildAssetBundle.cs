#define USE_SBP

#if USE_SBP

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace Saro.XAsset.Build
{
    public static class SBPBuildAssetBundle
    {
        public static ReturnCode BuildAssetBundles(string outputPath, IList<AssetBundleBuild> assetBundleBuilds, bool useChunkBasedCompression, BuildTarget buildTarget, out IBundleBuildResults results)
        {
            var buildContent = new BundleBuildContent(assetBundleBuilds);

            var buildParams = new BundleBuildParameters(buildTarget, BuildPipeline.GetBuildTargetGroup(buildTarget), outputPath);
            buildParams.WriteLinkXML = true;

            // Set build parameters for connecting to the Cache Server
            //buildParams.UseCache = true;
            //buildParams.CacheServerHost = "buildcache.unitygames.com";
            //buildParams.CacheServerPort = 8126;

            if (useChunkBasedCompression)
                buildParams.BundleCompression = UnityEngine.BuildCompression.LZ4;

            ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results);
            return exitCode;
        }
    }
}

#endif