using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saro.XAsset
{
    [Serializable]
    public struct AssetRef
    {
        /// <summary>
        /// 资源名
        /// </summary>
        public string name;

        /// <summary>
        /// 资源所在包索引，<see cref="XAssetManifest.bundles"/>
        /// </summary>
        public int bundle;

        /// <summary>
        /// 资源所在路径索引，<see cref="XAssetManifest.dirs"/>
        /// </summary>
        public int dir;
    }

    [Serializable]
    public struct BundleRef
    {
        /// <summary>
        /// 包名
        /// </summary>
        public string name;

        /// <summary>
        /// 依赖包索引，<see cref="XAssetManifest.bundles"/>
        /// </summary>
        public int[] deps;

        /// <summary>
        /// 包大小
        /// </summary>
        public long len;

        /// <summary>
        /// 包流哈希
        /// </summary>
        public string hash;
    }

    public class XAssetManifest : ScriptableObject
    {
        /// <summary>
        /// 已有变体
        /// TODO 暂时未支持
        /// </summary>
        public string[] activeVariants = new string[0];

        /// <summary>
        /// 资源路径
        /// </summary>
        public string[] dirs = new string[0];

        /// <summary>
        /// 资源
        /// </summary>
        public AssetRef[] assets = new AssetRef[0];

        /// <summary>
        /// 包
        /// </summary>
        public BundleRef[] bundles = new BundleRef[0];

    }
}