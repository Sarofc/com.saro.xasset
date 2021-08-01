using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;

namespace Saro.XAsset.Build
{
    internal class PostBuildMethods
    {
        [PostProcessBuild(1)]
        private static void TestMethod(BuildTarget BuildTarget, string path)
        {
            // TODO
            
        }
    }
}
