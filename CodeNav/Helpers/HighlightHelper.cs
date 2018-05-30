﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeNav.Models;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Window = EnvDTE.Window;

namespace CodeNav.Helpers
{
    public static class HighlightHelper
    {
        public static void HighlightCurrentItem(Window window, CodeDocumentViewModel codeDocumentViewModel)
        {
            try
            {
                if (!(window?.Selection is TextSelection)) return;
            }
            catch (Exception)
            {
                return;
            }

            HighlightCurrentItem(codeDocumentViewModel, ((TextSelection)window.Selection).CurrentLine,
                BrushHelper.ToBrush(EnvironmentColors.ToolWindowTabSelectedTextColorKey),
                BrushHelper.ToBrush(EnvironmentColors.AccessKeyToolTipDisabledTextColorKey),
                BrushHelper.ToBrush(EnvironmentColors.FileTabButtonDownSelectedActiveColorKey),
                BrushHelper.ToBrush(EnvironmentColors.ToolWindowTextColorKey));
        }

        public static void HighlightCurrentItem(CodeDocumentViewModel codeDocumentViewModel, int currentLine, 
            SolidColorBrush foreground, SolidColorBrush background, SolidColorBrush border, SolidColorBrush regularForeground)
        {
            if (codeDocumentViewModel == null) return;

            UnHighlight(codeDocumentViewModel, regularForeground);
            var itemsToHighlight = GetItemsToHighlight(codeDocumentViewModel.CodeDocument, currentLine);
            Highlight(codeDocumentViewModel, itemsToHighlight.Select(i => i.Id), foreground, background, border);
        }

        private static void UnHighlight(CodeDocumentViewModel codeDocumentViewModel, SolidColorBrush foreground) =>
            UnHighlight(codeDocumentViewModel.CodeDocument, foreground, codeDocumentViewModel.Bookmarks);

        private static void UnHighlight(List<CodeItem> codeItems, SolidColorBrush foreground, 
            Dictionary<string, BookmarkStyle> bookmarks)
        {
            foreach (var item in codeItems)
            {
                if (item == null) continue;
               
                item.FontWeight = FontWeights.Regular;

                if (!BookmarkHelper.IsBookmark(bookmarks, item))
                {
                    item.Background = Brushes.Transparent;
                    item.Foreground = foreground;
                } else
                {
                    item.Foreground = bookmarks[item.Id].Foreground;
                }

                if (item is IMembers)
                {
                    var hasMembersItem = (IMembers)item;

                    if (hasMembersItem.Members.Any())
                    {
                        UnHighlight(hasMembersItem.Members, foreground, bookmarks);
                    }
                }

                if (item is CodeClassItem)
                {
                    var classItem = (CodeClassItem)item;
                    classItem.BorderBrush = new SolidColorBrush(Colors.DarkGray);
                }
            }
        }

        /// <summary>
        /// Given a list of unique ids and a code document, find all code items and 'highlight' them.
        /// Highlighting changes the foreground, fontweight and background of a code item
        /// </summary>
        /// <param name="document">Code document</param>
        /// <param name="ids">List of unique code item ids</param>
        private static void Highlight(CodeDocumentViewModel codeDocumentViewModel, IEnumerable<string> ids, 
            SolidColorBrush foreground, SolidColorBrush background, SolidColorBrush border)
        {
            FrameworkElement element = null;

            // Reverse Ids, so they are in Namespace -> Class -> Method order
            //ids = ids.Reverse();

            foreach (var id in ids)
            {
                var item = FindCodeItem(codeDocumentViewModel.CodeDocument, id);
                if (item == null) return;

                item.Foreground = foreground;
                item.FontWeight = FontWeights.Bold;

                if (!BookmarkHelper.IsBookmark(codeDocumentViewModel, item))
                {
                    item.Background = background;
                }

                if (element == null && item.Control != null)
                {
                    element = item.Control.CodeItemsControl;
                }

                var found = FindItemContainer(element as ItemsControl, item);
                if (found != null)
                {
                    element = found;

                    if (!(item is IMembers))
                    {
                        found.BringIntoView();
                    }
                }

                if (item is CodeClassItem)
                {
                    (item as CodeClassItem).BorderBrush = border;
                }
            }           
        }

