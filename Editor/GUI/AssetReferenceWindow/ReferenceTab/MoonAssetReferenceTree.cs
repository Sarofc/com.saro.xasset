﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace Saro.MoonAsset
{
    internal class MoonAssetReferenceTree : TreeView
    {
        private static Texture2D tex_warn = EditorGUIUtility.FindTexture("console.warnicon.sml");

        internal MoonAssetReferenceTree(TreeViewState state, MultiColumnHeaderState mchs) : base(state, new MultiColumnHeader(mchs))
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;

            multiColumnHeader.canSort = false;
            //multiColumnHeader.sortingChanged += OnSortingChanged;
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            var _item = item as MoonAssetReferenceItem;
            if (_item == null) return false;
            var assetUrl = _item.GetAssetURL();
            if (assetUrl == null) return false;
            return assetUrl.Contains(search, System.StringComparison.OrdinalIgnoreCase);
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            if (root.children == null) root.children = new();

            var assets = MoonAsset.Current.AnalyzeHandles;

            foreach (var item in assets)
            {
                root.AddChild(new MoonAssetReferenceItem(item.Value));
            }

            return root;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            return false;
        }

        protected override bool CanChangeExpandedState(TreeViewItem item)
        {
            return false;
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
                    headerContent = new GUIContent("AssetType", "资源类型"),
                    minWidth = 150,
                    width = 150,
                    maxWidth = 150,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = false,
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("AssetUrl", "资源地址"),
                    minWidth = 400,
                    width = 600,
                    maxWidth = 1000,
                    headerTextAlignment = TextAlignment.Left,
                    canSort = true,
                    autoResize = true,
                 },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("RefCount", "引用计数"),
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

        protected override void RowGUI(RowGUIArgs args)
        {
            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                CellGUI(args.GetCellRect(i), args.item as MoonAssetReferenceItem, args.GetColumn(i), ref args);
        }

        private void CellGUI(Rect cellRect, MoonAssetReferenceItem item, int column, ref RowGUIArgs args)
        {
            var bundleItem = (args.item as MoonAssetReferenceItem);
            if (args.item.icon == null)
                extraSpaceBeforeIconAndLabel = 16f;
            else
                extraSpaceBeforeIconAndLabel = 0f;

            Color old = GUI.color;
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case 0:
                    DefaultGUI.Label(cellRect, item.GetAssetType(), args.selected, args.focused);
                    break;
                case 1:
                    DefaultGUI.Label(cellRect, item.GetAssetURL(), args.selected, args.focused);
                    break;
                case 2:
                    DefaultGUI.Label(cellRect, item.GetRefCount().ToString(), args.selected, args.focused);
                    break;
                default:
                    break;
            }

            GUI.color = old;
        }


        #region Sort

        internal enum SortOption
        {
            Type,
            AssetUrl,
            RefCount,
        }

        private SortOption[] m_SortOptions =
        {
            SortOption.Type,
            SortOption.AssetUrl,
            SortOption.RefCount,
        };

        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        private void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
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

        private void SortByColumn()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            List<MoonAssetReferenceItem> assetList = new List<MoonAssetReferenceItem>();
            foreach (var item in rootItem.children)
            {
                assetList.Add(item as MoonAssetReferenceItem);
            }
            var orderedItems = InitialOrder(assetList, sortedColumns);

            rootItem.children = orderedItems.Cast<TreeViewItem>().ToList();
        }

        private IOrderedEnumerable<MoonAssetReferenceItem> InitialOrder(IEnumerable<MoonAssetReferenceItem> myTypes, int[] columnList)
        {
            SortOption sortOption = m_SortOptions[columnList[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(columnList[0]);
            switch (sortOption)
            {
                case SortOption.Type:
                    return myTypes.Order(l => l.GetAssetType(), ascending);
                case SortOption.AssetUrl:
                    return myTypes.Order(l => l.GetAssetURL(), ascending);
                case SortOption.RefCount:
                    return myTypes.Order(l => l.GetRefCount(), ascending);
                default:
                    return myTypes.Order(l => l.displayName, ascending);
            }
        }

        #endregion


        #endregion
    }

    internal static class MyExtensionMethods
    {
        internal static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, System.Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }
    }
}
