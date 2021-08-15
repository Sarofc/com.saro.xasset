//using Saro.IO;
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using UnityEngine;
//using UnityEngine.Networking;

//namespace Saro.XAsset.Update
//{
//    /*
//     * TODO
//     * 
//     * 使用FTask重构
//     * 
//     * 使用EventSystem 將ui解耦
//     * 
//     */
//    public sealed class ___AssetUpdaterComponent : FEntity, IUpdater, INetworkMonitorListener
//    {
//        private enum EStep
//        {
//            Wait,
//            Copy,
//            Coping,
//            Versions,
//            Prepared,
//            Download,
//        }

//        public IUpdater Listener { get; set; }

//        // TODO 资源地址应该读取配置来获取,且配置是可热更的
//        [SerializeField] private string m_BaseURL = "http://127.0.0.1:7888/DLC/";

//        private EStep m_Step;
//        private DownloaderComponent m_Downloader;
//        private NetworkMonitorComponent m_NetworkMonitor;
//        private string m_DlcPath;
//        private string m_BasePath;

//        private bool m_NetReachabilityChanged;

//        private IEnumerator m_Checking;

//        public void Awake()
//        {
//            m_Downloader = AddComponent<DownloaderComponent>();
//            m_Downloader.onUpdate = OnUpdate;
//            m_Downloader.onFinished = OnComplete;

//            m_NetworkMonitor = AddComponent<NetworkMonitorComponent>();
//            m_NetworkMonitor.Listener = this;

//            m_DlcPath = GetDlcPath();
//            m_BasePath = GetBasePath();

//            m_Step = EStep.Wait;

//            Main.onApplicationFocus -= OnApplicationFocus;
//            Main.onApplicationFocus += OnApplicationFocus;
//        }

//        public void Destroy()
//        {
//            //UI.UIDialogue.Dispose();

//            Main.onApplicationFocus -= OnApplicationFocus;
//        }

//        public void OnApplicationFocus(bool hasFocus)
//        {
//            if (m_NetReachabilityChanged || m_Step == EStep.Wait) return;

//            if (hasFocus)
//            {
//                //UIDialogue.CloseAll();
//                if (m_Step == EStep.Download)
//                {
//                    m_Downloader.Restart();
//                }
//                else
//                {
//                    StartUpdate();
//                }
//            }
//            else
//            {
//                if (m_Step == EStep.Download)
//                {
//                    m_Downloader.Stop();
//                }
//            }
//        }

//        public void StartUpdate()
//        {
//            OnStart();

//            if (m_Checking != null)
//            {
//                Main.CancelCoroutine(m_Checking);
//            }

//            m_Checking = Checking();
//            Main.RunCoroutine(m_Checking);
//        }

//        private IEnumerator Checking()
//        {
//            if (!Directory.Exists(m_DlcPath))
//            {
//                Directory.CreateDirectory(m_DlcPath);
//            }

//            if (m_Step == EStep.Wait)
//            {
//                //yield return RequestVFS();
//                m_Step = EStep.Copy;

//                Debug.LogError("Step.Copy");
//            }

//            if (m_Step == EStep.Copy)
//            {
//                yield return RequestCopy();
//            }

//            if (m_Step == EStep.Coping)
//            {
//                var tmpLocalVersionFile = GetTmpLocalVersionListPath();
//                var versionList = VersionList.LoadVersionList(tmpLocalVersionFile);
//                yield return UpdateCopy(versionList);
//                m_Step = EStep.Versions;

//                Debug.LogError("Step.Versions");
//            }

//            if (m_Step == EStep.Versions)
//            {
//                yield return RequestVersionList();
//            }

//            if (m_Step == EStep.Prepared)
//            {
//                OnMessage("正在获取下载信息...");
//                var totalSize = m_Downloader.Size;

