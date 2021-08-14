using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

//sbp 不支持变体
//https://docs.unity3d.com/Packages/com.unity.scriptablebuildpipeline@1.5/manual/UpgradeGuide.html

namespace Saro.XAsset.Build
{
    public enum ENameBy
    {
        /// <summary>
        /// 显示指定包名
        /// </summary>
        Explicit,
        /// <summary>
        /// 以路径为包名
        /// </summary>
        Path,
        /// <summary>
        /// 以目录为包名
        /// </summary>
        Directory,
    }

    [Serializable]
    public struct RuleAsset
    {
        /// <summary>
        /// 资源路径
        /// </summary>
        public string asset;

        /// <summary>
        /// bundle名称
        /// </summary>
        public string bundle;
    }

    [Serializable]
    public struct RuleBundle
    {
        /// <summary>
        /// bundle名称
        /// </summary>
        public string bundle;

        /// <summary>
        /// 资源路径合集
        /// </summary>
        public string[] assets;
    }

    [Serializable]
    public struct BuildGroup
    {
        [Tooltip("搜索路径")]
        [Saro.Attributes.AssetPath(typeof(UnityEngine.Object), true)]
        public string searchPath;

        [Tooltip("搜索通配符，多个之间请用,(逗号)隔开")]
        public string searchPattern;

        [Tooltip("命名规则")]
        public ENameBy nameBy;

        [Tooltip("Explicit的名称")]
        public string assetBundleName;

        /// <returns>根据搜索规则,获取所有资源的路径</returns>
        public string[] GetAssets()
        {
            if (nameBy == ENameBy.Path)
            {
                if (!File.Exists(searchPath))
                {
                    Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                    return new string[0];
                }

                var ext = Path.GetExtension(searchPath).ToLower();
                if (ext == ".fbx") return new string[0];
                if (!XAssetBuildGroups.ValidateAsset(searchPath)) return new string[0];
                var asset = searchPath.Replace("\\", "/");
                return new string[] { asset };
            }
            else if (nameBy == ENameBy.Directory)
            {
                var patterns = searchPattern.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (!Directory.Exists(searchPath))
                {
                    Debug.LogWarning("Rule searchPath not exist:" + searchPath);
                    return new string[0];
                }

                var getFiles = new List<string>(256);
                foreach (var item in patterns)
                {
                    var files = Directory.GetFiles(searchPath, item, SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        if (Directory.Exists(file)) continue; // ?

                        var ext = Path.GetExtension(file).ToLower();
                        if (ext == ".fbx" && !item.Contains(ext)) continue;
                        if (!XAssetBuildGroups.ValidateAsset(file)) continue;
                        var asset = file.Replace("\\", "/");
                        getFiles.Add(asset);
                    }
                }

                return getFiles.ToArray();
            }

            throw new NotImplementedException($"type {nameBy} not handled.");
        }
    }

