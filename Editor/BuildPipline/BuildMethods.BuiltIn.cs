using UnityEditor;
using UnityEngine;

namespace Saro.XAsset.Build
{
    internal partial class BuildMethods : IBuildProcessor
    {
        //[BuildMethod(0, "Clear AssetBundleNames", false)]
        //private static void ClearAssetBundles()
        //{
        //    BuildScript.ClearAssetBundles();
        //    Debug.Log("[XAsset] ClearAssetBundles");
        //}

        [XAssetBuildMethod(0, "ApplyBuildRules", false)]
        private static void ApplyBuildRules()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            XAssetBuildScript.ApplyBuildRules();
            watch.Stop();
            Debug.Log("[XAsset] ApplyBuildRules " + watch.ElapsedMilliseconds + " ms.");
        }

        [XAssetBuildMethod(20, "Build AssetBundles", false)]
        private static void BuildAssetBundles()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            XAssetBuildScript.BuildAssetBundles();
            watch.Stop();
            Debug.Log("[XAsset] BuildAssetBundles " + watch.ElapsedMilliseconds + " ms.");
        }

        [XAssetBuildMethod(21, "Copy AssetBundles", false)]
        private static void CopyAssetBundles()
        {
            var destFolder = Application.streamingAssetsPath + "/" + XAssetComponent.k_AssetBundles;
            XAssetBuildScript.CopyAssetBundlesTo(destFolder);
            AssetDatabase.Refresh();
            Debug.Log($"[XAsset] Copy AssetBundles to SreammingFolder: {destFolder}");
        }

        [XAssetBuildMethod(50, "Build Player")]
        private static void BuildPlayer()
        {
            XAssetBuildScript.BuildPlayer();
        }
    }
}