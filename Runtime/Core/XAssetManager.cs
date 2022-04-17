﻿#define DEBUG_XASSET

using Saro.Core;
using Saro.Net;
using Saro.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saro.XAsset
{
    /*
     * 
     * https://baddogzz.github.io/2020/02/07/Unload-Resources/
     * 
     * WARN
     * 
     * 1. 如果资源加载没控制好，需要在合适的时候调用 Resources.UnloadUnusedAssets()
     * 
     * TODO
     * 
     * 2. 考虑使用一个policy类，对资源加载策略进行封装
     *   x 切场景时清理资源
     *   x 每隔多久，清理一次无用资源
     *   - 资源多久没使用，就标记自动卸载
     *   
     */

    public sealed partial class XAssetManager : IAssetInterface
    {
        /// <summary>
        /// 加载远端资源失败委托，统一托管
        /// </summary>
        public Action<string> OnLoadRemoteAssetError { get; set; } = (asseteName) =>
        {
            ERROR($" <color=blue>[auto]</color> OnLoadRemoteAssetError. download error. assetName: {asseteName}");
        };

        /// <summary>
        /// 加载远端资源状态委托，false:开始加载 true:完成加载
        /// </summary>
        public Action<string, bool> OnLoadRemoteAsset { get; set; }

        public Manifest Manifest => m_Manifest;
        private Manifest m_Manifest;

        public string RemoteVersionUrl { get { if (m_Manifest == null) return null; return m_Manifest.remoteVersionUrl; } }
        public string RemoteAssetUrl { get { if (m_Manifest == null) return null; return m_Manifest.remoteAssetUrl; } }

        public class XAssetPolicy
        {
            /// <summary>
            /// TODO dev 自动卸载资源，默认关闭
            /// </summary>
            public bool AutoUnloadAsset { get; set; } = false;
            /// <summary>
            /// <see cref="AutoUnloadAsset"/>开启后，多久调用一次<see cref="UnloadUnusedAssets"/>
            /// </summary>
            public float AutoUnloadAssetInterval { get; set; } = 5f;
            /// <summary>
            /// TODO 资源多久不用后，才被卸载，这个需要思考下 有无必要，实现难度
            /// </summary>
            public float UnusedAssetUnloadDelay { get; set; } = 30f;

            /// <summary>
            /// 卸载场景时，卸载无用资源，默认开启
            /// </summary>
            public bool UnloadAssetWhenUnloadScene { get; set; } = true;
        }

        public XAssetPolicy Policy { get; private set; } = new XAssetPolicy();

        private float m_TimeToAutoUnloadAsset;

        public enum EMode
        {
            /// <summary>
            /// 使用 AssetDatabase
            /// </summary>
            Editor = 0,
            /// <summary>
            /// 加载 ExtraAssets 目录
            /// </summary>
            Simulate = 1,
            /// <summary>
            /// 真机
            /// </summary>
            Runtime = 2,
        }

        public static EMode s_Mode = EMode.Editor;

        /// <summary>
        /// 只内部调用，外部通过 <see cref="Main.Resolve(Type)"/> 来获取
        /// </summary>
        internal static XAssetManager Current => Main.Resolve<IAssetInterface>() as XAssetManager;

        #region Service Impl

        void IService.Update()
        {
            //UnityEngine.Profiling.Profiler.BeginSample("[XAsset] UpdateAssets");
            UpdateAssets();
            //UnityEngine.Profiling.Profiler.EndSample();

            //UnityEngine.Profiling.Profiler.BeginSample("[XAsset] UpdateBundles");
            UpdateBundles();
            //UnityEngine.Profiling.Profiler.EndSample();

            //UnityEngine.Profiling.Profiler.BeginSample("[XAsset] AutoUnloadUnussedAssets");
            AutoUnloadUnussedAssets();
            //UnityEngine.Profiling.Profiler.EndSample();
        }

        void IService.Awake() => Initialize();

        void IService.Dispose() { }

        #endregion

        #region API

        private void Initialize()
        {
#if !UNITY_EDITOR
            s_Mode = EMode.Runtime;
#endif

            INFO($"加载模式 = {s_Mode}");

            //ClearAssetReference();

            var manifest = LoadLocalManifest(XAssetConfig.k_ManifestAsset);
            if (manifest != null)
            {
                OnManifestLoaded(manifest);
            }
        }

        // TODO 如果真的需要清理所有资源，那不能被清理得资源要标记上 HideFlags.DontUnloadUnusedAsset
        // issue 进游戏热更后
        // 从没有appendhash，转为appendhash后，因载入了部分内容相同，但名字不一样的ab
        // 报错 The AssetBundle 'xxx.bundle' can't be loaded because another AssetBundle with the same files is already loaded.
        [System.Obsolete("看上去不需要清理，只有热更了已经加载的bundle才会有问题，需要重启")]
        public void ClearAssetReference()
        {
            foreach (var item in m_AssetHandleMap)
            {
                item.Value.SetRefCountForce(0);
            }

            foreach (var item in m_BundleHandleMap)
            {
                item.Value.SetRefCountForce(0);
            }

            UnloadUnusedAssets();
            UpdateAssets();
            UpdateBundles();
        }

        public string GetAppVersion()
        {
            if (m_Manifest == null) return "-1";
            return m_Manifest.appVersion;
        }

        public string GetResVersion()
        {
            if (m_Manifest == null) return "-1";
            return m_Manifest.resVersion.ToString();
        }

        private SceneAssetHandle m_MainSceneHandle;
        public IAssetHandle LoadSceneAsync(string path, bool additive = false)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("invalid path");
                return null;
            }

            var handle = new SceneAssetAsyncHandle(path, additive);
            if (!additive)
            {
                if (m_MainSceneHandle != null)
                {
                    m_MainSceneHandle.DecreaseRefCount();
                    m_MainSceneHandle = null;
                }
                m_MainSceneHandle = handle;
            }
            handle.Load();
            handle.IncreaseRefCount();
            m_SceneHandles.Add(handle);

            __AnalyzeHandle(handle);

            INFO($"LoadScene: {path}");
            return handle;
        }

        public IAssetHandle LoadAssetAsync(string path, Type type)
        {
            return LoadAssetInternal(path, type, true);
        }

        public IAssetHandle LoadAsset(string path, Type type)
        {
            return LoadAssetInternal(path, type, false);
        }

        /// <summary>
        /// 卸载所有无用资源，只计算出哪些资源是无用的，并没有真正卸载
        /// </summary>
        public void UnloadUnusedAssets(bool immediate = true)
        {
            // asset
            foreach (var item in m_AssetHandleMap)
            {
                if (item.Value.IsUnused())
                {
                    //{
                    //    if (!item.Value.IsMarkUnload())
                    //    {
                    //        item.Value.MarkUnload(immediate);
                    //        m_UnusedAssetHandles.Add(item.Value);
                    //    }
                    //}

                    m_UnusedAssetHandles.Add(item.Value);
                }
            }
            foreach (var handle in m_UnusedAssetHandles)
            {
                m_AssetHandleMap.Remove(handle.AssetUrl);
            }

            // bundle
            foreach (var item in m_BundleHandleMap)
            {
                if (item.Value.IsUnused())
                {
                    //{
                    //    if (!item.Value.IsMarkUnload())
                    //    {
                    //        item.Value.MarkUnload(immediate);
                    //        m_UnusedBundleHandles.Add(item.Value);
                    //    }
                    //}

                    m_UnusedBundleHandles.Add(item.Value);
                }
            }
            foreach (var handle in m_UnusedBundleHandles)
            {
                m_BundleHandleMap.Remove(handle.AssetUrl);
            }

            INFO($"<color=red>UnloadUnusedAssets. Time: {Time.unscaledTime}</color>");
        }

        #endregion

        #region Private

        internal Manifest LoadLocalManifest(string manifestName)
        {
#if UNITY_EDITOR
            if (s_Mode == EMode.Editor)
            {
                return null;
            }
#endif

            if (TryGetAssetPath(manifestName, out var manifestPath, out _))
            {
                string content = null;
                try
                {
                    content = FileUtility.ReadAllText(manifestPath);

                    if (!string.IsNullOrEmpty(content))
                    {
                        var manifest = ScriptableObject.CreateInstance<Manifest>();
                        manifest.Load(content);
                        return manifest;
                    }
                }
                catch (Exception e)
                {
                    ERROR(e.ToString());
                }
            }

            return null;
        }

        private void OnManifestLoaded(Manifest manifest)
        {
            if (m_Manifest != null)
            {
                GameObject.Destroy(m_Manifest);
            }

            m_Manifest = manifest;

            //INFO(m_Manifest.ToString());
        }

        private void AutoUnloadUnussedAssets()
        {
            if (Policy.AutoUnloadAsset)
            {
                if (m_TimeToAutoUnloadAsset < Time.unscaledTime)
                {
                    UnloadUnusedAssets(false);

                    m_TimeToAutoUnloadAsset = Time.unscaledTime + Policy.AutoUnloadAssetInterval;
                }
            }
        }

        private readonly Dictionary<string, AssetHandle> m_AssetHandleMap = new(StringComparer.Ordinal); // 已加载asset
        private readonly List<AssetHandle> m_LoadingAssetHandles = new(128); // 正在加载asset
        private readonly List<AssetHandle> m_UnusedAssetHandles = new(128); // 待卸载asset
        private readonly List<SceneAssetHandle> m_SceneHandles = new(4); // 正在加载/已加载 scene

        private void UpdateAssets()
        {
            for (var i = 0; i < m_LoadingAssetHandles.Count; ++i)
            {
                var handle = m_LoadingAssetHandles[i];
                if (handle.Update())
                    continue;
                m_LoadingAssetHandles.RemoveAt(i--);
            }

            if (m_UnusedAssetHandles.Count > 0)
            {
                for (var i = 0; i < m_UnusedAssetHandles.Count; ++i)
                {
                    var handle = m_UnusedAssetHandles[i];
                    if (!handle.IsDone) continue;

                    // 延迟卸载支持
                    //{
                    //    if (!handle.IsUnused())
                    //    {
                    //        handle.UnMarkUnload();
                    //        m_UnusedAssetHandles.RemoveAt(i--);
                    //        continue;
                    //    }
                    //    if (!handle.IsReadyUnload()) continue;
                    //    m_AssetHandles.Remove(handle.AssetUrl); // 其他地方应该只mark
                    //}

                    INFO($"UnloadAsset: {handle.AssetUrl}");
                    handle.Unload();
                    m_UnusedAssetHandles.RemoveAt(i--);
                }
            }

            bool unloadScene = false;
            for (var i = 0; i < m_SceneHandles.Count; ++i)
            {
                var handle = m_SceneHandles[i];
                if (handle.Update() || !handle.IsUnused())
                    continue;
                INFO(string.Format("UnloadScene:{0}", handle.AssetUrl));
                handle.Unload();
                m_SceneHandles.RemoveAt(i--);

                unloadScene = true;
            }

            if (unloadScene)
            {
                if (Policy.UnloadAssetWhenUnloadScene)
                {
                    // 切场景时，也清理一次无用资源
                    UnloadUnusedAssets();
                }
            }
        }

        /// <summary>
        /// 加载资源
        /// <code>http 网络下载 只能异步，async = false 无效</code>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="type"></param>
        /// <param name="async"></param>
        /// <returns></returns>
        private AssetHandle LoadAssetInternal(string path, Type type, bool async)
        {
            if (string.IsNullOrEmpty(path))
            {
                ERROR("null or empty path");
                return null;
            }

            if (m_AssetHandleMap.TryGetValue(path, out AssetHandle handle)) // 有缓存，直接取
            {
                handle.Update();
                handle.IncreaseRefCount();
                //m_LoadingAssetHandles.Add(handle); // TODO 不应该再加一次？
                return handle;
            }

            if (GetAssetBundleName(path, out string assetBundleName)) // 没缓存取 ab 里拿
            {
                handle = async
                    ? new BundleAssetAsyncHandle(assetBundleName)
                    : new BundleAssetHandle(assetBundleName);
            }
            else
            {
                // 需要直接加载remote的资源文件，自己去下载得了，这里就不管了
                //if (path.StartsWith("http://", StringComparison.Ordinal) ||
                //    path.StartsWith("https://", StringComparison.Ordinal) ||
                //    path.StartsWith("file://", StringComparison.Ordinal) ||
                //    path.StartsWith("ftp://", StringComparison.Ordinal) ||
                //    path.StartsWith("jar:file://", StringComparison.Ordinal)) 
                //{
                //    // 网络下载，再加载
                //    handle = new WebAssetHandle();
                //}
                //else 
                {
                    // 都不是，则使用 AssetDatabase 加载，真机会报错
                    handle = new AssetDatabaseHandle();
                }
            }

            handle.AssetUrl = path;
            handle.AssetType = type;
            m_AssetHandleMap.Add(handle.AssetUrl, handle);
            m_LoadingAssetHandles.Add(handle);
            handle.Load();
            handle.IncreaseRefCount();

            __AnalyzeHandle(handle);

            INFO($"LoadAsset: {path}");

            return handle;
        }

        #endregion

        #region Bundles

        /// <summary>
        /// 异步加载bundle，每帧加载数量限制
        /// <code>0 意为不限制</code>
        /// </summary>
        public int MaxBundlesPerframe { get; set; } = 0;

        private readonly Dictionary<string, BundleHandle> m_BundleHandleMap = new(128, StringComparer.Ordinal); // 已加载bundle
        private readonly List<BundleHandle> m_LoadingBundleHandles = new(64); // 正在加载bundle
        private readonly Queue<BundleHandle> m_PendingBundleHandles = new(64); // 待加载bundle
        private readonly List<BundleHandle> m_UnusedBundleHandles = new(128); // 引用为0，待卸载bundle

        public IReadOnlyDictionary<string, BundleRef> AssetToBundle => m_Manifest.AssetToBundle;
        public IReadOnlyDictionary<string, BundleRef[]> BundleToDeps => m_Manifest.BundleToDeps;

        internal bool GetAssetBundleName(string path, out string assetBundleName)
        {
#if UNITY_EDITOR
            if (s_Mode == EMode.Editor)
            {
                assetBundleName = null;
                return false;
            }
#endif

            var ret = AssetToBundle.TryGetValue(path, out var bundleRef);
            assetBundleName = ret ? bundleRef.name : null;
            return ret;
        }

        internal BundleRef[] GetBundleDependencies(string assetBundleName)
        {
            if (BundleToDeps.TryGetValue(assetBundleName, out BundleRef[] deps))
                return deps;

            return null;
        }

        internal BundleHandle LoadBundle(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, false);
        }

        internal BundleHandle LoadBundleAsync(string assetBundleName)
        {
            return LoadBundleInternal(assetBundleName, true);
        }

        internal void UnloadBundle(BundleHandle bundle)
        {
            bundle.DecreaseRefCount();
        }

        private void UnloadDependencies(BundleHandle handle)
        {
            for (var i = 0; i < handle.Dependencies.Count; i++)
            {
                var depHandle = handle.Dependencies[i];
                depHandle.DecreaseRefCount();
            }

            handle.Dependencies.Clear();
        }

        private void LoadDependencies(BundleHandle handle, string assetBundleName, bool async)
        {
            var dependencies = GetBundleDependencies(assetBundleName);
            if (dependencies == null || dependencies.Length <= 0)
                return;

            for (var i = 0; i < dependencies.Length; i++)
            {
                var depRef = dependencies[i];
                handle.Dependencies.Add(LoadBundleInternal(depRef.name, async));
            }
        }

        private BundleHandle LoadBundleInternal(string assetBundleName, bool async)
        {
            if (string.IsNullOrEmpty(assetBundleName))
            {
                ERROR("assetBundleName == null");
                return null;
            }

            bool exists = TryGetAssetPath(assetBundleName, out var assetPath, out var remoteAssets);

            if (m_BundleHandleMap.TryGetValue(assetPath, out var handle))
            {
                handle.Update();
                handle.IncreaseRefCount();
                //m_LoadingBundleHandles.Add(handle); // TODO 不应该再加一次了？
                return handle;
            }

            if (!exists)
            {
                if (remoteAssets != null) // 如果能获取到远端资源信息，就尝试去下载
                {
                    handle = new WebBundleHandle
                    {
                        Info = new DownloadInfo
                        {
                            DownloadUrl = XAssetConfig.GetRemoteAssetURL(remoteAssets.Name),
                            SavePath = assetPath,
                            Hash = remoteAssets.Hash,
                            Size = remoteAssets.Size,
                        },
                    };
                }
                else
                {
                    ERROR($"can't find remote asset: {assetPath}");
                }
            }

            if (handle == null)
            {
                handle = async ? new BundleAsyncHandle() : new BundleHandle();
            }

            handle.AssetUrl = assetPath;

            m_BundleHandleMap.Add(assetPath, handle);

            if (MaxBundlesPerframe > 0
                && m_LoadingAssetHandles.Count >= MaxBundlesPerframe
                && (handle is BundleAsyncHandle || handle is WebBundleHandle))
            {
                // 当前异步加载的bundle超过 配置上限，加到待加载队列里去
                m_PendingBundleHandles.Enqueue(handle);
            }
            else
            {
                handle.Load();
                m_LoadingBundleHandles.Add(handle);
                INFO("LoadBundle: " + handle.AssetUrl);
            }

            LoadDependencies(handle, assetBundleName, async);

            handle.IncreaseRefCount();
            __AnalyzeHandle(handle);
            return handle;
        }

        private void UpdateBundles()
        {
            if (m_PendingBundleHandles.Count > 0 &&
                MaxBundlesPerframe > 0 &&
                m_LoadingBundleHandles.Count < MaxBundlesPerframe
                )
            {
                var toLoadCount = Math.Min(MaxBundlesPerframe - m_LoadingBundleHandles.Count, m_PendingBundleHandles.Count);
                while (toLoadCount > 0)
                {
                    var handle = m_PendingBundleHandles.Dequeue();
                    if (handle.LoadState == ELoadState.Init)
                    {
                        handle.Load();
                        INFO("LoadBundle: " + handle.AssetUrl);
                        m_LoadingBundleHandles.Add(handle);
                    }
                    toLoadCount--;
                }
            }

            for (var i = 0; i < m_LoadingBundleHandles.Count; i++)
            {
                var handle = m_LoadingBundleHandles[i];
                if (handle.Update())
                    continue;
                m_LoadingBundleHandles.RemoveAt(i--);
            }

            if (m_UnusedBundleHandles.Count > 0)
            {
                for (var i = 0; i < m_UnusedBundleHandles.Count; i++)
                {
                    var handle = m_UnusedBundleHandles[i];
                    if (!handle.IsDone) continue;

                    // 延迟卸载支持
                    //{
                    //    if (!handle.IsUnused())
                    //    {
                    //handle.UnMarkUnload();
                    //        m_UnusedBundleHandles.RemoveAt(i--);
                    //        continue;
                    //    }
                    //    if (!handle.IsReadyUnload()) continue;
                    //    m_UrlToBundleHandles.Remove(handle.AssetUrl);
                    //}

                    UnloadDependencies(handle);

                    handle.Unload();
                    INFO("UnloadBundle: " + handle.AssetUrl);
                    m_UnusedBundleHandles.RemoveAt(i--);
                }
            }
        }

        #endregion

        #region Debug

#if UNITY_EDITOR
        // TODO show AssetHandleMap, BundleHandleMap. etc.
        [System.Diagnostics.DebuggerHidden]
        internal Dictionary<string, List<IAssetHandle>> AnalyzeHandles { get; private set; } = new(1024);
#endif

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void __AnalyzeHandle(IAssetHandle handle)
        {
#if UNITY_EDITOR
            if (!AnalyzeHandles.TryGetValue(handle.AssetUrl, out var list))
            {
                list = new List<IAssetHandle>();
                AnalyzeHandles.Add(handle.AssetUrl, list);
            }
            list.Add(handle);
#endif
        }

        [System.Diagnostics.Conditional("DEBUG_XASSET")]
        internal static void INFO(string msg)
        {
            Log.INFO("XAsset", msg);
        }

        [System.Diagnostics.Conditional("DEBUG_XASSET")]
        internal static void WARN(string msg)
        {
            Log.WARN("XAsset", msg);
        }

        internal static void ERROR(string msg)
        {
            Log.ERROR("XAsset", msg);
        }

        #endregion
    }
}