//                if (totalSize > 0)
//                {
//                    var tips = string.Format("发现内容更新，总计需要下载 {0} 内容", DownloaderComponent.GetDisplaySize(totalSize));
//                    //var mb = UI.UIDialogue.Show("提示", tips, "下载", "退出");
//                    //yield return mb;
//                    //if (mb.isOk)
//                    {
//                        m_Downloader.StartDownload();
//                        m_Step = EStep.Download;

//                        Debug.LogError("Step.Download");
//                    }
//                    //else
//                    //{
//                    //    Quit();
//                    //}
//                }
//                else
//                {
//                    OnComplete();
//                }
//            }
//        }

//        private IEnumerator RequestCopy()
//        {
//            var localVersionListPath = GetLocalVersionListPath();
//            var localVersion = VersionList.LoadVersionOnly(localVersionListPath);

//            var baseVersionListPath = GetBaseVersionListPath();
//            var request = UnityWebRequest.Get(baseVersionListPath);
//            yield return request.SendWebRequest();

//            if (string.IsNullOrEmpty(request.error))
//            {
//                System.Version baseLocalVersion = null;

//                using (var ms = new MemoryStream(request.downloadHandler.data))
//                {
//                    using (var br = new BinaryReader(ms))
//                    {
//                        baseLocalVersion = VersionList.LoadVersionOnly(br);
//                    }
//                }

//                if (baseLocalVersion != null)
//                {
//                    if (localVersion == null || baseLocalVersion > localVersion)
//                    {
//                        var tmpLocalVersionFilePath = GetTmpLocalVersionListPath();
//                        File.WriteAllBytes(tmpLocalVersionFilePath, request.downloadHandler.data);

//                        m_Step = EStep.Coping;

//                        Debug.LogError("Step.Coping");
//                    }
//                    else
//                    {
//                        m_Step = EStep.Versions;

//                        Debug.LogError("Step.Versions");
//                    }
//                }
//                else
//                {
//                    m_Step = EStep.Versions;

//                    Debug.LogError("Step.Versions");
//                }
//            }
//            else
//            {
//                m_Step = EStep.Versions;

//                Debug.LogError("Step.Versions");
//            }
//            request.Dispose();
//        }

//        private IEnumerator RequestVersionList()
//        {
//            OnMessage("正在获取版本信息...");
//            if (Application.internetReachability == NetworkReachability.NotReachable)
//            {
//                //var mb = UIDialogue.Show("提示", "请检查网络连接状态", "重试", "退出");
//                //yield return mb;
//                //if (mb.isOk)
//                {
//                    StartUpdate();
//                }
//                //else
//                //{
//                //    Quit();
//                //}
//                yield break;
//            }

//            var tempVersionFilePath = GetTmpLocalVersionListPath();
//            var request = UnityWebRequest.Get(GetDownloadURL(VersionList.k_VersionFileName));
//            request.downloadHandler = new DownloadHandlerFile(tempVersionFilePath);
//            yield return request.SendWebRequest();
//            var error = request.error;
//            request.Dispose();
//            if (!string.IsNullOrEmpty(error))
//            {
//                Debug.LogError($"获取服务器版本失败: \n{error}");
//                //var mb = UI.UIDialogue.Show("提示", $"获取服务器版本失败: \n{error}", "重试");
//                //yield return mb;
//                //if (mb.isOk)
//                {
//                    StartUpdate();
//                }
//                //else
//                //{
//                //    Quit();
//                //}
//                yield break;
//            }

//            try
//            {
//                var remoteVersionList = VersionList.LoadVersionList(tempVersionFilePath);
//                var localVersionList = VersionList.LoadVersionList(GetLocalVersionListPath());

//                if (PrepareDownloads(remoteVersionList, localVersionList))
//                {
//                    m_Step = EStep.Prepared;

//                    Debug.LogError("Step.Prepared");
//                }
//                else
//                {
//                    OnComplete();
//                }
//            }
//            catch (Exception e)
//            {
//                Debug.LogException(e);

