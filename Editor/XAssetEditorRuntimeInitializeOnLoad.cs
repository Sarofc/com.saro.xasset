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
#if UNITY_EDITOR
            XAssetComponent.s_RuntimeMode = XAssetBuildScript.GetXAssetSettings().runtimeMode;
#endif

            XAssetComponent.s_EditorLoader = AssetDatabase.LoadAssetAtPath;
        }

        [System.Obsolete("没用")]
        void ProgressEditorSceneSettings()
        {
            var assets = new List<string>();
            var rules = XAssetBuildScript.GetXAssetBuildRules();
            foreach (var asset in rules.scenesInBuild)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                assets.Add(path);
            }
            foreach (var rule in rules.rules)
            {
                if (rule.searchPattern.Contains("*.unity"))
                {
                    assets.AddRange(rule.GetAssets());
                }
            }
            var scenes = new EditorBuildSettingsScene[assets.Count];
            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];
                scenes[index] = new EditorBuildSettingsScene(asset, true);
            }
            EditorBuildSettings.scenes = scenes;
        }
    }
}