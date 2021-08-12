#define USE_SBP

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;


#if USE_SBP
using UnityEngine.Build.Pipeline;
using UnityEditor.Build.Pipeline;
#endif

/*
 * TODO:
 * 
 * 1. 使用sbp打包，废弃旧打包系统
 * 
 */
namespace Saro.XAsset.Build
{
    public static class XAssetBuildScript
    {
        private const string k_XAssetBuildGroupsPath = "Assets/XAsset/XAssetBuildGroups.asset";
        private const string k_XAssetSettingsPath = "Assets/XAsset/XAssetSettings.asset";
        public static string s_DLCFolder = "ExtraResources/DLC/" + GetPlatformName();
        public static string s_DatFolder = s_DLCFolder + "/Dat";

        public static void ClearAssetBundleNames()
        {
            var allAssetBundleNames = AssetDatabase.GetAllAssetBundleNames();
            for (var i = 0; i < allAssetBundleNames.Length; i++)
            {
                var assetBundleName = allAssetBundleNames[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                                    string.Format("Clear AssetBundles {0}/{1}", i, allAssetBundleNames.Length), assetBundleName,
                                    i * 1f / allAssetBundleNames.Length))
                    break;

                AssetDatabase.RemoveAssetBundleName(assetBundleName, true);
            }
            EditorUtility.ClearProgressBar();
        }

        public static void MarkAssetBundleNames()
        {
            ClearAssetBundleNames();

            var buildGroups = GetXAssetBuildGroups();
            for (int i = 0; i < buildGroups.ruleBundles.Length; i++)
            {
                var bundle = buildGroups.ruleBundles[i];
                for (int j = 0; j < bundle.assets.Length; j++)
                {
                    var asset = bundle.assets[j];
                    var importer = AssetImporter.GetAtPath(asset);
                    importer.assetBundleName = bundle.bundle;
                }
            }

            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        internal static void ApplyBuildGroups()
        {
            var rules = GetXAssetBuildGroups();
            rules.Apply();
        }

        public static void CopyAssetBundlesTo(string destFolder)
        {
            if (Directory.Exists(destFolder))
            {
                Directory.Delete(destFolder, true);
            }

            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }

            if (true)
            {
                if (!Directory.Exists(s_DLCFolder)) return;

                var files = Directory.GetFiles(s_DLCFolder);
                foreach (var src in files)
                {
                    var fileName = Path.GetFileName(src);

                    var dest = Path.Combine(destFolder, fileName);
                    if (File.Exists(src))
                    {
                        File.Copy(src, dest, true);
                    }
                }
            }
            else // TODO vfs
            {
                if (!Directory.Exists(s_DatFolder)) return;

                var files = Directory.GetFiles(s_DatFolder);
                foreach (var src in files)
                {
                    var fileName = Path.GetFileName(src);

                    var dest = Path.Combine(destFolder, fileName);
                    if (File.Exists(src))
                    {
                        File.Copy(src, dest, true);
                    }
                }
            }
        }

        public static string GetPlatformName()
        {
            return GetPlatformForAssetBundles(EditorUserBuildSettings.activeBuildTarget);
        }

