using System;
using System.Collections.Generic;
using System.Reflection;

namespace Saro.XAsset.Build
{
    public class BuildMethod
    {
        public int order;
        public string description;
        public bool required;
        public bool selected = false;
        public Func<bool> callback;

        private static List<BuildMethod> s_BuildMethods;
        public static List<BuildMethod> BuildMethodCollection
        {
            get
            {
                if (s_BuildMethods == null)
                {
                    s_BuildMethods = GetBuildMethods();
                }
                return s_BuildMethods;
            }
        }

        private static List<BuildMethod> GetBuildMethods()
        {
            var allTypes = Utility.RefelctionUtility.GetSubClassTypesAllAssemblies(typeof(IBuildProcessor));

            var ret = new List<BuildMethod>();

            foreach (var type in allTypes)
            {
                var methods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var method in methods)
                {
                    var attr = method.GetCustomAttribute<XAssetBuildMethodAttribute>();
                    if (attr != null)
                    {
                        var buildMethod = new BuildMethod()
                        {
                            order = attr.executeOrder,
                            description = attr.displayName,
                            required = attr.required,
                            selected = attr.required,
                            callback = () =>
                            {
                                if (method.ReturnType == typeof(bool))
                                {
                                    return (bool)method.Invoke(null, null);
                                }
                                else
                                {
                                    try { method.Invoke(null, null); }
                                    catch (Exception e)
                                    {
                                        UnityEngine.Debug.LogException(e);
                                        return false;
                                    }
                                    return true;
                                }
                            }
                        };
                        ret.Add(buildMethod);
                    }
                }
            }

            ret.Sort((a, b) => a.order.CompareTo(b.order));

            return ret;
        }
    }

    /// <summary>
    /// XAsset自动化打包流程方法
    /// <see cref="BuildMethods"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class XAssetBuildMethodAttribute : Attribute
    {
        /// <summary>
        /// 执行顺序
        /// </summary>
        public int executeOrder;
        /// <summary>
        /// 显示名称
        /// </summary>
        public string displayName;
        /// <summary>
        /// 是否必须被执行
        /// </summary>
        public bool required;

        public XAssetBuildMethodAttribute(int executeOrder, string displayName, bool required = true)
        {
            this.executeOrder = executeOrder;
            this.displayName = displayName;
            this.required = required;
        }
    }
}
