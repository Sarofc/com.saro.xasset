//#define DEBUG_XASSET

using Saro.Core;
using Saro.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("Saro.XAsset.Editor")]

namespace Saro.XAsset
{
    /*
     * TODO
     * 
     * 1.异步API改为FTask版本
     * 2.引用计数问题！
     * 
     */

    [Serializable]
    public sealed class XAssetComponent : FEntity, IAssetInterface
    {
        public const string k_AssetBundles = "DLC";
        public const string k_XAssetManifestAsset = "Assets/XAsset/XAssetManifest.asset";
        public const string k_AssetExtension = ".unity3d";

#if UNITY_EDITOR
        internal static bool s_RuntimeMode = true;
#else
        internal readonly static bool s_RuntimeMode = true;
#endif
        internal static Func<string, Type, UnityEngine.Object> s_EditorLoader = null;

        [System.Diagnostics.Conditional("DEBUG_XASSET")]
        internal static void INFO(string msg)
        {
            Log.INFO("XAsset", msg);
        }

        internal static void WARN(string msg)
        {
            Log.WARN("XAsset", msg);
        }

        internal static void ERROR(string msg)
        {
            Log.ERROR("XAsset", msg);
        }

        internal static XAssetComponent Get()
        {
            return Game.Resolve<XAssetComponent>();
        }

        #region API

        public async FTask<bool> InitializeAsync()
        {
            if (string.IsNullOrEmpty(s_BasePath))
            {
                s_BasePath = Application.streamingAssetsPath + "/" + k_AssetBundles + "/";
            }

            if (string.IsNullOrEmpty(s_UpdatePath))
            {
                s_UpdatePath = Application.persistentDataPath + "/" + k_AssetBundles + "/";
            }

            //var path = string.Format("{0}/{1}", s_BasePath, Versions.Dataname);

            Clear();

            INFO(string.Format(
                "Initialize with: runtimeMode={0}\nbasePath：{1}\nupdatePath={2}",
                s_RuntimeMode, s_BasePath, s_UpdatePath));

            var request = new ManifestRequest { AssetUrl = k_XAssetManifestAsset };
            AddAssetRequest(request);

            var tcs = FTask<bool>.Create();

            request.Completed += (req) =>
            {
                req.DecreaseRefCount();
                if (req.IsError)
                {
                    ERROR("Init Error: " + req.Error);
                }

                tcs.SetResult(!req.IsError);
            };

            return await tcs;
        }


        /// <summary>
        /// 读取所有资源路径
        /// </summary>
        /// <returns></returns>
        public IReadOnlyDictionary<string, string> GetAllAssetPaths()
        {
            return m_AssetToBundles;
        }

        /// <summary>
        /// 基础路径
        /// </summary>
        public static string s_BasePath { get; internal set; }

        /// <summary>
        /// 热更路径
        /// </summary>
        public static string s_UpdatePath { get; internal set; }

        /// <summary>
        /// 获取当前平台AB包文件夹名字
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentPlatformAssetBundleFolderName()
        {
            return GetAssetBundleFolderName(Application.platform);
        }

        public static string GetAssetBundleFolderName(RuntimePlatform target)
        {
            switch (target)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "IOS";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "Windows";
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return "OSX";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            if (m_RunningSceneRequest != null)
            {
                m_RunningSceneRequest.DecreaseRefCount();
                m_RunningSceneRequest = null;
            }

            RemoveUnusedAssets();
            UpdateAssets();
            UpdateBundles();

            m_AssetToBundles.Clear();
            m_BundleToDependencies.Clear();
        }

        private SceneAssetRequest m_RunningSceneRequest;

        public IAssetRequest LoadSceneCallback(string path, bool additive = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("invalid path");
                return null;
            }

