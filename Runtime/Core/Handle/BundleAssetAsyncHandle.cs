﻿using System;
using UnityEngine;

namespace Saro.XAsset
{
    public class BundleAssetAsyncHandle : BundleAssetHandle
    {
        private AssetBundleRequest m_AssetBundleRequest;

        public BundleAssetAsyncHandle(string bundle)
            : base(bundle)
        {
        }

        public override bool IsDone
        {
            get
            {
                switch (LoadState)
                {
                    case ELoadState.Init:
                        return false;
                    case ELoadState.Loaded:
                        return true;
                    case ELoadState.LoadAssetBundle:
                        {
                            if (Error != null || m_BundleHandle.Error != null)
                                return true;

                            for (int i = 0, max = m_BundleHandle.Dependencies.Count; i < max; i++)
                            {
                                var item = m_BundleHandle.Dependencies[i];
                                if (item.Error != null)
                                    return true;
                            }

                            if (!m_BundleHandle.IsDone)
                                return false;

                            for (int i = 0, max = m_BundleHandle.Dependencies.Count; i < max; i++)
                            {
                                var item = m_BundleHandle.Dependencies[i];
                                if (!item.IsDone)
                                    return false;
                            }

                            if (m_BundleHandle.Bundle == null)
                            {
                                Error = "assetBundle == null";
                                return true;
                            }

                            if (typeof(Component).IsAssignableFrom(AssetType))
                            {
                                m_AssetBundleRequest = m_BundleHandle.Bundle.LoadAssetAsync(AssetUrl, typeof(GameObject));
                            }
                            else
                            {
                                m_AssetBundleRequest = m_BundleHandle.Bundle.LoadAssetAsync(AssetUrl, AssetType);
                            }

                            LoadState = ELoadState.LoadAsset;
                            break;
                        }
                    case ELoadState.Unload:
                        return false;
                    case ELoadState.LoadAsset:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (!m_AssetBundleRequest.isDone)
                    return false;

                if (typeof(Component).IsAssignableFrom(AssetType))
                {
                    var gameObject = m_AssetBundleRequest.asset as GameObject;
                    Asset = gameObject.GetComponent(AssetType);
                }
                else
                {
                    Asset = m_AssetBundleRequest.asset;
                }

                LoadState = ELoadState.Loaded;
                return true;
            }
        }

        public override float Progress
        {
            get
            {
                var bundleProgress = m_BundleHandle.Progress;
                if (m_BundleHandle.Dependencies.Count <= 0)
                    return bundleProgress * 0.3f + (m_AssetBundleRequest != null ? m_AssetBundleRequest.progress * 0.7f : 0);
                for (int i = 0, max = m_BundleHandle.Dependencies.Count; i < max; i++)
                {
                    var item = m_BundleHandle.Dependencies[i];
                    bundleProgress += item.Progress;
                }

                return bundleProgress / (m_BundleHandle.Dependencies.Count + 1) * 0.3f +
                       (m_AssetBundleRequest != null ? m_AssetBundleRequest.progress * 0.7f : 0);
            }
        }

        internal override void Load()
        {
            m_BundleHandle = XAssetManager.Current.LoadBundleAsync(m_AssetBundleName);
            LoadState = ELoadState.LoadAssetBundle;
        }

        internal override void Unload()
        {
            base.Unload();
            m_AssetBundleRequest = null;
            LoadState = ELoadState.Unload;
        }
    }
}