using UnityEditor;
using Saro.SaroEditor;
using System.Collections.Generic;

namespace Saro.XAsset.Build
{
    [CustomEditor(typeof(XAssetSettings))]
    public class XAssetSettingsInspector : BaseEditor<XAssetSettings>
    {
        protected override void GetExcludedPropertiesInInspector(List<string> excluded)
        {
            base.GetExcludedPropertiesInInspector(excluded);
        }
    }
}