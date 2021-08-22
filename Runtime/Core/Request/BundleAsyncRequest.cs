using UnityEngine;

namespace Saro.XAsset
{
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
}