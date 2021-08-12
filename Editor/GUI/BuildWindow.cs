using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace Saro.XAsset.Build
{
    public sealed class BuildWindow : EditorWindow
    {
        [MenuItem("Tools/Build")]
        private static void ShowBuildWindow()
        {
            var window = GetWindow<BuildWindow>();
            window.titleContent = new GUIContent("MGF-Build");
            window.Show();
        }

        private class Styles
        {
            public GUIStyle style_FontItalic;
            public GUIStyle style_FontBlodAndItalic;

            public Styles()
            {
                style_FontItalic = new GUIStyle()
                {
                    fontStyle = FontStyle.Italic
                };

                style_FontBlodAndItalic = new GUIStyle()
                {
                    fontStyle = FontStyle.BoldAndItalic
                };
            }
        }

        private static Styles s_Styles;

        private void OnEnable()
        {
            EnsureXAssetSettings();
            EnsureBuildMethods();
        }

        private void OnGUI()
        {
            if (s_Styles == null)
            {
                s_Styles = new Styles();
            }

            DrawToolBar();
        }

        private void DrawToolBar()
        {
            m_Selected = GUILayout.Toolbar(m_Selected, s_Toolbar);
            m_ScrolPos = EditorGUILayout.BeginScrollView(m_ScrolPos);
            switch (m_Selected)
            {
                case 0:
                    DrawBuildSettings();
                    EditorGUILayout.Space();
                    DrawButtons();
                    EditorGUILayout.Space();
                    DrawBuildOptions();
                    break;
                default:
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBuildSettings()
        {
            GUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Platform: " + EditorUserBuildSettings.activeBuildTarget, s_Styles.style_FontBlodAndItalic);

            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone));
                    break;
                case BuildTarget.iOS:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.iOS));
                    break;
                case BuildTarget.Android:
                    EditorGUILayout.LabelField("Scripting Backend: " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
                    break;
                default:
                    break;
            }

            EditorUserBuildSettings.development = EditorGUILayout.Toggle("Devepment Build: ", EditorUserBuildSettings.development);
            if (!EditorUserBuildSettings.development) GUI.enabled = false;
            EditorGUI.indentLevel++;
            EditorUserBuildSettings.connectProfiler = EditorGUILayout.Toggle("Connect Profiler: ", EditorUserBuildSettings.connectProfiler);
            EditorUserBuildSettings.allowDebugging = EditorGUILayout.Toggle("Script Debugging: ", EditorUserBuildSettings.allowDebugging);
            EditorUserBuildSettings.buildScriptsOnly = EditorGUILayout.Toggle("Build Scripts Only: ", EditorUserBuildSettings.buildScriptsOnly);
            EditorGUI.indentLevel--;
            if (!EditorUserBuildSettings.development) GUI.enabled = true;

            EditorGUILayout.Space();

            if (m_XAssetSettings != null)
            {
                Editor.CreateCachedEditor(m_XAssetSettings, typeof(XAssetSettingsInspector), ref m_CachedEditor);
                if (m_CachedEditor != null)
                {
                    m_CachedEditor.OnInspectorGUI();
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawBuildOptions()
        {
            GUILayout.BeginVertical("box");

            if (m_BuildMethods != null)
            {
                var rect = EditorGUILayout.GetControlRect();

                EditorGUI.LabelField(rect, "Build Pass：");

                rect.x = rect.width - 100f;
                rect.width = 100f;

                if (GUI.Button(rect, "Build Selected"))
                {
                    ExecuteAction(() =>
                    {
                        for (int i = 0; i < m_BuildMethods.Count; i++)
                        {
                            var buildMethod = m_BuildMethods[i];
                            if ((m_XAssetSettings.buildMethodOptions & (1 << i)) != 0 || buildMethod.required)
                            {
                                if (buildMethod.callback.Invoke() == false)
                                {
                                    throw new System.Exception(string.Format("Execute {0} Failed, Abort！", buildMethod.displayName));
                                }

                                Debug.Log($"Execute {buildMethod.displayName} Successfull");
                            }
                        }
                    });
                }

                for (int i = 0; i < m_BuildMethods.Count; i++)
                {
                    DrawBuildMethod(i, m_BuildMethods[i]);
                }
            }

            GUILayout.EndVertical();
        }

        private void DrawBuildMethod(int index, BuildMethod buildMethod)
        {
            if (buildMethod != null)
            {
                var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 8);
                EditorGUI.HelpBox(rect, string.Empty, MessageType.None);

                var rect1 = rect;
                rect1.y += 4f;
                rect1.x += 10f;
                rect1.width = 50f;


                buildMethod.selected = (m_XAssetSettings.buildMethodOptions & (1 << index)) != 0 || buildMethod.required;

                var tmpEnable = GUI.enabled;
                GUI.enabled = !buildMethod.required;
                buildMethod.selected = EditorGUI.ToggleLeft(rect1, string.Empty, buildMethod.selected);
                GUI.enabled = tmpEnable;

                if (buildMethod.selected) m_XAssetSettings.buildMethodOptions |= 1 << index;
                else m_XAssetSettings.buildMethodOptions &= ~(1 << index);

                rect1.x = 40f;
                rect1.width = 300f;
                EditorGUI.LabelField(rect1, string.Format("[{0:00}] {1}", buildMethod.order, buildMethod.displayName), buildMethod.required ? s_Styles.style_FontBlodAndItalic : s_Styles.style_FontItalic);
                rect1.x = rect.width - 40;
                rect1.width = 40;
                rect1.height = EditorGUIUtility.singleLineHeight;

                if (GUI.Button(rect1, "Run"))
                {
                    ExecuteAction(() =>
                    {
                        if (buildMethod.callback.Invoke() == false)
                        {
                            EditorUtility.DisplayDialog("Failed", string.Format("Execute {0} failed!", buildMethod.displayName), "OK");
                        }
                    });
                }
            }
        }

        private void DrawButtons()
        {
            GUILayout.BeginVertical("box");
            {
                GUILayout.Label("functions");

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Player Settings.."))
                {
                    SettingsService.OpenProjectSettings("Project/Player");
                }

                if (GUILayout.Button("XAssetRule.."))
                {
                    Selection.activeObject = XAssetBuildScript.GetXAssetBuildGroups();
                }

                if (GUILayout.Button("Run HFS"))
                {
                    var absoluteHfsExe = System.IO.Path.GetFullPath("Packages/com.saro.xasset/Editor/HFS/hfs.exe");
                    //Debug.LogError(absoluteHfsExe);
                    Common.Cmder.Run(absoluteHfsExe);
                }

                if (GUILayout.Button("Refresh Scenes"))
                {
                    var paths = GetAllScenes();

                    var scenes = new EditorBuildSettingsScene[paths.Length];

                    for (int i = 0; i < paths.Length; i++)
                    {
                        var path = paths[i];
                        scenes[i] = new EditorBuildSettingsScene(path, true);
                    }

                    EditorBuildSettings.scenes = scenes;
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private string[] GetAllScenes()
        {
            var sceneGUIDs = AssetDatabase.FindAssets("t:scene");

            var paths = new string[sceneGUIDs.Length];
            for (int i = 0; i < sceneGUIDs.Length; i++)
            {
                string guid = sceneGUIDs[i];
                paths[i] = AssetDatabase.GUIDToAssetPath(guid);
            }

            return paths;
        }

        private static string[] s_Toolbar = new string[]
        {
             "XAssetSettings"
        };
        private Editor m_CachedEditor;
        private List<BuildMethod> m_BuildMethods;
        private XAssetSettings m_XAssetSettings;
        private int m_Selected;
        private Vector2 m_ScrolPos;

        private void EnsureBuildMethods()
        {
            m_BuildMethods = BuildMethod.BuildMethodCollection;
        }

        private void EnsureXAssetSettings()
        {
            m_XAssetSettings = XAssetBuildScript.GetXAssetSettings();
            XAssetBuildScript.GetXAssetManifest();
            XAssetBuildScript.GetXAssetBuildGroups();
        }

        private void ExecuteAction(System.Action action)
        {
            EditorUtility.DisplayProgressBar("Wait...", "", 0);

            EditorApplication.delayCall = () =>
            {
                EditorApplication.delayCall = null;
                if (action != null)
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            };
        }
    }
}