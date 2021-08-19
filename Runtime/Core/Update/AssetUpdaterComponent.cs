using Saro.IO;
using Saro.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Saro.XAsset.Update
{
    /*
     * TODO
     * 
     * 使用FTask重构
     * 
     * 使用EventSystem 將ui解耦
     * 
     */
    public sealed class AssetUpdaterComponent : FEntity, IUpdater, INetworkMonitorListener
    {
        private enum EStep
        {
            Wait,
            Copy,
            Coping,
            Versions,
            Prepared,
            Download,
        }

        public IUpdater Listener { get; set; }

        // TODO 资源地址应该读取配置来获取,且配置是可热更的
        public string DlcUrl { get; set; }

        private EStep m_Step;
        private DownloaderComponent m_Downloader;
        private NetworkMonitorComponent m_NetworkMonitor;
        private string m_DlcPath;
        private string m_BasePath;

        private bool m_NetReachabilityChanged;

        public void Awake()
        {
            m_Downloader = AddComponent<DownloaderComponent>();

            m_NetworkMonitor = AddComponent<NetworkMonitorComponent>();
            m_NetworkMonitor.Listener = this;

            m_DlcPath = GetDlcPath();
            m_BasePath = GetBasePath();

            m_Step = EStep.Wait;

            Main.onApplicationFocus -= OnApplicationFocus;
            Main.onApplicationFocus += OnApplicationFocus;
        }

        private void INFO(string msg)
        {
            Log.INFO("AssetUpdate", msg);
        }

        private void ERROR(string msg)
        {
            Log.ERROR("AssetUpdate", msg);
        }

        private void WARN(string msg)
        {
            Log.WARN("AssetUpdate", msg);
        }

        public async FTask StartUpdate()
        {
            INFO("Start Update...");

            var localVer = await InitVersionManifestAsync();
            var remoteVar = await RequestRemoteVersioManifestAsync();
            var diff = await GetDiffAssetInfosAsync(localVer, remoteVar);
            await DownloadAsync(diff);
            OverrideLocalVersionManifestUseTmp();

            INFO("Update Finish...");
        }

        private async FTask<VersionManifest> InitVersionManifestAsync()
        {
            var tcs = FTask<VersionManifest>.Create();
            var localVerPath = GetLocalVersionManifestPath();
            if (File.Exists(localVerPath))
            {
                var localVer = VersionManifest.LoadVersionManifest(localVerPath);
                tcs.SetResult(localVer);
            }
            else
            {
                var baseVerPath = GetBaseVersionManifestPath();
                var request = UnityWebRequest.Get(baseVerPath);
                request.downloadHandler = new DownloadHandlerFile(localVerPath);
                var op = request.SendWebRequest();
                op.completed += _op =>
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        ERROR("Base VersionManifest not found.\n" + request.error);
                        tcs.SetResult(null);
                    }
                    else
                    {
                        var localVer = VersionManifest.LoadVersionManifest(localVerPath);
                        tcs.SetResult(localVer);
                    }
                };
            }

            return await tcs;
        }

        private async FTask<VersionManifest> RequestRemoteVersioManifestAsync(FCancellationToken cancellationToken = null)
        {
            var tcs = FTask<VersionManifest>.Create();
            var tmpVerPath = GetTmpLocalVersionManifestPath();
            var request = UnityWebRequest.Get(GetDownloadURL(VersionManifest.k_VersionManifestFileName));
            request.downloadHandler = new DownloadHandlerFile(tmpVerPath);
            var op = request.SendWebRequest();
            op.completed += _op =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ERROR(request.error);
                    tcs.SetResult(null);
                }
                else
                {
                    var remoteVer = VersionManifest.LoadVersionManifest(tmpVerPath);
                    tcs.SetResult(remoteVer);
                }
            };

            void CancelAction()
            {
                if (!tcs.IsCompleted)
                    tcs.SetResult(null);
            }

            VersionManifest ret;
            try
            {
                cancellationToken?.Add(CancelAction);
                ret = await tcs;
            }
            finally
            {
                cancellationToken?.Remove(CancelAction);
            }
            return ret;
        }

        private async FTask<List<AssetInfo>> GetDiffAssetInfosAsync(VersionManifest localVer, VersionManifest remoteVer)
        {
            await FTask.CompletedTask;

            if (remoteVer == null) return null;

            if (localVer == null) return remoteVer.assets.Values.ToList();

            return localVer.Diff(remoteVer);
        }

        private async FTask DownloadAsync(IList<AssetInfo> assets)
        {
            if (assets == null || assets.Count == 0)
            {
                INFO("Nothing to download");
                return;
            }

            INFO($"download list:\n{string.Join("\n", assets)}");

            var maxDownload = (int)Download.s_MaxDownloads;
            var tasks = new List<FTask>();
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                var downloadObj = Download.DownloadAsync(GetDownloadURL(asset.key), GetDlcPath() + asset.key, asset.length);
                tasks.Add(downloadObj.task);

                if (tasks.Count == maxDownload || i == assets.Count - 1)
                {
                    await FTaskUtility.WaitAll(tasks);

                    ERROR($"下载完毕 i: {i}");

                    // TODO 可能解压
                    // TODO threading
                    //foreach (var item in tasks)
                    //{
                    //    var srcFilepath = item.GetResult().Info.savePath;
                    //    var dstFilePath = GetDlcPath() + Path.GetFileName(srcFilepath);

                    //    if (!File.Exists(dstFilePath))
                    //        File.Move(srcFilepath, dstFilePath);
                    //    else
                    //        File.Delete(srcFilepath);
                    //}

                    //ERROR($"移动文件 i: {i}");

                    tasks.Clear();
                }
            }
        }

        public void Destroy()
        {
            //UI.UIDialogue.Dispose();

            Main.onApplicationFocus -= OnApplicationFocus;
        }

        public void OnApplicationFocus(bool hasFocus)
        {
            if (m_NetReachabilityChanged || m_Step == EStep.Wait) return;

            if (hasFocus)
            {

            }
            else
            {
                if (m_Step == EStep.Download)
                {
                    //m_Downloader.Stop();
                }
            }
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #region Path

        private string GetDownloadURL(string fileName)
        {
            return string.Format("{0}/DLC/{1}/{2}", DlcUrl, XAssetComponent.GetCurrentPlatformAssetBundleFolderName(), fileName);
        }

        private string GetDownloadCachePath()
        {
            return string.Format("{0}/DLC/DownloadCache/", Application.persistentDataPath);
        }

        private static string GetDlcPath()
        {
            return string.Format("{0}/DLC/", Application.persistentDataPath);
        }

        private static string GetBasePath()
        {
            return GetStreamingAssetsPath() + "/" + XAssetComponent.GetCurrentPlatformAssetBundleFolderName() + "/";
        }

        private static string GetStreamingAssetsPath()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return Application.streamingAssetsPath;
            }

            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                return "file:///" + Application.streamingAssetsPath;
            }

            return "file://" + Application.streamingAssetsPath;
        }


        private string GetBaseVersionManifestPath()
        {
            return m_BasePath + VersionManifest.k_VersionManifestFileName;
        }

        private string GetTmpLocalVersionManifestPath()
        {
            return m_DlcPath + VersionManifest.k_VersionManifestFileName + ".tmp";
        }

        private string GetLocalVersionManifestPath()
        {
            return m_DlcPath + VersionManifest.k_VersionManifestFileName;
        }

        private bool OverrideLocalVersionManifestUseTmp()
        {
            var tmpVerPath = GetTmpLocalVersionManifestPath();
            var verPath = GetLocalVersionManifestPath();

            try
            {
                if (File.Exists(tmpVerPath))
                {
                    File.Copy(tmpVerPath, verPath, true);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            return false;
        }

        #endregion

        #region Interface

        public void OnClear()
        {
            OnMessage("数据清除完毕");
            OnProgress(0);

            m_Downloader.Destroy();
            m_Step = EStep.Wait;
            m_NetReachabilityChanged = false;

            XAssetComponent.Get().Clear();

            if (Listener != null)
            {
                Listener.OnClear();
            }

            if (Directory.Exists(m_DlcPath))
            {
                Directory.Delete(m_DlcPath, true);
            }
        }

        public void OnMessage(string msg)
        {
            if (Listener != null)
            {
                Listener.OnMessage(msg);
            }
        }

        public void OnProgress(float progress)
        {
            if (Listener != null)
            {
                Listener.OnProgress(progress);
            }
        }

        public void OnStart()
        {
            if (Listener != null)
            {
                Listener.OnStart();
            }
        }

        public void OnVersion(string ver)
        {
            if (Listener != null)
            {
                Listener.OnVersion(ver);
            }
        }

        void INetworkMonitorListener.OnReachablityChanged(NetworkReachability reachability)
        {

        }

        #endregion
    }


    [FObjectSystem]
    internal class AssetUpdaterComponentAwakeSystem : AwakeSystem<AssetUpdaterComponent>
    {
        public override void Awake(AssetUpdaterComponent self)
        {
            self.Awake();
        }
    }

    //[ObjectSystem]
    //internal class AssetUpdaterComponentUpdateSystem : UpdateSystem<AssetUpdaterComponent>
    //{
    //    public override void Update(AssetUpdaterComponent self)
    //    {
    //        self.Update();
    //    }
    //}

    [FObjectSystem]
    internal class AssetUpdaterComponentDestroySystem : DestroySystem<AssetUpdaterComponent>
    {
        public override void Destroy(AssetUpdaterComponent self)
        {
            self.Destroy();
        }
    }

}