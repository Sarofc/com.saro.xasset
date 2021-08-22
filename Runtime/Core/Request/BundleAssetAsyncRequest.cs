using System;
using UnityEngine;

namespace Saro.XAsset
{
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
}