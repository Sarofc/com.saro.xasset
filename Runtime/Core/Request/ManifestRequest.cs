using Saro.Core;
using System.IO;

namespace Saro.XAsset
{
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
}