    // TODO 重构下
    public class XAssetBuildGroups : ScriptableObject
    {
        private readonly Dictionary<string, string> m_Asset2Bundles = new Dictionary<string, string>(1024, StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> m_ConflictedAssets = new Dictionary<string, string[]>(1024, StringComparer.Ordinal);
        private readonly HashSet<string> m_NeedOptimizedAssets = new HashSet<string>();
        private readonly Dictionary<string, HashSet<string>> m_Tracker = new Dictionary<string, HashSet<string>>(1024, StringComparer.Ordinal);

        [Header("Patterns")]
        public string searchPatternAsset = "*.asset";
        public string searchPatternController = "*.controller";
        public string searchPatternDir = "*";
        public string searchPatternMaterial = "*.mat";
        public string searchPatternPng = "*.png";
        public string searchPatternPrefab = "*.prefab";
        public string searchPatternScene = "*.unity";
        public string searchPatternText = "*.txt,*.bytes,*.json,*.csv,*.xml,*htm,*.html,*.yaml,*.fnt";

        [Tooltip("是否用hash代替bundle名称")]
        public bool nameBundleByHash = true;

        [Tooltip("构建的版本号")]
        [Header("Builds")]
        public string version = "0.0.0";

        [Tooltip("built-in场景")]
        public SceneAsset[] scenesInBuild = new SceneAsset[0];

        public BuildGroup[] groups = new BuildGroup[0];

        [Attributes.ReadOnly] public RuleAsset[] ruleAssets = new RuleAsset[0];
        [Attributes.ReadOnly] public RuleBundle[] ruleBundles = new RuleBundle[0];

        #region API

        /// <summary>
        /// 更新资源版本号
        /// </summary>
        /// <returns></returns>
        public System.Version AddVersion()
        {
            var versionObj = new System.Version(version);
            var revision = versionObj.Revision + 1;
            versionObj = new System.Version(versionObj.Major, versionObj.Minor, versionObj.Build, revision);
            version = versionObj.ToString();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            return versionObj;
        }

        public void Apply()
        {
            Clear();
            CollectAssets();
            AnalysisAssets();
            OptimizeAssets();
            Save();
        }

        public AssetBundleBuild[] GetAssetBundleBuilds()
        {
            var builds = new AssetBundleBuild[ruleBundles.Length];
            for (int i = 0; i < ruleBundles.Length; i++)
            {
                RuleBundle ruleBundle = ruleBundles[i];
                builds[i] = new AssetBundleBuild
                {
                    assetNames = ruleBundle.assets,
                    assetBundleName = ruleBundle.bundle,

                    // if use short path
                    //addressableNames = ruleBundle.assets.Select(Path.GetFileNameWithoutExtension).ToArray()
                };
            }

            return builds;
        }

        #endregion

        #region Private

        internal static bool ValidateAsset(string asset)
        {
            if (!(asset.StartsWith("Assets/") || asset.StartsWith("Packages/"))) return false;

            var ext = Path.GetExtension(asset).ToLower();
            return ext != ".dll" && ext != ".cs" && ext != ".meta" && ext != ".js" && ext != ".boo";
        }

        internal static bool IsSceneAsset(string asset)
        {
            return asset.EndsWith(".unity");
        }

        internal static bool IsShaderAsset(string asset)
        {
            return asset.EndsWith(".shader") || asset.EndsWith(".shadervariants");
        }

        internal string RuledAssetBundleName(string name)
        {
            if (nameBundleByHash)
            {
                return Utility.HashUtility.GetMd5Hash(name) + XAssetComponent.k_AssetExtension;
            }

            unsafe
            {
                //return name.Replace('\\', '/').Replace('/', '_').ToLower() + XAssetComponent.k_AssetExtension;

                var newName = name + XAssetComponent.k_AssetExtension;
                fixed (char* ptr = newName)
                {
                    var curPtr = ptr;
                    while (*curPtr != '\0')
                    {
                        var chr = *curPtr;
                        if (chr == '\\' || chr == '/')
                        {
                            *curPtr = '_';
                        }
                        else
                        {
                            // ToLower
                            if ('A' <= chr && chr <= 'Z')
                            {
                                *curPtr = (char)(chr | 0x20);
                            }
                        }
                        curPtr++;
                    }
                    return newName;
                }
            }
        }

        private void Track(string asset, string bundle)
        {
            if (!m_Tracker.TryGetValue(asset, out HashSet<string> bundles))
            {
                bundles = new HashSet<string>();
                m_Tracker.Add(asset, bundles);
            }

            bundles.Add(bundle);

            // 一个asset在多个bundles里, 即冗余了
            if (bundles.Count > 1)
            {
                m_Asset2Bundles.TryGetValue(asset, out string bundleName);
                if (string.IsNullOrEmpty(bundleName))
                {
                    m_NeedOptimizedAssets.Add(asset);
                }
            }
        }

        private Dictionary<string, List<string>> GetBundle2Assets()
        {
            var assetCount = m_Asset2Bundles.Count;
            var bundles = new Dictionary<string, List<string>>(assetCount / 2, StringComparer.Ordinal);
            foreach (var item in m_Asset2Bundles)
            {
                var bundle = item.Value;
                if (!bundles.TryGetValue(bundle, out List<string> list))
                {
                    list = new List<string>(64);
                    bundles[bundle] = list;
                }
                var asset = item.Key;
                if (!list.Contains(asset)) list.Add(asset);
            }

            return bundles;
        }

        private void Clear()
        {
            m_Tracker.Clear();
            m_NeedOptimizedAssets.Clear();
            m_ConflictedAssets.Clear();
            m_Asset2Bundles.Clear();
        }

        private void Save()
        {
            var bundle2Assets = GetBundle2Assets();
            ruleBundles = new RuleBundle[bundle2Assets.Count];
            var i = 0;
            foreach (var item in bundle2Assets)
            {
                ruleBundles[i] = new RuleBundle
                {
                    bundle = item.Key,
                    //variant = "",
                    assets = item.Value.ToArray()
                };
                i++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        private void OptimizeAssets()
        {
            // 剔除冗余资源
            int i = 0, max = m_ConflictedAssets.Count;
            foreach (var item in m_ConflictedAssets)
            {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冲突{0}/{1}", i, max), item.Key, i / (float)max))
                    break;

                var list = item.Value;
                foreach (string asset in list)
                {
                    if (!IsSceneAsset(asset))
                        m_NeedOptimizedAssets.Add(asset);
                }
                i++;
            }

            i = 0;
            max = m_NeedOptimizedAssets.Count;
            foreach (var item in m_NeedOptimizedAssets)
            {
                if (EditorUtility.DisplayCancelableProgressBar(string.Format("优化冗余{0}/{1}", i, max), item, i / (float)max))
                    break;

                OptimizeAsset(item);
                i++;
            }
        }

        private void AnalysisAssets()
        {
            var bundle2Assets = GetBundle2Assets();
            int i = 0, max = bundle2Assets.Count;
            foreach (var item in bundle2Assets)
            {
                var bundle = item.Key;

                if (EditorUtility.DisplayCancelableProgressBar(string.Format("分析依赖{0}/{1}", i, max), bundle, i / (float)max))
                    break;

                var assetPaths = bundle2Assets[bundle];

                var pathNames = assetPaths.ToArray();

                bool hasScene = false;
                bool allScene = true;
                foreach (string asset in assetPaths)
                {
                    if (IsSceneAsset(asset))
                    {
                        hasScene = true;
                    }
                    else
                    {
                        allScene = false;

                        if (IsShaderAsset(asset))
                            m_NeedOptimizedAssets.Add(asset);
                    }
                }

                //if (assetPaths.Exists(IsSceneAsset) && !assetPaths.TrueForAll(IsSceneAsset))
                bool sceneBundleConflicted = hasScene && !allScene;
                if (sceneBundleConflicted)
                    m_ConflictedAssets.Add(bundle, pathNames);

                // 获取所有被引用的资源
                var dependencies = AssetDatabase.GetDependencies(pathNames, true);
                if (dependencies.Length > 0)
                {
                    // 获取所有冗余项
                    foreach (var asset in dependencies)
                    {
                        if (ValidateAsset(asset))
                        {
                            Track(asset, bundle);

                            if (IsShaderAsset(asset))
                                m_NeedOptimizedAssets.Add(asset);
                        }
                    }
                }
                i++;
            }
        }

        private void CollectAssets()
        {
            for (int i = 0, max = groups.Length; i < max; i++)
            {
                var group = groups[i];

                if (EditorUtility.DisplayCancelableProgressBar(string.Format("收集资源{0}/{1}", i, max), group.searchPath, i / (float)max))
                    break;

                ApplyGroup(group);
            }

            var array = new RuleAsset[m_Asset2Bundles.Count];
            int index = 0;
            foreach (var item in m_Asset2Bundles)
            {
                array[index++] = new RuleAsset
                {
                    asset = item.Key,
                    bundle = item.Value
                };
            }

            Array.Sort(array, (a, b) => string.Compare(a.asset, b.asset, StringComparison.Ordinal));
            ruleAssets = array;
        }

        private void OptimizeAsset(string asset)
        {
            if (IsShaderAsset(asset))
                m_Asset2Bundles[asset] = RuledAssetBundleName("shaders");
            else
                m_Asset2Bundles[asset] = RuledAssetBundleName(asset);

            //XAssetComponent.ERROR($"OptimizeAsset: {asset}");
        }

        private void ApplyGroup(BuildGroup group)
        {
            var assets = group.GetAssets();

            var startIndex = group.searchPath.Length;
            foreach (var asset in assets)
            {
                if (IsShaderAsset(asset))
                {
                    m_Asset2Bundles[asset] = RuledAssetBundleName("shaders");
                    continue;
                }

                switch (group.nameBy)
                {
                    case ENameBy.Explicit:
                        {
                            m_Asset2Bundles[asset] = RuledAssetBundleName(group.assetBundleName);

                            break;
                        }
                    case ENameBy.Path:
                        {
                            m_Asset2Bundles[asset] = RuledAssetBundleName(asset);

                            break;
                        }
                    case ENameBy.Directory:
                        {
                            m_Asset2Bundles[asset] = RuledAssetBundleName(Path.GetDirectoryName(asset));

                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            }
        }

        #endregion

#if UNITY_EDITOR

        internal Dictionary<string, string> Asset2BundleCahce
        {
            get
            {
                if (m_Asset2BundleCahce == null)
                {
                    m_Asset2BundleCahce = new Dictionary<string, string>();

                    foreach (var vbundle in ruleBundles)
                    {
                        foreach (var vasset in vbundle.assets)
                        {
                            m_Asset2BundleCahce.Add(vasset, vbundle.bundle);
                        }
                    }
                }

                return m_Asset2BundleCahce;
            }
            set
            {
                m_Asset2BundleCahce = value;
            }
        }

        private Dictionary<string, string> m_Asset2BundleCahce;

        internal string[] GetAllAssetBundleNames()
        {
            return Array.ConvertAll(ruleBundles, bundle => bundle.bundle);
        }

        internal string GetAssetBundleName(string assetPath)
        {
            // sbp 不支持变体！
            return GetImplicitAssetBundleName(assetPath);
        }

        internal string GetImplicitAssetBundleName(string assetPath)
        {
            if (Asset2BundleCahce.TryGetValue(assetPath, out var bundle))
            {
                return bundle;
            }
            return null;
        }


        internal string[] GetAssetPathsFromAssetBundle(string assetBundleName)
        {
            foreach (var vbundle in ruleBundles)
            {
                if (string.CompareOrdinal(vbundle.bundle, assetBundleName) == 0)
                    return vbundle.assets;
            }

            return new string[0];
        }
#endif
    }
}