//                Debug.LogError("版本文件加载失败");
//                //UIDialogue.Show("提示", "版本文件加载失败", "重试", "退出").onComplete +=
//                //     delegate (UI.UIDialogue.EventId id)
//                //     {
//                //         if (id == UI.UIDialogue.EventId.Ok)
//                //         {
//                //             StartUpdate();
//                //         }
//                //         else
//                //         {
//                //             Quit();
//                //         }
//                //     };
//            }
//        }

//        private IEnumerator UpdateCopy(VersionList versionList)
//        {
//            OnMessage("开始复制文件...");

//            var versionAssetInfos = versionList.versionAssetInfos;

//            var fileToCopy = new HashSet<string>();
//            foreach (var item in versionAssetInfos)
//            {
//                fileToCopy.Add(item.Value.file);
//            }

//            var index = 0;
//            foreach (var file in fileToCopy)
//            {
//                Debug.LogError(file);

//                var request = UnityWebRequest.Get(m_BasePath + file);
//                request.downloadHandler = new DownloadHandlerFile(m_DlcPath + file);
//                yield return request.SendWebRequest();
//                request.Dispose();
//                OnMessage(string.Format("正在复制文件：{0}/{1}", index, fileToCopy.Count));
//                OnProgress(index * 1f / fileToCopy.Count);
//                index++;
//            }

//            OverrideLocalVersionListUseTmp();
//        }

//        public void ClearAssets()
//        {
//            //UIDialogue.Show("提示", "清除数据后所有数据需要重新下载，请确认！", "清除").onComplete += id =>
//            //{
//            //    if (id != UIDialogue.EventId.Ok)
//            //        return;
//            //    OnClear();
//            //};
//        }

//        private void OnUpdate(long progress, long size, float speed)
//        {
//            OnMessage(string.Format("下载中...\t\t{0}/{1}\t\t速度: {2}",
//                DownloaderComponent.GetDisplaySize(progress),
//                DownloaderComponent.GetDisplaySize(size),
//                DownloaderComponent.GetDisplaySpeed(speed)));

//            OnProgress(progress * 1f / size);
//        }

//        private async void OnComplete()
//        {
//            m_Step = EStep.Wait;

//            Debug.LogError("Step.Wait");

//            var downloads = m_Downloader.Downloads;
//            if (downloads.Count > 0)
//            {
//                //Debug.LogError("合并文件...");
//                // 合并文件
//                OnMessage("合并文件...");

//                var datPath = GetDlcPath() + VersionList.k_DatFileName;
//                var access = EVFileSystemAccess.ReadWrite;

//                CommonVFileSystemStream stream = null;
//                VFileSystem vfs = null;

//                try
//                {
//                    // create or load vfs
//                    // TODO 可以搞成跟 FileStream 一样的API,可以直接OpenOrCreate
//                    if (!File.Exists(datPath))
//                    {
//                        stream = new CommonVFileSystemStream(datPath, access, true);
//                        vfs = VFileSystem.Create(datPath, access, stream, 1024, 1024 * 32);
//                    }
//                    else
//                    {
//                        stream = new CommonVFileSystemStream(datPath, access, false);
//                        vfs = VFileSystem.Load(datPath, access, stream);
//                    }

//                    // wirte file
//                    for (int index = 0; index < downloads.Count; index++)
//                    {
//                        Download download = downloads[index];
//                        var fileName = download.Name;
//                        var filePath = download.SavePath;

//                        //if (vfs.HasFile(fileName))
//                        //{
//                        //    vfs.DeleteFile(fileName);
//                        //}

//                        if (!vfs.WriteFile(fileName, filePath))
//                        { }

//                        OnMessage($"正在合并文件: {index}/{downloads.Count}");
//                        OnProgress((float)index / downloads.Count);
//                    }

//                    // delete tmp files
//                    //for (int i = 0; i < downloads.Count; i++)
//                    //{
//                    //    File.Delete(downloads[i].SavePath);
//                    //}

//                    m_Downloader.Destroy();
//                }
//                catch (IOException e)
//                {
//                    Debug.LogException(e);
//                }
//                catch (Exception e)
//                {
//                    Debug.LogException(e);
//                }
//                finally
//                {
//                    if (stream != null) stream.Dispose();
//                    if (vfs != null) vfs.Dispose();
//                }

