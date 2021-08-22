using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saro.XAsset
{
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
}