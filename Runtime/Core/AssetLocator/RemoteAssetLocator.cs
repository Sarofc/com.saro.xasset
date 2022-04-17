﻿namespace Saro.XAsset
{
    /// <summary>
    /// 定位远端资源，永远返回false
    /// </summary>
    public sealed class RemoteAssetLocator : BaseAssetLocator
    {
        public RemoteAssetLocator(string directory) : base(directory)
        {
        }

        protected override bool GetAssetPath(string assetName, ref string assetPath, ref IRemoteAssets remoteAssets)
        {
            var manifest = XAssetManager.Current?.Manifest;
            if (manifest != null && manifest.TryGetRemoteAsset(assetName, out remoteAssets))
            {
                if (remoteAssets != null /*&& remoteAsset.Hash == fileInfo.Length*/)
                {
                    // 加载路径指向下载目录
                    assetPath = XAssetConfig.k_DlcPath + "/" + assetName;
                }
            }

            return false;
        }
    }
}
