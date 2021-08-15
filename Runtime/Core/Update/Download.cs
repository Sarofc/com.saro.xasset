using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Saro.Tasks;

namespace Saro.XAsset.Update
{
    public enum EDownloadStatus
    {
        Wait,
        Progressing,
        Success,
        Failed
    }

    public class DownloadInfo
    {
        public uint crc;
        public string savePath;
        public long size;
        public string url;
    }

    // TODO
    // 下载限速
    public class Download
    {
        #region Static

        public static uint s_MaxDownloads = 10;
        public static uint s_ReadBufferSize = 1042 * 4;
        public static readonly List<Download> s_Prepared = new List<Download>();
        public static readonly List<Download> s_Progressing = new List<Download>();
        public static readonly Dictionary<string, Download> s_Cache = new Dictionary<string, Download>();
        private static long s_LastSampleTime;
        private static long s_LastTotalDownloadedBytes;
        public static bool Working => s_Progressing.Count > 0;

        public static long TotalDownloadedBytes
        {
            get
            {
                var value = 0L;
                foreach (var item in s_Cache) value += item.Value.DownloadedBytes;

                return value;
            }
        }

        public static long TotalSize
        {
            get
            {
                var value = 0L;
                foreach (var item in s_Cache) value += item.Value.Info.size;

                return value;
            }
        }

        public static long TotalBandwidth { get; private set; }



        private static readonly double[] k_ByteUnits =
        {
            1073741824.0, 1048576.0, 1024.0, 1
        };

        private static readonly string[] k_ByteUnitsNames =
        {
            "GB", "MB", "KB", "B"
        };

        public static string FormatBytes(long bytes)
        {
            var size = "0 B";
            if (bytes == 0) return size;

            for (var index = 0; index < k_ByteUnits.Length; index++)
            {
                var unit = k_ByteUnits[index];
                if (bytes >= unit)
                {
                    size = $"{bytes / unit:##.##} {k_ByteUnitsNames[index]}";
                    break;
                }
            }

            return size;
        }

        public static void ClearAllDownloads()
        {
            foreach (var download in s_Progressing) download.Cancel();

            s_Prepared.Clear();
            s_Progressing.Clear();
            s_Cache.Clear();
        }

        // TODO api 需要调整，download对象应该调用时就返回的
        public static (FTask task, Download download) DownloadAsync(string url, string savePath, long size = 0, uint crc = 0)
        {
            var tcs = FTask.Create();

            Action<Download> completed = (download) =>
            {
                tcs.SetResult();
            };

            var download = DownloadAsync(url, savePath, completed, size, crc);

            return (tcs, download);
        }


        public static Download DownloadAsync(string url, string savePath, Action<Download> completed = null,
            long size = 0, uint crc = 0)
        {
            return DownloadAsync(new DownloadInfo
            {
                url = url,
                savePath = savePath,
                crc = crc,
                size = size
            }, completed);
        }

        public static (FTask task, Download download) DownloadAsync(DownloadInfo info)
        {
            var tcs = FTask.Create();

            Action<Download> completed = (download) =>
            {
                tcs.SetResult();
            };

            var download = DownloadAsync(info, completed);

            return (tcs, download);
        }

        public static Download DownloadAsync(DownloadInfo info, Action<Download> completed = null)
        {
            Download download;
            if (!s_Cache.TryGetValue(info.url, out download))
            {
                download = new Download
                {
                    Info = info
                };
                s_Prepared.Add(download);
                s_Cache.Add(info.url, download);
            }
            else
            {
                WARN($"Download url { info.url} already exist.");
            }

            if (completed != null) download.Completed += completed;

            return download;
        }

        public static void UpdateAll()
        {
            if (s_Prepared.Count > 0)
                for (var index = 0; index < Math.Min(s_Prepared.Count, s_MaxDownloads - s_Progressing.Count); index++)
                {
                    var download = s_Prepared[index];
                    s_Prepared.RemoveAt(index);
                    index--;
                    s_Progressing.Add(download);
                    download.Start();
                }

            if (s_Progressing.Count > 0)
            {
                for (var index = 0; index < s_Progressing.Count; index++)
                {
                    var download = s_Progressing[index];
                    if (download.Updated != null) download.Updated(download);

                    if (download.IsDone)
                    {
                        if (download.Status == EDownloadStatus.Failed)
                            ERROR($"Unable to download {download.Info.url} with error {download.error}");
                        else
                            INFO($"Success to download {download.Info.url}");

                        s_Progressing.RemoveAt(index);
                        index--;
                        download.Complete();
                    }
                }

                // TODO test
                if (DateTime.Now.Ticks - s_LastSampleTime >= 10000000)
                {
                    TotalBandwidth = TotalDownloadedBytes - s_LastTotalDownloadedBytes;
                    s_LastTotalDownloadedBytes = TotalDownloadedBytes;
                    s_LastSampleTime = DateTime.Now.Ticks;
                }
            }
            else
            {
                if (s_Cache.Count <= 0) return;

                s_Cache.Clear();
                s_LastTotalDownloadedBytes = 0;
                s_LastSampleTime = DateTime.Now.Ticks;
            }
        }

        #endregion

        #region Instance

