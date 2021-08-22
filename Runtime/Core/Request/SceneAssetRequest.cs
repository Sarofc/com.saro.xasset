using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saro.XAsset
{
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
}