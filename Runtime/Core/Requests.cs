using Saro.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Saro.XAsset
{
    using Object = UnityEngine.Object;

    public enum ELoadState
    {
        Init,
        LoadAssetBundle,
        LoadAsset,
        Loaded,
        Unload,
    }

    public class AssetRequest : Reference, IAssetRequest
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public Type AssetType { get; set; }
        /// <summary>
        /// 资源路径
        /// </summary>
        public string AssetUrl { get; set; }

        /// <summary>
        /// 加载状态
        /// </summary>
        public ELoadState LoadState { get; protected set; }

        public AssetRequest()
        {
            Asset = null;
            LoadState = ELoadState.Init;
        }

        /// <summary>
        /// 是否加载完，不管成功失败
        /// </summary>
        public virtual bool IsDone
        {
            get { return true; }
        }

        /// <summary>
        /// 是否有报错
        /// </summary>
        public virtual bool IsError
        {
            get
            {
                return !string.IsNullOrEmpty(Error);
            }
        }

        /// <summary>
        /// 加载进度
        /// </summary>
        public virtual float Progress
        {
            get { return 1; }
        }

        /// <summary>
        /// 报错字符串
        /// </summary>
        public virtual string Error { get; protected set; }

        /// <summary>
        /// 加载的文本
        /// </summary>
        public string Text { get; protected set; }

        /// <summary>
        /// 加载的字节流
        /// </summary>
        public byte[] Bytes { get; protected set; }

        /// <summary>
        /// 加载的资源
        /// </summary>
        public Object Asset { get; protected set; }

        internal virtual void Load()
        {
            if (!XAssetComponent.s_RuntimeMode && XAssetComponent.s_EditorLoader != null)
                Asset = XAssetComponent.s_EditorLoader(AssetUrl, AssetType);
            if (Asset == null)
            {
                Error = "error! file not exist:" + AssetUrl;
            }
        }

        internal virtual void Unload()
        {
            if (Asset == null)
                return;

            if (!XAssetComponent.s_RuntimeMode)
            {
                if (!(Asset is GameObject))
                    Resources.UnloadAsset(Asset);
            }

            Asset = null;
        }

        internal bool Update()
        {
            if (!IsDone)
                return true;
            if (Completed == null)
                return false;
            try
            {
                Completed.Invoke(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            Completed = null;
            return false;
        }

        public Action<IAssetRequest> Completed { get; set; }
    }

    public class ManifestRequest : AssetRequest
    {
        private BundleRequest m_Request;
        private string m_AssetName;

        public override float Progress
        {
            get
            {
                switch (LoadState)
                {
                    case ELoadState.LoadAssetBundle:
                        return m_Request.Progress;

                    case ELoadState.Loaded:
                        return 1f;
                }

                return string.IsNullOrEmpty(Error) ? 1f : 0f;
            }
        }

        public override bool IsDone
        {
            get
            {
                if (!string.IsNullOrEmpty(Error))
                {
                    return true;
                }

                return LoadState == ELoadState.Loaded;
            }
        }

        internal override void Load()
        {
            m_AssetName = Path.GetFileName(AssetUrl);
            if (XAssetComponent.s_RuntimeMode)
            {
                var assetBundleName = m_AssetName.Replace(".asset", ".unity3d").ToLower();
                //Debug.LogError($"[XAsset] asset: {m_AssetName} \nurl: {Url} \nbundleName: {assetBundleName}");
                m_Request = XAssetComponent.Get().LoadBundleAsync(assetBundleName);
                m_Request.Completed = Request_completed;
                LoadState = ELoadState.LoadAssetBundle;
            }
            else
            {
                LoadState = ELoadState.Loaded;
            }
        }

        private void Request_completed(IAssetRequest ar)
        {
            m_Request.Completed = null;

            if (m_Request.IsError)
            {
                Error = m_Request.Error;
            }
            else
            {
                //var manifest = m_Request.AssetBundle.LoadAsset<XAssetManifest>(m_AssetName);
                var manifest = m_Request.AssetBundle.LoadAsset<XAssetManifest>(AssetUrl);

                //Debug.LogError($"print ab \"{m_Request.AssetBundle.name}\" \n{String.Join("\n", m_Request.AssetBundle.GetAllAssetNames())}");

                if (manifest == null)
                {
                    Error = "manifest == null. url: " + m_AssetName;
                }
                else
                {
                    XAssetComponent.Get().OnManifestLoaded(manifest);
                    m_Request.AssetBundle.Unload(true);
                    m_Request.AssetBundle = null;
                }
            }

            LoadState = ELoadState.Loaded;
        }

        internal override void Unload()
        {
            if (m_Request != null)
            {
                m_Request.DecreaseRefCount();
                m_Request = null;
            }
        }
    }

    public class BundleAssetRequest : AssetRequest
    {
        protected readonly string m_AssetBundleName;
        protected BundleRequest m_BundleRequest;

        public BundleAssetRequest(string bundle)
        {
            m_AssetBundleName = bundle;
        }

        internal override void Load()
        {
            // fix
            // 同一帧，先调异步接口，再调用同步接口，同步接口 bundle.assetBundle 报空
            // （不确定异步加载bundle未完成时的情况）

            m_BundleRequest = XAssetComponent.Get().LoadBundle(m_AssetBundleName);

            Asset = m_BundleRequest.AssetBundle.LoadAsset(AssetUrl, AssetType);
        }

        internal override void Unload()
        {
            if (m_BundleRequest != null)
            {
                m_BundleRequest.DecreaseRefCount();
                m_BundleRequest = null;
            }

            Asset = null;
        }
    }

    public class BundleAssetAsyncRequest : BundleAssetRequest
    {
        private AssetBundleRequest m_AssetBundleRequest;

        public BundleAssetAsyncRequest(string bundle)
            : base(bundle)
        {
        }

        public override bool IsDone
        {
            get
            {
                if (Error != null || m_BundleRequest.Error != null)
                    return true;

                for (int i = 0, max = m_BundleRequest.Dependencies.Count; i < max; i++)
                {
                    var item = m_BundleRequest.Dependencies[i];
                    if (item.Error != null)
                        return true;
                }

                switch (LoadState)
                {
                    case ELoadState.Init:
                        return false;
                    case ELoadState.Loaded:
                        return true;
                    case ELoadState.LoadAssetBundle:
                        {
                            if (!m_BundleRequest.IsDone)
                                return false;

                            for (int i = 0, max = m_BundleRequest.Dependencies.Count; i < max; i++)
                            {
                                var item = m_BundleRequest.Dependencies[i];
                                if (!item.IsDone)
                                    return false;
                            }

                            if (m_BundleRequest.AssetBundle == null)
                            {
                                Error = "assetBundle == null";
                                return true;
                            }

                            m_AssetBundleRequest = m_BundleRequest.AssetBundle.LoadAssetAsync(AssetUrl, AssetType);
                            LoadState = ELoadState.LoadAsset;
                            break;
                        }
                    case ELoadState.Unload:
                        break;
                    case ELoadState.LoadAsset:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (LoadState != ELoadState.LoadAsset)
                    return false;
                if (!m_AssetBundleRequest.isDone)
                    return false;
                Asset = m_AssetBundleRequest.asset;
                LoadState = ELoadState.Loaded;
                return true;
            }
        }

        public override float Progress
        {
            get
            {
                var bundleProgress = m_BundleRequest.Progress;
                if (m_BundleRequest.Dependencies.Count <= 0)
                    return bundleProgress * 0.3f + (m_AssetBundleRequest != null ? m_AssetBundleRequest.progress * 0.7f : 0);
                for (int i = 0, max = m_BundleRequest.Dependencies.Count; i < max; i++)
                {
                    var item = m_BundleRequest.Dependencies[i];
                    bundleProgress += item.Progress;
                }

                return bundleProgress / (m_BundleRequest.Dependencies.Count + 1) * 0.3f +
                       (m_AssetBundleRequest != null ? m_AssetBundleRequest.progress * 0.7f : 0);
            }
        }

        internal override void Load()
        {
            m_BundleRequest = XAssetComponent.Get().LoadBundleAsync(m_AssetBundleName);
            LoadState = ELoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            m_AssetBundleRequest = null;
            LoadState = ELoadState.Unload;
            base.Unload();
        }
    }

    public class SceneAssetRequest : AssetRequest
    {
        protected readonly LoadSceneMode m_LoadSceneMode;
        protected readonly string m_SceneName;
        protected string m_AssetBundleName;
        protected BundleRequest m_Bundle;

        public SceneAssetRequest(string path, bool addictive)
        {
            AssetUrl = path;
            XAssetComponent.Get().GetAssetBundleName(path, out m_AssetBundleName);
            m_SceneName = Path.GetFileNameWithoutExtension(AssetUrl);
            m_LoadSceneMode = addictive ? LoadSceneMode.Additive : LoadSceneMode.Single;
        }

        public override float Progress
        {
            get { return 1; }
        }

        internal override void Load()
        {
            if (!string.IsNullOrEmpty(m_AssetBundleName))
            {
                m_Bundle = XAssetComponent.Get().LoadBundle(m_AssetBundleName);
                if (m_Bundle != null)
                    SceneManager.LoadScene(m_SceneName, m_LoadSceneMode);
            }
            else
            {
                try
                {
                    SceneManager.LoadScene(m_SceneName, m_LoadSceneMode);
                    LoadState = ELoadState.LoadAsset;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Error = e.ToString();
                    LoadState = ELoadState.Loaded;
                }
            }
        }

        internal override void Unload()
        {
            if (m_Bundle != null)
                m_Bundle.DecreaseRefCount();

            if (m_LoadSceneMode == LoadSceneMode.Additive)
            {
                if (SceneManager.GetSceneByName(m_SceneName).isLoaded)
                    SceneManager.UnloadSceneAsync(m_SceneName);
            }

            m_Bundle = null;
        }
    }

    public class SceneAssetAsyncRequest : SceneAssetRequest
    {
        private AsyncOperation m_Request;

        public SceneAssetAsyncRequest(string path, bool addictive)
            : base(path, addictive)
        {
        }

        public override float Progress
        {
            get
            {
                if (m_Bundle == null)
                    return m_Request == null ? 0 : m_Request.progress;

                var bundleProgress = m_Bundle.Progress;
                if (m_Bundle.Dependencies.Count <= 0)
                    return bundleProgress * 0.3f + (m_Request != null ? m_Request.progress * 0.7f : 0);
                for (int i = 0, max = m_Bundle.Dependencies.Count; i < max; i++)
                {
                    var item = m_Bundle.Dependencies[i];
                    bundleProgress += item.Progress;
                }

                return bundleProgress / (m_Bundle.Dependencies.Count + 1) * 0.3f +
                       (m_Request != null ? m_Request.progress * 0.7f : 0);
            }
        }

        public override bool IsDone
        {
            get
            {
                switch (LoadState)
                {
                    case ELoadState.Loaded:
                        return true;
                    case ELoadState.LoadAssetBundle:
                        {
                            if (m_Bundle == null || m_Bundle.Error != null)
                                return true;

                            for (int i = 0, max = m_Bundle.Dependencies.Count; i < max; i++)
                            {
                                var item = m_Bundle.Dependencies[i];
                                if (item.Error != null)
                                    return true;
                            }

                            if (!m_Bundle.IsDone)
                                return false;

                            for (int i = 0, max = m_Bundle.Dependencies.Count; i < max; i++)
                            {
                                var item = m_Bundle.Dependencies[i];
                                if (!item.IsDone)
                                    return false;
                            }

                            LoadSceneAsync();

                            break;
                        }
                    case ELoadState.Unload:
                        break;
                    case ELoadState.LoadAsset:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (LoadState != ELoadState.LoadAsset)
                    return false;
                if (m_Request != null && !m_Request.isDone)
                    return false;
                LoadState = ELoadState.Loaded;
                return true;
            }
        }

        private void LoadSceneAsync()
        {
            try
            {
                m_Request = SceneManager.LoadSceneAsync(m_SceneName, m_LoadSceneMode);
                LoadState = ELoadState.LoadAsset;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Error = e.ToString();
                LoadState = ELoadState.Loaded;
            }
        }

        internal override void Load()
        {
            if (!string.IsNullOrEmpty(m_AssetBundleName))
            {
                m_Bundle = XAssetComponent.Get().LoadBundleAsync(m_AssetBundleName);
                LoadState = ELoadState.LoadAssetBundle;
            }
            else
            {
                LoadSceneAsync();
            }
        }

        internal override void Unload()
        {
            base.Unload();
            m_Request = null;
        }
    }

    public class WebAssetRequest : AssetRequest
    {
        private UnityWebRequest m_Request;

        public override bool IsDone
        {
            get
            {
                if (LoadState == ELoadState.Init)
                    return false;
                if (LoadState == ELoadState.Loaded)
                    return true;

                if (LoadState == ELoadState.LoadAsset)
                {
                    if (m_Request == null || !string.IsNullOrEmpty(m_Request.error))
                        return true;

                    if (m_Request.isDone)
                    {
                        if (AssetType != typeof(Texture2D))
                        {
                            if (AssetType != typeof(TextAsset))
                            {
                                if (AssetType != typeof(AudioClip))
                                    Bytes = m_Request.downloadHandler.data;
                                else
                                    Asset = DownloadHandlerAudioClip.GetContent(m_Request);
                            }
                            else
                            {
                                Text = m_Request.downloadHandler.text;
                            }
                        }
                        else
                        {
                            Asset = DownloadHandlerTexture.GetContent(m_Request);
                        }

                        LoadState = ELoadState.Loaded;
                        return true;
                    }

                    return false;
                }

                return true;
            }
        }

        public override string Error
        {
            get { return m_Request.error; }
        }

        public override float Progress
        {
            get { return m_Request.downloadProgress; }
        }

        internal override void Load()
        {
            if (AssetType == typeof(AudioClip))
            {
                m_Request = UnityWebRequestMultimedia.GetAudioClip(AssetUrl, AudioType.WAV);
            }
            else if (AssetType == typeof(Texture2D))
            {
                m_Request = UnityWebRequestTexture.GetTexture(AssetUrl);
            }
            else
            {
                m_Request = new UnityWebRequest(AssetUrl);
                m_Request.downloadHandler = new DownloadHandlerBuffer();
            }

            m_Request.SendWebRequest();
            LoadState = ELoadState.LoadAsset;
        }

        internal override void Unload()
        {
            if (Asset != null)
            {
                Object.Destroy(Asset);
                Asset = null;
            }

            if (m_Request != null)
                m_Request.Dispose();

            Bytes = null;
            Text = null;
        }
    }

    public class BundleRequest : AssetRequest
    {
        public readonly List<BundleRequest> Dependencies = new List<BundleRequest>();

        public virtual AssetBundle AssetBundle
        {
            get { return Asset as AssetBundle; }
            internal set { Asset = value; }
        }

        internal override void Load()
        {
            //asset = Versions.LoadAssetBundleFromFile(url);
            Asset = AssetBundle.LoadFromFile(AssetUrl);
            //Asset = XAsset.Get().LoadAssetBundleFromFile(Url);

            if (AssetBundle == null)
                Error = AssetUrl + " LoadFromFile failed.";
        }

        internal override void Unload()
        {
            if (AssetBundle == null)
                return;
            AssetBundle.Unload(true);
            AssetBundle = null;
        }
    }

    public class BundleAsyncRequest : BundleRequest
    {
        private AssetBundleCreateRequest m_Request;

        public override AssetBundle AssetBundle
        {
            get
            {
                // fix 
                // 同一帧，先调异步接口，再调用同步接口，同步接口 bundle.assetBundle 报空
                if (m_Request != null && !m_Request.isDone)
                {
                    Asset = m_Request.assetBundle;
                    //Debug.LogError("bundle async request is not done. asset = " + (asset ? asset.name : "null"));
                }
                return base.AssetBundle;
            }
            internal set
            {
                base.AssetBundle = value;
            }
        }

        public override bool IsDone
        {
            get
            {
                if (LoadState == ELoadState.Init)
                    return false;

                if (LoadState == ELoadState.Loaded)
                    return true;

                if (LoadState == ELoadState.LoadAssetBundle && m_Request.isDone)
                {
                    Asset = m_Request.assetBundle;
                    if (Asset == null)
                    {
                        Error = string.Format("unable to load assetBundle:{0}", AssetUrl);
                    }

                    LoadState = ELoadState.Loaded;
                }

                return m_Request == null || m_Request.isDone;
            }
        }

        public override float Progress
        {
            get { return m_Request != null ? m_Request.progress : 0f; }
        }

        internal override void Load()
        {
            //_request = Versions.LoadAssetBundleFromFileAsync(url);
            m_Request = AssetBundle.LoadFromFileAsync(AssetUrl);
            //m_Request = XAsset.Get().LoadAssetBundleFromFileAsync(Url);

            if (m_Request == null)
            {
                Error = AssetUrl + " LoadFromFile failed.";
                return;
            }

            LoadState = ELoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            m_Request = null;
            LoadState = ELoadState.Unload;
            base.Unload();
        }
    }

    public class WebBundleRequest : BundleRequest
    {
        private UnityWebRequest m_Request;

        public override string Error
        {
            get { return m_Request != null ? m_Request.error : null; }
        }

        public override bool IsDone
        {
            get
            {
                if (LoadState == ELoadState.Init)
                    return false;

                if (m_Request == null || LoadState == ELoadState.Loaded)
                    return true;

                if (m_Request.isDone)
                {
                    AssetBundle = DownloadHandlerAssetBundle.GetContent(m_Request);
                    LoadState = ELoadState.Loaded;
                }

                return m_Request.isDone;
            }
        }

        public override float Progress
        {
            get { return m_Request != null ? m_Request.downloadProgress : 0f; }
        }

        internal override void Load()
        {
            m_Request = UnityWebRequestAssetBundle.GetAssetBundle(AssetUrl);
            m_Request.SendWebRequest();
            LoadState = ELoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            if (m_Request != null)
            {
                m_Request.Dispose();
                m_Request = null;
            }

            LoadState = ELoadState.Unload;
            base.Unload();
        }
    }
}