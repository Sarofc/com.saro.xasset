using Saro.Core;
using System;
using UnityEngine;

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
            Load_Editor();

            if (Asset == null)
            {
                Error = "base class 'AssetRequest' load error! url: " + AssetUrl;
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void Load_Editor()
        {
#if UNITY_EDITOR
            if (!XAssetComponent.s_RuntimeMode && XAssetComponent.s_EditorLoader != null)
                Asset = XAssetComponent.s_EditorLoader(AssetUrl, AssetType);
#endif
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
}