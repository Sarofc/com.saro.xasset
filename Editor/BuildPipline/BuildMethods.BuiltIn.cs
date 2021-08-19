using UnityEditor;
using UnityEngine;

namespace Saro.XAsset.Build
{
    internal partial class BuildMethods : IBuildProcessor
    {
        [XAssetBuildMethod(-2, "ClearAssetBundleNames", false)]
        private static void ClearAssetBundles()
        {
            XAssetBuildScript.ClearAssetBundleNames();
            Debug.Log("[XAsset] ClearAssetBundles");
        }

        //[XAssetBuildMethod(-1, "Mark AssetBundleNames", false)]
        //private static void MarkAssetBundleNames()
        //{
        //    XAssetBuildScript.MarkAssetBundleNames();
        //    Debug.Log("[XAsset] MarkAssetBundleNames");
        //}

        [XAssetBuildMethod(0, "ApplyBuildGroups", false)]
        private static void ApplyBuildGroups()
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            XAssetBuildScript.ApplyBuildGroups();
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

        [XAssetBuildMethod(30, "Upload Assets To FileServer", false)]
        private static void UploadAssetsToFileServer()
        {
            // Test
            XAssetBuildScript.UploadAssetsToFileServer("http://localhost:8088/DLC/Windows");
        }

        [XAssetBuildMethod(50, "Build Player")]
        private static void BuildPlayer()
        {
            XAssetBuildScript.BuildPlayer();
        }
    }
}