using System;
using System.Collections.Generic;
using System.Linq;
using Saro.XAsset;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using Saro.SaroEditor;

namespace Saro.XAsset.Build
{
    internal class BundleTreeView : TreeViewWithTreeModel<BundleTreeElement>
    {
        protected float m_RowHeights = 20f;
        protected float m_ToggleWidth = 18f;

        static Texture2D[] s_TestIcons =
        {
            EditorGUIUtility.FindTexture("CacheServerConnected@2x"),
            EditorGUIUtility.FindTexture("SceneAsset Icon"),
        };

        // All columns
        enum MyColumns
        {
            Icon,
            Name,
            Size,
        }

        public enum SortOption
        {
            Icon,
            Name,
            Size,
        }

        // Sort options per column
        SortOption[] m_SortOptions =
        {
            SortOption.Icon,
            SortOption.Name,
            SortOption.Size,
        };

        public event Action<IList<BundleTreeElement>> OnSelectionChanged;

        public BundleTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<BundleTreeElement> model) : base(state, multicolumnHeader, model)
        {
            Assert.AreEqual(m_SortOptions.Length, Enum.GetValues(typeof(MyColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

            // Custom setup
            rowHeight = m_RowHeights;
            columnIndexForTreeFoldouts = 0;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (m_RowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            extraSpaceBeforeIconAndLabel = m_ToggleWidth;
            multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }

        // Note we We only build the visible rows, only the backend has the full tree information. 
        // The treeview only creates info for the row list.
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
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
            {
                return; // No column to sort for (just use the order the data are in)
            }

            // Sort the roots of the existing tree items
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
        }

        void SortByMultipleColumns()
        {
            var sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            var myTypes = rootItem.children.Cast<TreeViewItem<BundleTreeElement>>();
            var orderedQuery = InitialOrder(myTypes, sortedColumns);
            for (int i = 1; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.Name:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
                        break;
                    case SortOption.Size:
                        //orderedQuery = orderedQuery.ThenBy(l => l.data.bundle.len, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItem<BundleTreeElement>> InitialOrder(IEnumerable<TreeViewItem<BundleTreeElement>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.Name:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.Size:
                    //return myTypes.Order(l => l.data.bundle.len, ascending);
                default:
                    Assert.IsTrue(false, "Unhandled enum");
                    break;
            }

            // default
            return myTypes.Order(l => l.data.name, ascending);
        }

        int GetIconIndex(TreeViewItem<BundleTreeElement> item)
        {
            // TODO 看如何区分 场景包 和 普通包
            return 0;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItem<BundleTreeElement>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem<BundleTreeElement> item, MyColumns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                //case MyColumns.Icon1:
                //    {
                //        GUI.DrawTexture(cellRect, s_TestIcons[GetIcon1Index(item)], ScaleMode.ScaleToFit);
                //    }
                //    break;
                case MyColumns.Icon:
                    {
                        GUI.DrawTexture(cellRect, s_TestIcons[GetIconIndex(item)], ScaleMode.ScaleToFit);
                    }
                    break;

                case MyColumns.Name:
                    {
                        // Do toggle
                        Rect toggleRect = cellRect;
                        toggleRect.x += GetContentIndent(item);
                        toggleRect.width = m_ToggleWidth;

                        // Default icon and label
                        args.rowRect = cellRect;
                        base.RowGUI(args);
                    }
                    break;

                case MyColumns.Size:
                    {
                        string value = "Missing";
                        //if (column == MyColumns.Size)
                        //    value = Update.DownloaderComponent.GetDisplaySize(item.data.bundle.len);

                        DefaultGUI.LabelRightAligned(cellRect, value, args.selected, args.focused);
                    }
                    break;
            }
        }

        // Rename
        //--------

        protected override bool CanRename(TreeViewItem item)
        {
            // Only allow rename if we can show the rename overlay with a certain width (label might be clipped by other columns)
            Rect renameRect = GetRenameRect(treeViewRect, 0, item);
            return renameRect.width > 30;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            // Set the backend name and reload the tree to reflect the new model
            if (args.acceptedRename)
            {
                var element = treeModel.Find(args.itemID);
                element.name = args.newName;
                Reload();
            }
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            var listFind = new List<BundleTreeElement>();
            for (int i = 0; i < selectedIds.Count; i++)
            {
                var id = selectedIds[i];
                var item = FindItem(id, rootItem);
                if(item is TreeViewItem<BundleTreeElement> _find)
                {
                    listFind.Add(_find.data);
                }
            }

            OnSelectionChanged?.Invoke(listFind);
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            Rect cellRect = GetCellRectForTreeFoldouts(rowRect);
            CenterRectUsingSingleLineHeight(ref cellRect);
            return base.GetRenameRect(cellRect, row, item);
        }

        // Misc
        //--------

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth)
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType"), "Sed hendrerit mi enim, eu iaculis leo tincidunt at."),
                    contextMenuText = "Type",
                    headerTextAlignment = TextAlignment.Center,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 30,
                    minWidth = 30,
                    maxWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 400,
                    minWidth = 400,
                    maxWidth = 500,
                    autoResize = false,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Size"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Center,
                    width = 80,
                    minWidth = 60,
                    maxWidth = 100,
                    autoResize = false,
                    allowToggleVisibility = false
                },
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var state = new MultiColumnHeaderState(columns);
            return state;
        }
    }

    static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
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

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }
    }
}
