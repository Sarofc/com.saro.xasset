using UnityEngine;
using UnityEngine.Networking;

namespace Saro.XAsset
{
    using Object = UnityEngine.Object;

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
}