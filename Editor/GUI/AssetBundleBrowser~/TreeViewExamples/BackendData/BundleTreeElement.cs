using System;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEditor;
using Saro.SaroEditor;

namespace Saro.XAsset.Build
{

    [Serializable]
    internal class BundleTreeElement : TreeElement
    {
        public RuleBundle bundle;

        public BundleTreeElement(string name, int depth, int id) : this(default(RuleBundle), name, depth, id) { }

        public BundleTreeElement(RuleBundle bundle, string name, int depth, int id) : base(name, depth, id)
        {
            this.bundle = bundle;
        }
    }
}