        private static string GetPlatformForAssetBundles(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                case BuildTarget.StandaloneOSX:
                    return "OSX";
                default:
                    return null;
            }
        }

        private static string[] GetBuiltScenesFromXAssetSettings()
        {
            var builtInScenes = GetXAssetBuildGroups().scenesInBuild;
            var scenes = new HashSet<string>();
            foreach (SceneAsset item in builtInScenes)
            {
                var path = AssetDatabase.GetAssetPath(item);
                if (!string.IsNullOrEmpty(path))
                {
                    scenes.Add(path);
                }
            }

            return scenes.ToArray();
        }

        [System.Obsolete("Legacy Build, use SBP now", true)]
        private static string GetAssetBundleManifestFilePath()
        {
            var relativeAssetBundlesOutputPathForPlatform = Path.Combine("Asset", GetPlatformName());
            return Path.Combine(relativeAssetBundlesOutputPathForPlatform, GetPlatformName()) + ".manifest";
        }

        public static void BuildPlayer()
        {
            var outputPath =
                Path.Combine(Environment.CurrentDirectory,
                    "ExtraResources/Build/" + GetPlatformName() + "/" + Application.productName
                        .ToLower()); //EditorUtility.SaveFolderPanel("Choose Location of the Built Game", "", "");

            if (Directory.Exists(outputPath))
            {
                Directory.Delete(outputPath, true);
            }

            if (outputPath.Length == 0)
                return;

            var builtInScenes = GetBuiltScenesFromXAssetSettings();
            if (builtInScenes.Length == 0)
            {
                Debug.Log("Built In Scenes is empty.");
                return;
            }

            var targetName = GetBuildTargetAppName(EditorUserBuildSettings.activeBuildTarget);
            if (targetName == null)
                return;

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = builtInScenes,
                locationPathName = outputPath + targetName,
#if !USE_SBP
                assetBundleManifestPath = GetAssetBundleManifestFilePath(),
#endif
                target = EditorUserBuildSettings.activeBuildTarget,
            };

            if (EditorUserBuildSettings.development)
            {
                buildPlayerOptions.options |= BuildOptions.Development;
            }

            if (GetXAssetSettings().detailBuildReport)
            {
                buildPlayerOptions.options |= BuildOptions.DetailedBuildReport;
            }

            BuildPipeline.BuildPlayer(buildPlayerOptions);
            OpenFolderUtility.OpenDirectory(outputPath);
        }

        private static string CreateAssetBundleDirectory()
        {
            if (Directory.Exists(s_DLCFolder))
                Directory.Delete(s_DLCFolder, true);

            if (!Directory.Exists(s_DLCFolder))
                Directory.CreateDirectory(s_DLCFolder);

            return s_DLCFolder;
        }

        public static void BuildAssetBundles()
        {
#if USE_SBP
            SBPBuildAssetBundles();
#else   
            LegacyBuildAssetBundles();
#endif
        }

        [System.Obsolete("use sbp!")]
        private static void LegacyBuildAssetBundles()
        {
            var outputFolder = CreateAssetBundleDirectory();
            var options = GetXAssetSettings().buildAssetBundleOptions;

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var xassetBuildGroups = GetXAssetBuildGroups();
            var assetBundleBuilds = xassetBuildGroups.GetAssetBundleBuilds();
            var assetBundleManifest = BuildPipeline.BuildAssetBundles(outputFolder, assetBundleBuilds, options, buildTarget);
            if (assetBundleManifest == null)
            {
                return;
            }

            var xassetManifest = GetXAssetManifest();
            var dirs = new List<string>();
            var assets = new List<AssetRef>();
            var bundles = assetBundleManifest.GetAllAssetBundles();
            var bundle2Ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                bundle2Ids[bundle] = index;
            }

            var bundleRefs = new List<BundleRef>();
            for (var index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                var deps = assetBundleManifest.GetAllDependencies(bundle);
                var path = string.Format("{0}/{1}", outputFolder, bundle);
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        bundleRefs.Add(new BundleRef
                        {
                            name = bundle,
                            //id = index,
                            deps = Array.ConvertAll(deps, input => bundle2Ids[input]),
                            len = stream.Length,
                            hash = assetBundleManifest.GetAssetBundleHash(bundle).ToString(),
                        });
                    }
                }
                else
                {
                    Debug.LogError(path + " file not exsit.");
                }
            }

            for (var i = 0; i < xassetBuildGroups.ruleAssets.Length; i++)
            {
                var item = xassetBuildGroups.ruleAssets[i];
                var path = item.asset;
                var dir = Path.GetDirectoryName(path).Replace("\\", "/");

                var index = dirs.FindIndex(o => o.Equals(dir));
                if (index == -1)
                {
                    index = dirs.Count;
                    dirs.Add(dir);
                }
                try
                {
                    var asset = new AssetRef
                    {
                        bundle = bundle2Ids[item.bundle],
                        dir = index,
                        name = Path.GetFileName(path),
                    };
                    assets.Add(asset);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{item.bundle} {index} {Path.GetFileName(path)}");
                    throw e;
                }

            }

            xassetManifest.dirs = dirs.ToArray();
            xassetManifest.assets = assets.ToArray();
            xassetManifest.bundles = bundleRefs.ToArray();

            EditorUtility.SetDirty(xassetManifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var manifestBundleName = "xassetmanifest.unity3d";
            assetBundleBuilds = new[] {
                new AssetBundleBuild {
                    assetNames = new[] { AssetDatabase.GetAssetPath (xassetManifest), },
                    assetBundleName = manifestBundleName
                }
            };

            BuildPipeline.BuildAssetBundles(outputFolder, assetBundleBuilds, options, buildTarget);
            ArrayUtility.Add(ref bundles, manifestBundleName);

            var version = GetXAssetBuildGroups().AddVersion();

            // TODO 先不打vfs
            //Update.VersionList.BuildVersionList(outputFolder, s_DatFolder, bundles, version);
        }

