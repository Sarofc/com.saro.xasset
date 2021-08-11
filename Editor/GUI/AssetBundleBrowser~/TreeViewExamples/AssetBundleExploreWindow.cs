using System;
using System.Collections.Generic;
using System.Linq;
using Saro.SaroEditor;
using Saro.XAsset;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace Saro.XAsset.Build
{

    internal class AssetBundleExploreWindow : EditorWindow
    {
        [NonSerialized] bool m_InitializedBundleTreeView;
        [NonSerialized] bool m_InitializedAssetTreeView;

        [SerializeField] TreeViewState m_BundleTreeViewState; // Serialized in the window layout file so it survives assembly reloading
        [SerializeField] MultiColumnHeaderState m_BundleHeaderState;
        BundleTreeView m_BundleTreeView;

        [SerializeField] MultiColumnHeaderState m_AssetHeaderState;
        [SerializeField] TreeViewState m_AssetTreeViewState;
        AssetTreeView m_AssetTreeView;

        SearchField m_BundleSearchField;

        XAssetBuildRules m_MyTreeAsset;

        //[MenuItem("TreeView Examples/Multi Columns")]

        public static AssetBundleExploreWindow GetWindow()
        {
            var window = GetWindow<AssetBundleExploreWindow>();
            window.titleContent = new GUIContent("AssetBundleExploreWindow");
            window.Focus();
            window.Repaint();
            return window;
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var myTreeAsset = EditorUtility.InstanceIDToObject(instanceID) as XAssetBuildRules;
            if (myTreeAsset != null)
            {
                var window = GetWindow();
                window.SetTreeAsset(myTreeAsset);
                return true;
            }
            return false; // we did not handle the open
        }

        void SetTreeAsset(XAssetBuildRules myTreeAsset, bool reinit = true)
        {
            m_MyTreeAsset = myTreeAsset;

            //m_Bundle2Asseets.Clear();

            //foreach (var asset in m_MyTreeAsset.ruleAssets)
            //{
            //    var bundle = m_MyTreeAsset.bundles[asset.bundle];
            //    if (m_Bundle2Asseets.ContainsKey(bundle.id))
            //    {
            //        m_Bundle2Asseets[bundle.id].Add(asset);
            //    }
            //    else
            //    {
            //        m_Bundle2Asseets.Add(bundle.id, new List<AssetRef> { asset });
            //    }
            //}

            if (reinit)
            {
                m_InitializedBundleTreeView = false;
                m_InitializedAssetTreeView = false;
            }
        }

        Rect BundleTreeViewRect
        {
            get { return new Rect(20, 30, (position.width - 40) * (m_TreeViewSplitPercent), position.height - 60); }
        }

        float m_TreeViewSplitPercent = 0.55f;

        Rect AssetTreeViewRect
        {
            get { return new Rect(20 + (position.width - 40) * (m_TreeViewSplitPercent), 30, (position.width - 40) * (1f - m_TreeViewSplitPercent), position.height - 60); }
        }

        Rect toolbarRect
        {
            get { return new Rect(20f, 10f, position.width - 40f, 20f); }
        }

        Rect bottomToolbarRect
        {
            get { return new Rect(20f, position.height - 18f, position.width - 40f, 16f); }
        }

        void InitBundleTreeViewIfNeeded()
        {
            if (!m_InitializedBundleTreeView)
            {
                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_BundleTreeViewState == null)
                    m_BundleTreeViewState = new TreeViewState();

                bool firstInit = m_BundleHeaderState == null;
                var headerState = BundleTreeView.CreateDefaultMultiColumnHeaderState(BundleTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_BundleHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_BundleHeaderState, headerState);
                m_BundleHeaderState = headerState;

                var multiColumnHeader = new MyMultiColumnHeader(headerState);
                if (firstInit)
                    multiColumnHeader.ResizeToFit();

                var treeModel = new TreeModel<BundleTreeElement>(GetBundleData());

                m_BundleTreeView = new BundleTreeView(m_BundleTreeViewState, multiColumnHeader, treeModel);
                m_BundleTreeView.OnSelectionChanged += BundleTreeView_OnSelectionChanged;

                m_BundleSearchField = new SearchField();
                m_BundleSearchField.downOrUpArrowKeyPressed += m_BundleTreeView.SetFocusAndEnsureSelectedItem;

                m_InitializedBundleTreeView = true;
            }
        }

        private void BundleTreeView_OnSelectionChanged(IList<BundleTreeElement> list)
        {
            var index = 0;
            var newData = CreateAsssetTreeRoot();
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];

                //for (int j = 0; j < item.bundle.assets.Length; j++)
                //{
                //    var asset = item.bundle.assets[i];
                //    newData.Add(new AssetTreeElement(asset, 0, ++index));
                //}
                var assetDeps = AssetDatabase.GetDependencies(item.bundle.assets, true).ToList();
                for (int k = assetDeps.Count - 1; k >= 0; k--)
                {
                    if (!XAssetBuildRules.ValidateAsset(assetDeps[i]))
                    {
                        assetDeps.RemoveAt(k);
                    }
                }
                newData.AddRange(assetDeps.Select(a => new AssetTreeElement(a, 0, ++index)));
            }
            m_AssetTreeView.treeModel.SetData(newData);
            m_AssetTreeView.Reload();
        }

        void InitAssetTreeViewIfNeeded()
        {
            if (!m_InitializedAssetTreeView)
            {
                // Check if it already exists (deserialized from window layout file or scriptable object)
                if (m_AssetTreeViewState == null)
                    m_AssetTreeViewState = new TreeViewState();

                bool firstInit = m_AssetHeaderState == null;
                var headerState = AssetTreeView.CreateDefaultMultiColumnHeaderState(AssetTreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_AssetHeaderState, headerState))
                    MultiColumnHeaderState.OverwriteSerializedFields(m_AssetHeaderState, headerState);
                m_AssetHeaderState = headerState;

                var multiColumnHeader = new MyMultiColumnHeader(headerState);
                if (firstInit)
                    multiColumnHeader.ResizeToFit();

                var treeModel = new TreeModel<AssetTreeElement>(CreateAsssetTreeRoot());

                m_AssetTreeView = new AssetTreeView(m_AssetTreeViewState, multiColumnHeader, treeModel);

                //m_SearchField = new SearchField();
                //m_SearchField.downOrUpArrowKeyPressed += m_BundleTreeView.SetFocusAndEnsureSelectedItem;

                m_InitializedAssetTreeView = true;
            }
        }

        IList<BundleTreeElement> GetBundleData()
        {
            if (m_MyTreeAsset != null && m_MyTreeAsset.ruleBundles != null && m_MyTreeAsset.ruleBundles.Length > 0)
            {
                var index = 0;
                var ret = new List<BundleTreeElement>();
                ret.Add(new BundleTreeElement("root", -1, index));
                for (int i = 0; i < m_MyTreeAsset.ruleBundles.Length; i++)
                {
                    var bundle = m_MyTreeAsset.ruleBundles[i];
                    ret.Add(new BundleTreeElement(bundle, bundle.bundle, 0, ++index));
                }

                return ret;
            }

            return new List<BundleTreeElement>() { new BundleTreeElement("root", -1, 0) };
        }

        List<AssetTreeElement> CreateAsssetTreeRoot()
        {
            return new List<AssetTreeElement>() { new AssetTreeElement("root", -1, 0) };
        }

        void OnSelectionChange()
        {
            if (!m_InitializedBundleTreeView)
                return;

            var myTreeAsset = Selection.activeObject as XAssetBuildRules;
            if (myTreeAsset != null && myTreeAsset != m_MyTreeAsset)
            {
                SetTreeAsset(myTreeAsset, false);
                m_BundleTreeView.treeModel.SetData(GetBundleData());
                m_BundleTreeView.Reload();
            }
        }

        void OnGUI()
        {
            InitBundleTreeViewIfNeeded();
            InitAssetTreeViewIfNeeded();

            SearchBar(toolbarRect);
            DoBundleTreeView(BundleTreeViewRect);
            DoAssetTreeView(AssetTreeViewRect);
            BottomToolBar(bottomToolbarRect);
        }

        void SearchBar(Rect rect)
        {
            m_BundleTreeView.searchString = m_BundleSearchField.OnGUI(rect, m_BundleTreeView.searchString);
        }

        void DoBundleTreeView(Rect rect)
        {
            m_BundleTreeView.OnGUI(rect);
        }

        void DoAssetTreeView(Rect rect)
        {
            m_AssetTreeView.OnGUI(rect);
        }

        void BottomToolBar(Rect rect)
        {
            GUILayout.BeginArea(rect);

            using (new EditorGUILayout.HorizontalScope())
            {

                //var style = "miniButton";
                //if (GUILayout.Button("Expand All", style))
                //{
                //    m_BundleTreeView.ExpandAll();
                //}

                //if (GUILayout.Button("Collapse All", style))
                //{
                //    m_BundleTreeView.CollapseAll();
                //}

                GUILayout.FlexibleSpace();

                GUILayout.Label(m_MyTreeAsset != null ? AssetDatabase.GetAssetPath(m_MyTreeAsset) : string.Empty);

                GUILayout.FlexibleSpace();

                //if (GUILayout.Button("Set sorting", style))
                //{
                //    var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
                //    myColumnHeader.SetSortingColumns(new int[] { 4, 3, 2 }, new[] { true, false, true });
                //    myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
                //}


                //GUILayout.Label("Header: ", "minilabel");
                //if (GUILayout.Button("Large", style))
                //{
                //    var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
                //    myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
                //}
                //if (GUILayout.Button("Default", style))
                //{
                //    var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
                //    myColumnHeader.mode = MyMultiColumnHeader.Mode.DefaultHeader;
                //}
            }

            GUILayout.EndArea();
        }
    }


    internal class MyMultiColumnHeader : MultiColumnHeader
    {
        Mode m_Mode;

        public enum Mode
        {
            LargeHeader,
            DefaultHeader,
            MinimumHeaderWithoutSorting
        }

        public MyMultiColumnHeader(MultiColumnHeaderState state)
            : base(state)
        {
            mode = Mode.DefaultHeader;
        }

        public Mode mode
        {
            get
            {
                return m_Mode;
            }
            set
            {
                m_Mode = value;
                switch (m_Mode)
                {
                    case Mode.LargeHeader:
                        canSort = true;
                        height = 37f;
                        break;
                    case Mode.DefaultHeader:
                        canSort = true;
                        height = DefaultGUI.defaultHeight;
                        break;
                    case Mode.MinimumHeaderWithoutSorting:
                        canSort = false;
                        height = DefaultGUI.minimumHeight;
                        break;
                }
            }
        }

        protected override void ColumnHeaderGUI(MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
        {
            // Default column header gui
            base.ColumnHeaderGUI(column, headerRect, columnIndex);

            // Add additional info for large header
            if (mode == Mode.LargeHeader)
            {
                // Show example overlay stuff on some of the columns
                if (columnIndex > 2)
                {
                    headerRect.xMax -= 3f;
                    var oldAlignment = EditorStyles.largeLabel.alignment;
                    EditorStyles.largeLabel.alignment = TextAnchor.UpperRight;
                    GUI.Label(headerRect, 36 + columnIndex + "%", EditorStyles.largeLabel);
                    EditorStyles.largeLabel.alignment = oldAlignment;
                }
            }
        }
    }

}