        private static IEnumerable<CodeItem> GetItemsToHighlight(IEnumerable<CodeItem> items, int line)
        {
            var itemsToHighlight = new List<CodeItem>();

            foreach (var item in items)
            {
                if (item.StartLine <= line && item.EndLine >= line)
                {
                    itemsToHighlight.Add(item);
                }

                if (item is IMembers)
                {
                    itemsToHighlight.AddRange(GetItemsToHighlight(((IMembers)item).Members, line));
                }
            }

            return itemsToHighlight;
        }
            
        public static void SetForeground(IEnumerable<CodeItem> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                item.Foreground = BrushHelper.ToBrush(EnvironmentColors.ToolWindowTextColorKey);

                if (item is IMembers)
                {
                    var hasMembersItem = (IMembers) item;
                    if (hasMembersItem.Members.Any())
                    {
                        SetForeground(hasMembersItem.Members);
                    }
                }
            }
        }

        /// <summary>
        /// Find frameworkElement belonging to a code item
        /// </summary>
        /// <param name="itemsControl">itemsControl to search in</param>
        /// <param name="item">item to find</param>
        /// <returns></returns>
        private static FrameworkElement FindItemContainer(ItemsControl itemsControl, CodeItem item)
        {
            if (itemsControl == null) return null;

            var itemContainer = itemsControl.ItemContainerGenerator.ContainerFromItem(item);
            var itemContainerSubItemsControl = FindVisualChild<ItemsControl>(itemContainer);

            if (itemContainerSubItemsControl != null)
            {
                return itemContainerSubItemsControl;
            }

            if ((itemContainer as ContentPresenter)?.Content == item)
            {
                return itemContainer as FrameworkElement;
            }

            return null;
        }

        private static CodeItem FindCodeItem(IEnumerable<CodeItem> items, string id)
        {
            foreach (var item in items)
            {
                if (item.Id.Equals(id))
                {
                    return item;
                }

                if (item is IMembers)
                {
                    var hasMembersItem = (IMembers)item;
                    if (hasMembersItem.Members.Any())
                    {
                        var found = FindCodeItem(hasMembersItem.Members, id);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }

        public static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        return (T)child;
                    }

                    T childItem = FindVisualChild<T>(child);
                    if (childItem != null) return childItem;
                }
            }
            return null;
        }
    }

    public class CodeItemKindComparer : IComparer<CodeItem>
    {
        public int Compare(CodeItem itemA, CodeItem itemB)
        {
            switch (itemA.Kind)
            {
                case CodeItemKindEnum.Class:
                    switch (itemB.Kind)
                    {
                        case CodeItemKindEnum.Class:
                            return 0;
                        case CodeItemKindEnum.Interface:
                            return -1;
                        default:
                            return 1;
                    }
                case CodeItemKindEnum.Constant:
                case CodeItemKindEnum.Constructor:
                case CodeItemKindEnum.Delegate:
                case CodeItemKindEnum.Enum:
                case CodeItemKindEnum.EnumMember:
                case CodeItemKindEnum.Event:
                case CodeItemKindEnum.Method:
                case CodeItemKindEnum.Property:
                case CodeItemKindEnum.Variable:
                    return -1;
                case CodeItemKindEnum.Interface:
                case CodeItemKindEnum.Namespace:
                    return 1;
                case CodeItemKindEnum.Region:
                case CodeItemKindEnum.Struct:
                    switch (itemB.Kind)
                    {
                        case CodeItemKindEnum.Region:
                        case CodeItemKindEnum.Struct:
                            return 0;
                        case CodeItemKindEnum.Interface:
                        case CodeItemKindEnum.Class:
                            return -1;
                        default:
                            return 1;
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