#if USE_SBP
        private static void SBPBuildAssetBundles()
        {
            var outputFolder = CreateAssetBundleDirectory();
            var options = GetXAssetSettings().buildAssetBundleOptions;

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var xassetBuildGroups = GetXAssetBuildGroups();
            var assetBundleBuilds = xassetBuildGroups.GetAssetBundleBuilds();

            var retCode = SBPBuildAssetBundle.BuildAssetBundles(outputFolder, assetBundleBuilds, options, buildTarget, out var result);

            if (retCode != ReturnCode.Success)
            {
                Debug.LogError("Build AssetBundle Error. code: " + retCode);
                return;
            }


            //// sbpmanifest，运行时暂时用不到，测试用
            //var sbpManifest = ScriptableObject.CreateInstance<CompatibilityAssetBundleManifest>();
            //sbpManifest.SetResults(result.BundleInfos);
            //File.WriteAllText(outputFolder, JsonUtility.ToJson(sbpManifest));
            //XAssetComponent.ERROR(JsonUtility.ToJson(sbpManifest));


            var assetBundleManifest = result.BundleInfos;
            var xassetManifest = GetXAssetManifest();
            var dirs = new List<string>();
            var assets = new List<AssetRef>();
            var bundles = assetBundleManifest.Keys.ToArray();
            var bundle2Ids = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                bundle2Ids[bundle] = index;
            }

            var bundleRefs = new List<BundleRef>();
            for (var index = 0; index < bundles.Length; index++)
            {
                var bundle = bundles[index];
                var deps = assetBundleManifest[bundle].Dependencies;
                var path = string.Format("{0}/{1}", outputFolder, bundle);
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        bundleRefs.Add(new BundleRef
                        {
                            name = bundle,
                            deps = Array.ConvertAll(deps, input => bundle2Ids[input]),
                            len = stream.Length,
                            hash = assetBundleManifest[bundle].Hash.ToString(),
                        });
                    }
                }
                else
                {
                    Debug.LogError(path + " file not exsit.");
                }
            }

            for (var i = 0; i < xassetBuildGroups.ruleAssets.Length; i++)
            {
                var item = xassetBuildGroups.ruleAssets[i];
                var path = item.asset;
                var dir = Path.GetDirectoryName(path).Replace("\\", "/");

                var index = dirs.FindIndex(o => o.Equals(dir));
                if (index == -1)
                {
                    index = dirs.Count;
                    dirs.Add(dir);
                }
                try
                {
                    var asset = new AssetRef
                    {
                        bundle = bundle2Ids[item.bundle],
                        dir = index,
                        name = Path.GetFileName(path),
                    };
                    assets.Add(asset);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{item.bundle} {index} {Path.GetFileName(path)}");
                    throw e;
                }

            }

            xassetManifest.dirs = dirs.ToArray();
            xassetManifest.assets = assets.ToArray();
            xassetManifest.bundles = bundleRefs.ToArray();

            EditorUtility.SetDirty(xassetManifest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var manifestBundleName = "xassetmanifest.unity";
            assetBundleBuilds = new[]
            {
                new AssetBundleBuild
                {
                    assetNames = new[] { AssetDatabase.GetAssetPath (xassetManifest), },
                    assetBundleName = manifestBundleName
                }
            };

            SBPBuildAssetBundle.BuildAssetBundles(outputFolder, assetBundleBuilds, options, buildTarget, out var results);

            var version = GetXAssetBuildGroups().AddVersion();

            // TODO 打vfs
            //ArrayUtility.Add(ref bundles, manifestBundleName);
            //Update.VersionList.BuildVersionList(outputFolder, s_DatFolder, bundles, version);
        }
#endif


        private static string GetBuildTargetAppName(BuildTarget target)
        {
            string name = string.Empty;
            string time = string.Empty;
            if (GetXAssetSettings().buildSingleFolder)
            {
                name = PlayerSettings.productName;
                time = string.Empty;
            }
            else
            {
                time = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                name = PlayerSettings.productName + "-v" + PlayerSettings.bundleVersion + "." + GetXAssetBuildGroups().version;
            }
            switch (target)
            {
                case BuildTarget.Android:
                    return string.Format("/{0}-{1}.apk", name, time);

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return string.Format("/{0}-{1}.exe", name, time);

                case BuildTarget.StandaloneOSX:
                    return "/" + name + ".app";

                //case BuildTarget.WebGL:
                //case BuildTarget.iOS:
                //    return "";

                // Add more build targets for your own.
                default:
                    Debug.Log("Target not implemented.");
                    return null;
            }
        }

        internal static T GetAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }

            return asset;
        }

        internal static XAssetManifest GetXAssetManifest()
        {
            return GetAsset<XAssetManifest>(XAssetComponent.k_XAssetManifestAsset);
        }

        internal static XAssetBuildGroups GetXAssetBuildGroups()
        {
            return GetAsset<XAssetBuildGroups>(k_XAssetBuildGroupsPath);
        }

        internal static XAssetSettings GetXAssetSettings()
        {
            return GetAsset<XAssetSettings>(k_XAssetSettingsPath);
        }
    }
}