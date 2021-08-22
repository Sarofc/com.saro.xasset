using UnityEngine.Networking;

namespace Saro.XAsset
{
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