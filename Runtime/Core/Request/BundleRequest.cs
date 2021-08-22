using System.Collections.Generic;
using UnityEngine;

namespace Saro.XAsset
{
    public class BundleRequest : AssetRequest
    {
        public readonly List<BundleRequest> Dependencies = new List<BundleRequest>();

        public virtual AssetBundle AssetBundle
        {
            get { return Asset as AssetBundle; }
            internal set { Asset = value; }
        }

        internal override void Load()
        {
            //asset = Versions.LoadAssetBundleFromFile(url);
            Asset = AssetBundle.LoadFromFile(AssetUrl);
            //Asset = XAsset.Get().LoadAssetBundleFromFile(Url);

            if (AssetBundle == null)
                Error = AssetUrl + " LoadFromFile failed.";
        }

        internal override void Unload()
        {
            if (AssetBundle == null)
                return;
            AssetBundle.Unload(true);
            AssetBundle = null;
        }
    }
}