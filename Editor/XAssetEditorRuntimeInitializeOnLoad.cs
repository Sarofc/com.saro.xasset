#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Saro.XAsset.Build
{
    public class XAssetEditorRuntimeInitializeOnLoad
    {
#if UNITY_2019_1_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void OnInitialize()
        {
            XAssetComponent.s_RuntimeMode = XAssetBuildScript.GetXAssetSettings().runtimeMode;
            XAssetComponent.s_EditorLoader = AssetDatabase.LoadAssetAtPath;
        }
    }
}

#endif