            var request = new SceneAssetAsyncRequest(path, additive);
            if (!additive)
            {
                if (m_RunningSceneRequest != null)
                {
                    m_RunningSceneRequest.DecreaseRefCount();
                    m_RunningSceneRequest = null;
                }
                m_RunningSceneRequest = request;
            }
            request.Load();
            request.IncreaseRefCount();
            m_SceneRequests.Add(request);
            INFO(string.Format("LoadScene:{0}", path));
            return request;
        }

        public T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            var request = LoadAsset(path, typeof(T));
            if (request != null && request.Asset != null)
            {
                return request.Asset as T;
            }
            else
            {
                ERROR($"error {request.Error}");
            }

            return null;
        }

        public IAssetRequest LoadAssetCallback<T>(string path) where T : UnityEngine.Object
        {
            return LoadAssetInternal(path, typeof(T), true);
        }

        public IAssetRequest LoadAssetCallback(string path, Type type)
        {
            return LoadAssetInternal(path, type, true);
        }

        public IAssetRequest LoadAsset(string path, Type type)
        {
            return LoadAssetInternal(path, type, false);
        }

        public (FTask<T> task, IAssetRequest request) LoadAssetAsync<T>(string path) where T : UnityEngine.Object
        {
            var request = LoadAssetInternal(path, typeof(T), true);
            var tcs = FTask<T>.Create();
            request.Completed += _request =>
            {
                tcs.SetResult(_request.Asset as T);
            };
            return (tcs, request);
        }

        public (FTask<bool> task, IAssetRequest request) LoadSceneAsync(string path, bool additive = false)
        {
            var request = LoadSceneCallback(path, additive);
            var tcs = FTask<bool>.Create();
            request.Completed += _request =>
            {
                tcs.SetResult(!_request.IsError);
            };
            return (tcs, request);
        }

        public void UnloadAsset(IAssetRequest asset)
        {
            asset.DecreaseRefCount();
        }

        public void RemoveUnusedAssets()
        {
            foreach (var item in m_Assets)
            {
                if (item.Value.IsUnused())
                {
                    m_UnusedAssets.Add(item.Value);
                }
            }
            foreach (var request in m_UnusedAssets)
            {
                m_Assets.Remove(request.AssetUrl);
            }
            foreach (var item in m_UrlToBundles)
            {
                if (item.Value.IsUnused())
                {
                    m_UnusedBundles.Add(item.Value);
                }
            }
            foreach (var request in m_UnusedBundles)
            {
                m_UrlToBundles.Remove(request.AssetUrl);
            }
        }

        #endregion

        #region Private

        internal void OnManifestLoaded(XAssetManifest manifest)
        {
            var assets = manifest.assets;
            var dirs = manifest.dirs;
            var bundles = manifest.bundles;

            for (int i = 0; i < bundles.Length; i++)
            {
                BundleRef item = bundles[i];
                m_BundleToDependencies[item.name] = Array.ConvertAll(item.deps, id => bundles[id].name);
            }

            foreach (var item in assets)
            {
                var path = string.Format("{0}/{1}", dirs[item.dir], item.name);
                if (item.bundle >= 0 && item.bundle < bundles.Length)
                {
                    m_AssetToBundles[path] = bundles[item.bundle].name;
                }
                else
                {
                    ERROR(string.Format("{0} bundle {1} not exist.", path, item.bundle));
                }
            }
        }

        private Dictionary<string, AssetRequest> m_Assets = new Dictionary<string, AssetRequest>(StringComparer.Ordinal);

        private List<AssetRequest> m_LoadingAssets = new List<AssetRequest>();

        private List<SceneAssetRequest> m_SceneRequests = new List<SceneAssetRequest>();

        private List<AssetRequest> m_UnusedAssets = new List<AssetRequest>();

        private void UpdateAssets()
        {
            for (var i = 0; i < m_LoadingAssets.Count; ++i)
            {
                var request = m_LoadingAssets[i];
                if (request.Update())
                    continue;
                m_LoadingAssets.RemoveAt(i);
                --i;
            }

            if (m_UnusedAssets.Count > 0)
            {
                for (var i = 0; i < m_UnusedAssets.Count; ++i)
                {
                    var request = m_UnusedAssets[i];
                    if (!request.IsDone) continue;
                    INFO(string.Format("UnloadAsset:{0}", request.AssetUrl));
                    request.Unload();
                    m_UnusedAssets.RemoveAt(i);
                    i--;
                }
            }

            for (var i = 0; i < m_SceneRequests.Count; ++i)
            {
                var request = m_SceneRequests[i];
                if (request.Update() || !request.IsUnused())
                    continue;
                m_SceneRequests.RemoveAt(i);
                INFO(string.Format("UnloadScene:{0}", request.AssetUrl));
                request.Unload();
                RemoveUnusedAssets();
                --i;
            }
        }

        private void AddAssetRequest(AssetRequest request)
        {
            m_Assets.Add(request.AssetUrl, request);
            m_LoadingAssets.Add(request);
            request.Load();
        }

        private AssetRequest LoadAssetInternal(string path, Type type, bool async)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("invalid path");
                return null;
            }

            AssetRequest request;
            if (m_Assets.TryGetValue(path, out request))
            {
                request.Update();
                request.IncreaseRefCount();
                m_LoadingAssets.Add(request);
                return request;
            }

            if (GetAssetBundleName(path, out string assetBundleName))
            {
                request = async
                    ? new BundleAssetAsyncRequest(assetBundleName)
                    : new BundleAssetRequest(assetBundleName);
            }
            else
            {
                if (path.StartsWith("http://", StringComparison.Ordinal) ||
                    path.StartsWith("https://", StringComparison.Ordinal) ||
                    path.StartsWith("file://", StringComparison.Ordinal) ||
                    path.StartsWith("ftp://", StringComparison.Ordinal) ||
                    path.StartsWith("jar:file://", StringComparison.Ordinal))
                {
                    request = new WebAssetRequest();

                    //INFO("WebAssetRequest 加载: " + path);
                }
                else
                {
                    request = new AssetRequest();

                    //INFO("AssetRequest 加载：" + path);
                }
            }

            request.AssetUrl = path;
            request.AssetType = type;
            AddAssetRequest(request);
            request.IncreaseRefCount();

            INFO(string.Format("LoadAsset:{0}", path));

            return request;
        }

        #endregion

        #region Bundles

        private const int k_MAX_BUNDLES_PERFRAME = 0;

        private Dictionary<string, BundleRequest> m_UrlToBundles = new Dictionary<string, BundleRequest>(StringComparer.Ordinal);

        private List<BundleRequest> m_LoadingBundles = new List<BundleRequest>();

        private List<BundleRequest> m_UnusedBundles = new List<BundleRequest>();

        private List<BundleRequest> m_ToLoadBundles = new List<BundleRequest>();

        private Dictionary<string, string> m_AssetToBundles = new Dictionary<string, string>(StringComparer.Ordinal);

        private Dictionary<string, string[]> m_BundleToDependencies = new Dictionary<string, string[]>(StringComparer.Ordinal);

        internal bool GetAssetBundleName(string path, out string assetBundleName)
        {
            return m_AssetToBundles.TryGetValue(path, out assetBundleName);
        }

        private string[] GetAllDependencies(string bundle)
        {
            if (m_BundleToDependencies.TryGetValue(bundle, out string[] deps))
                return deps;

            return new string[0];
        }

        internal BundleRequest LoadBundle(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, false);
        }

        internal BundleRequest LoadBundleAsync(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, true);
        }

        internal void UnloadBundle(BundleRequest bundle)
        {
            bundle.DecreaseRefCount();
        }

        private void UnloadDependencies(BundleRequest bundle)
        {
            for (var i = 0; i < bundle.Dependencies.Count; i++)
            {
                var item = bundle.Dependencies[i];
                item.DecreaseRefCount();
            }

            bundle.Dependencies.Clear();
        }

        private void LoadDependencies(BundleRequest bundle, string assetBundleName, bool asyncRequest)
        {
            var dependencies = GetAllDependencies(assetBundleName);
            if (dependencies.Length <= 0)
                return;
            for (var i = 0; i < dependencies.Length; i++)
            {
                var item = dependencies[i];
                bundle.Dependencies.Add(LoadBundleInternal(item, asyncRequest));
            }
        }

        private BundleRequest LoadBundleInternal(string assetBundleName, bool asyncMode)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                ERROR("assetBundleName == null");
                return null;
            }

            var url = GetDataPath(assetBundleName) + assetBundleName;

            if (m_UrlToBundles.TryGetValue(url, out BundleRequest bundleRequest))
            {
                bundleRequest.Update();
                bundleRequest.IncreaseRefCount();
                m_LoadingBundles.Add(bundleRequest);
                return bundleRequest;
            }

            if (url.StartsWith("http://", StringComparison.Ordinal) ||
                url.StartsWith("https://", StringComparison.Ordinal) ||
                url.StartsWith("file://", StringComparison.Ordinal) ||
                url.StartsWith("ftp://", StringComparison.Ordinal))
                bundleRequest = new WebBundleRequest();
            else
                bundleRequest = asyncMode ? new BundleAsyncRequest() : new BundleRequest();

            bundleRequest.AssetUrl = url;
            m_UrlToBundles.Add(url, bundleRequest);

            if (k_MAX_BUNDLES_PERFRAME > 0 && (bundleRequest is BundleAsyncRequest || bundleRequest is WebBundleRequest))
            {
                m_ToLoadBundles.Add(bundleRequest);
            }
            else
            {
                bundleRequest.Load();
                m_LoadingBundles.Add(bundleRequest);
                INFO("LoadBundle: " + url);
            }

            LoadDependencies(bundleRequest, assetBundleName, asyncMode);

            bundleRequest.IncreaseRefCount();
            return bundleRequest;
        }

        private string GetDataPath(string bundleName)
        {
            if (string.IsNullOrEmpty(s_UpdatePath))
                return s_BasePath;

            if (File.Exists(s_UpdatePath + bundleName))
                return s_UpdatePath;

            return s_BasePath;
        }

        private void UpdateBundles()
        {
            if (m_ToLoadBundles.Count > 0 &&
                k_MAX_BUNDLES_PERFRAME > 0 &&
                m_LoadingBundles.Count < k_MAX_BUNDLES_PERFRAME
                )
            {
                for (var i = 0; i < Math.Min(k_MAX_BUNDLES_PERFRAME - m_LoadingBundles.Count, m_ToLoadBundles.Count); ++i)
                {
                    var item = m_ToLoadBundles[i];
                    if (item.LoadState == ELoadState.Init)
                    {
                        item.Load();
                        m_LoadingBundles.Add(item);
                        m_ToLoadBundles.RemoveAt(i);
                        --i;
                    }
                }
            }


            for (var i = 0; i < m_LoadingBundles.Count; i++)
            {
                var item = m_LoadingBundles[i];
                if (item.Update())
                    continue;
                m_LoadingBundles.RemoveAt(i);
                --i;
            }

            if (m_UnusedBundles.Count <= 0) return;
            {
                for (var i = 0; i < m_UnusedBundles.Count; i++)
                {
                    var item = m_UnusedBundles[i];
                    if (item.IsDone)
                    {
                        UnloadDependencies(item);
                        item.Unload();
                        INFO("UnloadBundle: " + item.AssetUrl);
                        m_UnusedBundles.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        #endregion

        internal void Update()
        {
            UpdateAssets();
            UpdateBundles();
        }
    }

    [FObjectSystem]
    internal sealed class XAssetComponentUpdateSystem : UpdateSystem<XAssetComponent>
    {
        public override void Update(XAssetComponent self)
        {
            self.Update();
        }
    }
}