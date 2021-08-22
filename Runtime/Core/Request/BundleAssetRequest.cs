namespace Saro.XAsset
{
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
}