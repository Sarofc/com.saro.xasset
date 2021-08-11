using System;
using Saro.SaroEditor;

namespace Saro.XAsset.Build
{

    [Serializable]
    internal class AssetTreeElement : TreeElement
    {
        public AssetRef asset;

        public AssetTreeElement(string name, int depth, int id) : this(default(AssetRef), name, depth, id) { }

        public AssetTreeElement(AssetRef asset, string name, int depth, int id) : base(name, depth, id)
        {
            this.asset = asset;
        }
    }
}
