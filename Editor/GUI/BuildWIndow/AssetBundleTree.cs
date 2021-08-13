using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System;


namespace AssetBundleBrowser
{
    internal class AssetBundleTree : TreeView
    {
        AssetBundleManageTab m_Controller;
        private bool m_ContextOnItem = false;
        List<UnityEngine.Object> m_EmptyObjectList = new List<UnityEngine.Object>();

        internal AssetBundleTree(TreeViewState state, MultiColumnHeaderState mchs, AssetBundleManageTab ctrl) : base(state, new MultiColumnHeader(mchs))
        {
            AssetBundleModel.Model.Rebuild();
            m_Controller = ctrl;
            showBorder = true;

            showAlternatingRowBackgrounds = true;
            multiColumnHeader.sortingChanged += OnSortingChanged;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var bundleItem = item as AssetBundleModel.BundleTreeItem;
            return bundleItem.bundle.DoesItemMatchSearch(search);
        }

        //protected override void RowGUI(RowGUIArgs args)
        //{
        //    var bundleItem = (args.item as AssetBundleModel.BundleTreeItem);
        //    if (args.item.icon == null)
        //        extraSpaceBeforeIconAndLabel = 16f;
        //    else
        //        extraSpaceBeforeIconAndLabel = 0f;

        //    Color old = GUI.color;
        //    if ((bundleItem.bundle as AssetBundleModel.BundleVariantFolderInfo) != null)
        //        GUI.color = AssetBundleModel.Model.k_LightGrey; //new Color(0.3f, 0.5f, 0.85f);
        //    base.RowGUI(args);
        //    GUI.color = old;

        //    var message = bundleItem.BundleMessage();
        //    if(message.severity != MessageType.None)
        //    {
        //        var size = args.rowRect.height;
        //        var right = args.rowRect.xMax;
        //        Rect messageRect = new Rect(right - size, args.rowRect.yMin, size, size);
        //        GUI.Label(messageRect, new GUIContent(message.icon, message.message ));
        //    }
        //}

        protected override TreeViewItem BuildRoot()
        {
            AssetBundleModel.Model.Refresh();
            var root = AssetBundleModel.Model.CreateBundleTreeView();
            return root;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            var selectedBundles = new List<AssetBundleModel.BundleInfo>();
            if (selectedIds != null)
            {
                foreach (var id in selectedIds)
                {
                    var item = FindItem(id, rootItem) as AssetBundleModel.BundleTreeItem;
                    if (item != null && item.bundle != null)
                    {
                        item.bundle.RefreshAssetList();
                        selectedBundles.Add(item.bundle);
                    }
                }
            }

            m_Controller.UpdateSelectedBundles(selectedBundles);
        }

        public override void OnGUI(Rect rect)
        {
            base.OnGUI(rect);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
            }
        }

        internal void Refresh()
        {
            var selection = GetSelection();
            Reload();
            SelectionChanged(selection);
        }


        #region MyRegion


        internal static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            return new MultiColumnHeaderState(GetColumns());
        }
        private static MultiColumnHeaderState.Column[] GetColumns()
        {
            var retVal = new MultiColumnHeaderState.Column[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Bundle", "Bundle name. 'auto' means asset was pulled in due to dependency"),
                    minWidth = 400,
                    width = 400,
                    maxWidth = 420,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = true,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Size", "Size on disk"),
                    minWidth = 100,
                    width = 100,
                    maxWidth = 100,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = false,
                 },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Bundle Size", "Size of bundle"),
                    minWidth = 100,
                    width = 100,
                    maxWidth = 100,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = false,
                 },
            };

            return retVal;
        }
        enum MyColumns
        {
            Bundle,
            Size,
            RealSize,
        }
        internal enum SortOption
        {
            Bundle,
            Size,
            BundleSize,
        }
        SortOption[] m_SortOptions =
        {
            SortOption.Bundle,
            SortOption.Size,
            SortOption.BundleSize,
        };


        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item as AssetBundleModel.BundleTreeItem, args.GetColumn(i), ref args);
        }

        private void CellGUI(Rect cellRect, AssetBundleModel.BundleTreeItem item, int column, ref RowGUIArgs args)
        {
            var bundleItem = (args.item as AssetBundleModel.BundleTreeItem);
            if (args.item.icon == null)
                extraSpaceBeforeIconAndLabel = 16f;
            else
                extraSpaceBeforeIconAndLabel = 0f;

            Color old = GUI.color;
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0:
                    if ((bundleItem.bundle as AssetBundleModel.BundleVariantFolderInfo) != null)
                        GUI.color = AssetBundleModel.Model.k_LightGrey; //new Color(0.3f, 0.5f, 0.85f);
                    base.RowGUI(args);
                    break;
                case 1:
                    DefaultGUI.Label(cellRect, item.bundle.TotalSize(), args.selected, args.focused);
                    break;
                case 2:
                    DefaultGUI.Label(cellRect, item.bundle.BundleSize(), args.selected, args.focused);
                    break;
                default:
                    break;
            }

            GUI.color = old;

            var message = bundleItem.BundleMessage();
            if (message.severity != MessageType.None)
            {
                var size = args.rowRect.height;
                var right = args.rowRect.xMax;
                Rect messageRect = new Rect(right - size, args.rowRect.yMin, size, size);
                GUI.Label(messageRect, new GUIContent(message.icon, message.message));
            }
        }

        void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }
        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
                return;

            SortByColumn();

            rows.Clear();
            for (int i = 0; i < root.children.Count; i++)
                rows.Add(root.children[i]);

            Repaint();
        }
        void SortByColumn()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            List<AssetBundleModel.BundleTreeItem> assetList = new List<AssetBundleModel.BundleTreeItem>();
            foreach (var item in rootItem.children)
            {
                assetList.Add(item as AssetBundleModel.BundleTreeItem);
            }
            var orderedItems = InitialOrder(assetList, sortedColumns);

            rootItem.children = orderedItems.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<AssetBundleModel.BundleTreeItem> InitialOrder(IEnumerable<AssetBundleModel.BundleTreeItem> myTypes, int[] columnList)
        {
            SortOption sortOption = m_SortOptions[columnList[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(columnList[0]);
            switch (sortOption)
            {
                case SortOption.Bundle:
                    return myTypes.Order(l => l.displayName, ascending);
                case SortOption.Size:
                    return myTypes.Order(l => l.bundle.totalSize, ascending);
                case SortOption.BundleSize:
                    return myTypes.Order(l => l.bundle.bundleSize, ascending);
                default:
                    return myTypes.Order(l => l.bundle.displayName, ascending);
            }

        }


        #endregion
    }
}
