#region 程序集 Unity.ScriptableBuildPipeline.Editor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// 未知的位置
// Decompiled with ICSharpCode.Decompiler 6.1.0.5902
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace Saro.XAsset.Build
{
    public class CreateBuiltInResourcesBundle : IBuildTask
    {
        private static readonly GUID k_unity_builtin_extra = new GUID("0000000000000000f000000000000000"); // Resources/unity_builtin_extra
        private static readonly GUID k_unity_default_resources = new GUID("0000000000000000e000000000000000"); // Library/unity default resources

        [InjectContext(ContextUsage.In, false)]
        private IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.InOut, true)]
        private IBundleExplictObjectLayout m_Layout;

        public int Version => 1;

        public string ShaderBundleName { get; private set; }

        public string DataBundleName { get; private set; }

        public CreateBuiltInResourcesBundle(string dataBundleName, string shaderBundleName)
        {
            DataBundleName = dataBundleName;
            ShaderBundleName = shaderBundleName;
        }

        public ReturnCode Run()
        {
            HashSet<ObjectIdentifier> buildInObjects = new HashSet<ObjectIdentifier>();
            foreach (AssetLoadInfo dependencyInfo in m_DependencyData.AssetInfo.Values)
                buildInObjects.UnionWith(dependencyInfo.referencedObjects.Where(x => x.guid == k_unity_builtin_extra));

            foreach (SceneDependencyInfo dependencyInfo in m_DependencyData.SceneInfo.Values)
                buildInObjects.UnionWith(dependencyInfo.referencedObjects.Where(x => x.guid == k_unity_builtin_extra));

            ObjectIdentifier[] usedSet = buildInObjects.ToArray();
            Type[] usedTypes = ContentBuildInterface.GetTypeForObjects(usedSet);

            if (m_Layout == null)
                m_Layout = new BundleExplictObjectLayout();

            // 将 Shader 和非 Shader 资源分别记录到两个不同的 Bundle 中
            Type shader = typeof(Shader);
            for (int i = 0; i < usedTypes.Length; i++)
            {
                m_Layout.ExplicitObjectLocation.Add(usedSet[i], usedTypes[i] == shader ? ShaderBundleName : DataBundleName);
            }

            if (m_Layout.ExplicitObjectLocation.Count == 0)
                m_Layout = null;

            return ReturnCode.Success;
        }
    }
}