        private readonly byte[] m_ReadBuffer = new byte[s_ReadBufferSize];
        private FileStream m_Writer;
        //private Thread m_Thread;


        private Download()
        {
            Status = EDownloadStatus.Wait;
            DownloadedBytes = 0;
        }

        public DownloadInfo Info { get; private set; }
        public EDownloadStatus Status { get; private set; }
        public string error { get; private set; }
        public event Action<Download> Completed;
        public event Action<Download> Updated;

        public bool IsDone => Status == EDownloadStatus.Failed || Status == EDownloadStatus.Success;
        public float Progress => DownloadedBytes * 1f / Info.size;
        public long DownloadedBytes { get; private set; }

        /// <summary>
        /// 使用切片下载
        /// </summary>
        public bool UseSegment => RangeFrom != 0L && RangeTo != 0L;

        public long RangeFrom { get; private set; } = 0L;
        public long RangeTo { get; private set; } = 0L;


        public void Retry()
        {
            Status = EDownloadStatus.Wait;
            Start();
        }

        public void UnPause()
        {
            Retry();
        }

        public void Pause()
        {
            Status = EDownloadStatus.Wait;
        }

        public void Cancel()
        {
            error = "User Cancel.";
            Status = EDownloadStatus.Failed;
        }

        private void Complete()
        {
            if (Completed != null)
            {
                Completed.Invoke(this);
                Completed = null;
            }
        }

        private void Run()
        {
            try
            {
                Downloading();
                CloseWrite();
                if (Status == EDownloadStatus.Failed) return;

                if (DownloadedBytes != Info.size)
                {
                    error = $"Download lenght {DownloadedBytes} mismatch to {Info.size}";
                    Status = EDownloadStatus.Failed;
                    return;
                }

                if (Info.crc != 0)
                {
                    var crc = Utility.HashUtility.GetCrc32(Info.savePath);
                    if (Info.crc != crc)
                    {
                        File.Delete(Info.savePath);
                        error = $"Download crc {crc} mismatch to {Info.crc}";
                        Status = EDownloadStatus.Failed;
                        return;
                    }
                }

                Status = EDownloadStatus.Success;
            }
            catch (Exception e)
            {
                CloseWrite();
                error = e.Message;
                Status = EDownloadStatus.Failed;
            }
        }

        private void CloseWrite()
        {
            if (m_Writer != null)
            {
                m_Writer.Flush();
                m_Writer.Close();
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors spe)
        {
            return true;
        }

        private void Downloading()
        {
            var request = CreateWebRequest();
            using (var response = request.GetResponse())
            {
                if (response.ContentLength > 0)
                {
                    if (Info.size == 0) Info.size = response.ContentLength + DownloadedBytes;

                    using (var reader = response.GetResponseStream())
                    {
                        if (DownloadedBytes < Info.size)
                            while (Status == EDownloadStatus.Progressing)
                                if (ReadToEnd(reader))
                                    break;
                    }
                }
                else
                {
                    Status = EDownloadStatus.Success;
                }
            }
        }

        private WebRequest CreateWebRequest()
        {
            WebRequest request;
            if (Info.url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
                request = GetHttpWebRequest();
            }
            else
            {
                request = GetHttpWebRequest();
            }

            return request;
        }

        private WebRequest GetHttpWebRequest()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(Info.url);
            httpWebRequest.ProtocolVersion = HttpVersion.Version10;

            if (UseSegment)
            {
                httpWebRequest.AddRange(RangeFrom + DownloadedBytes, RangeTo);
            }
            else
            {
                if (DownloadedBytes > 0) httpWebRequest.AddRange(DownloadedBytes);
            }

            return httpWebRequest;
        }

        private bool ReadToEnd(Stream reader)
        {
            var len = reader.Read(m_ReadBuffer, 0, m_ReadBuffer.Length);
            if (len > 0)
            {
                m_Writer.Write(m_ReadBuffer, 0, len);
                DownloadedBytes += len;
                return false;
            }

            return true;
        }

        private void Start()
        {
            if (Status != EDownloadStatus.Wait) return;

            INFO("Start download {Info.url}");
            Status = EDownloadStatus.Progressing;
            var file = new FileInfo(Info.savePath);
            if (file.Exists && file.Length > 0)
            {
                if (Info.size > 0 && file.Length == Info.size)
                {
                    Status = EDownloadStatus.Success;
                    return;
                }

                m_Writer = File.OpenWrite(Info.savePath);
                DownloadedBytes = m_Writer.Length - 1;
                if (DownloadedBytes > 0) m_Writer.Seek(-1, SeekOrigin.End);
            }
            else
            {
                var dir = Path.GetDirectoryName(Info.savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                m_Writer = File.Create(Info.savePath);
                DownloadedBytes = 0;
            }

            Task.Run(Run);

            // TODO compare with Task.Run
            //m_Thread = new Thread(Run)
            //{
            //    IsBackground = true
            //};
            //m_Thread.Start();
        }

        #endregion


        private static void INFO(string msg)
        {
            Log.INFO("Download", msg);
        }

        private static void ERROR(string msg)
        {
            Log.ERROR("Download", msg);
        }

        private static void WARN(string msg)
        {
            Log.WARN("Download", msg);
        }
    }
}