//                //Debug.LogError("覆盖版本文件...");
//                OverrideLocalVersionListUseTmp();
//            }

//            var localVersionListPath = GetLocalVersionListPath();
//            var version = VersionList.LoadVersionOnly(localVersionListPath);
//            if (version != null)
//            {
//                OnVersion(version.ToString());
//            }
//            else
//            {
//                throw new Exception("Local VersionList is invalid.");
//            }

//            OnProgress(1);
//            OnMessage("更新完成");


//            // 重新初始化 XAssest
//            OnMessage("初始化资源管理器...");
//            OnProgress(0);

//            var result = await XAssetComponent.Get().InitializeAsync();

//            if (result)
//            {
//                OnMessage("初始化成功!");
//                OnProgress(1);

//                var sceneRequest = XAssetComponent.Get().LoadSceneCallback("Assets/Res/Scene/level-1.unity");
//                sceneRequest.Completed += _r =>
//                {
//                    //Debug.LogError("加载成功: " + _r.Asset.name);
//                };
//            }
//            else
//            {
//                Debug.LogError("初始化异常错误：");
//                //UIDialogue.Show("提示", "初始化异常错误："/* + manifestRequest.Error*/ + "请联系技术支持").onComplete += _ =>
//                //{
//                //    Quit();
//                //};
//            }
//        }

//        private void Quit()
//        {
//#if UNITY_EDITOR
//            UnityEditor.EditorApplication.isPlaying = false;
//#else
//            Application.Quit();
//#endif
//        }

//        private bool PrepareDownloads(VersionList remoteVersionList, VersionList localVersionList)
//        {
//            if (!remoteVersionList.IsValid()) throw new Exception("RemoteVersionList is invalid.");

//            bool hasNew = false;
//            var remoteAssetInfos = remoteVersionList.versionAssetInfos;
//            foreach (var item in remoteAssetInfos)
//            {
//                if (localVersionList != null && localVersionList.versionAssetInfos.TryGetValue(item.Key, out var assetInfo))
//                {
//                    if (assetInfo.IsNew(item.Value))
//                    {
//                        if (!hasNew) hasNew = true;
//                        AddDownload(item.Value);
//                    }
//                }
//                else
//                {
//                    if (!hasNew) hasNew = true;
//                    AddDownload(item.Value);
//                }
//            }

//            //Debug.LogError(string.Join("\n", m_Downloader.Downloads));

//            return hasNew;
//        }

//        private void AddDownload(VersionAssetInfo item)
//        {
//            m_Downloader.AddDownload(GetDownloadURL(item.pack), item.file, m_DlcPath + item.file, item.hash, item.offset, item.length);
//        }

//        #region Path

//        private string GetDownloadURL(string fileName)
//        {
//            return string.Format("{0}{1}/{2}", m_BaseURL, XAssetComponent.GetCurrentPlatformAssetBundleFolderName(), fileName);
//        }

//        private static string GetDlcPath()
//        {
//            return string.Format("{0}/DLC/", Application.persistentDataPath);
//        }

//        private static string GetBasePath()
//        {
//            return GetStreamingAssetsPath() + "/" + XAssetComponent.GetCurrentPlatformAssetBundleFolderName() + "/";
//        }

//        private static string GetStreamingAssetsPath()
//        {
//            if (Application.platform == RuntimePlatform.Android)
//            {
//                return Application.streamingAssetsPath;
//            }

//            if (Application.platform == RuntimePlatform.WindowsPlayer ||
//                Application.platform == RuntimePlatform.WindowsEditor)
//            {
//                return "file:///" + Application.streamingAssetsPath;
//            }

//            return "file://" + Application.streamingAssetsPath;
//        }


//        private string GetBaseVersionListPath()
//        {
//            return m_BasePath + VersionList.k_VersionFileName;
//        }

//        private string GetTmpLocalVersionListPath()
//        {
//            return m_DlcPath + VersionList.k_VersionFileName + ".tmp";
//        }

//        private string GetLocalVersionListPath()
//        {
//            return m_DlcPath + VersionList.k_VersionFileName;
//        }

//        private bool OverrideLocalVersionListUseTmp()
//        {
//            var tmp = GetTmpLocalVersionListPath();
//            var dest = GetLocalVersionListPath();

//            try
//            {
//                if (File.Exists(tmp))
//                {
//                    File.Copy(tmp, dest, true);
//                    return true;
//                }
//            }
//            catch (Exception e)
//            {
//                Debug.LogError(e);
//            }

//            return false;
//        }

//        #endregion

//        #region Interface

//        public void OnClear()
//        {
//            OnMessage("数据清除完毕");
//            OnProgress(0);

//            m_Downloader.Destroy();
//            m_Step = EStep.Wait;
//            m_NetReachabilityChanged = false;

//            XAssetComponent.Get().Clear();

//            if (Listener != null)
//            {
//                Listener.OnClear();
//            }

//            if (Directory.Exists(m_DlcPath))
//            {
//                Directory.Delete(m_DlcPath, true);
//            }
//        }

//        public void OnMessage(string msg)
//        {
//            if (Listener != null)
//            {
//                Listener.OnMessage(msg);
//            }
//        }

//        public void OnProgress(float progress)
//        {
//            if (Listener != null)
//            {
//                Listener.OnProgress(progress);
//            }
//        }

//        public void OnStart()
//        {
//            if (Listener != null)
//            {
//                Listener.OnStart();
//            }
//        }

//        public void OnVersion(string ver)
//        {
//            if (Listener != null)
//            {
//                Listener.OnVersion(ver);
//            }
//        }

//        void INetworkMonitorListener.OnReachablityChanged(NetworkReachability reachability)
//        {
//            if (m_Step == EStep.Wait) return;

//            m_NetReachabilityChanged = true;
//            if (m_Step == EStep.Download)
//            {
//                m_Downloader.Stop();
//            }

//            if (reachability == NetworkReachability.NotReachable)
//            {
//                Debug.LogError("找不到网络，请确保手机已经联网");
//                //UI.UIDialogue.Show("提示！", "找不到网络，请确保手机已经联网", "确定", "退出").onComplete += delegate (UIDialogue.EventId id)
//                //{
//                //    if (id == UIDialogue.EventId.Ok)
//                //    {
//                //        if (m_Step == EStep.Download)
//                //        {
//                //            m_Downloader.Restart();
//                //        }
//                //        else
//                //        {
//                //            StartUpdate();
//                //        }
//                //        m_NetReachabilityChanged = false;
//                //    }
//                //    else
//                //    {
//                //        Quit();
//                //    }
//                //};
//            }
//            else
//            {
//                if (m_Step == EStep.Download)
//                {
//                    m_Downloader.Restart();
//                }
//                else
//                {
//                    StartUpdate();
//                }

//                m_NetReachabilityChanged = false;
//                //UIDialogue.CloseAll();
//            }
//        }

//        #endregion
//    }


//    [FObjectSystem]
//    internal class ___AssetUpdaterComponentAwakeSystem : AwakeSystem<___AssetUpdaterComponent>
//    {
//        public override void Awake(___AssetUpdaterComponent self)
//        {
//            self.Awake();
//        }
//    }

//    //[ObjectSystem]
//    //internal class ___AssetUpdaterComponentUpdateSystem : UpdateSystem<___AssetUpdaterComponent>
//    //{
//    //    public override void Update(___AssetUpdaterComponent self)
//    //    {
//    //        self.Update();
//    //    }
//    //}

//    [FObjectSystem]
//    internal class ___AssetUpdaterComponentDestroySystem : DestroySystem<___AssetUpdaterComponent>
//    {
//        public override void Destroy(___AssetUpdaterComponent self)
//        {
//            self.Destroy();
//        }
//    }

//}