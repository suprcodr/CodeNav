﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CodeNav.Models;
using CodeNav.Properties;

namespace CodeNav.Helpers
{
    public static class VisibilityHelper
    {
        public static List<CodeItem> SetCodeItemVisibility(CodeDocumentViewModel model)
            => SetCodeItemVisibility(model.CodeDocument, model.FilterText, model.FilterOnBookmarks, model.Bookmarks);

        /// <summary>
        /// Loop through all codeItems and look into Settings to see if the item should be visible or not.
        /// </summary>
        /// <param name="document">List of codeItems</param>
        /// <param name="name">Filters items by name</param>
        /// <param name="filterOnBookmarks">Filters items by being bookmarked</param>
        /// <param name="bookmarks">List of bookmarked items</param>
        public static List<CodeItem> SetCodeItemVisibility(List<CodeItem> document, string name = "",
            bool filterOnBookmarks = false, Dictionary<string, int> bookmarks = null)
        {
            try
            {
                if (document == null || !document.Any())
                {
                    // No code items have been found to filter on by name
                    return new List<CodeItem>();
                }

                foreach (var item in document)
                {
                    if (item is IMembers hasMembersItem && hasMembersItem.Members.Any())
                    {
                        SetCodeItemVisibility(hasMembersItem.Members, name, filterOnBookmarks, bookmarks);
                    }

                    item.IsVisible = ShouldBeVisible(item, name, filterOnBookmarks, bookmarks) ? Visibility.Visible : Visibility.Collapsed;
                    item.Opacity = SetOpacity(item);
                }
            }
            catch (Exception e)
            {
                LogHelper.Log("Error during setting visibility", e);
            }

            return document;
        }

        /// <summary>
        /// Toggle visibility of the CodeNav margin
        /// </summary>
        /// <param name="row">the grid row of which the visibility will be toggled</param>
        /// <param name="condition">if condition is True visibility will be set to hidden</param>
        public static void SetMarginHeight(RowDefinition row, bool condition)
        {
            if (row == null) return;
            row.Height = condition ? new GridLength(0) : new GridLength(Settings.Default.Width);
        }

        /// <summary>
        /// Toggle visibility of the CodeNav margin
        /// </summary>
        /// <param name="row">the grid row of which the visibility will be toggled</param>
        /// <param name="document">the list of codeitems to determine if there is anything to show at all</param>
        public static void SetMarginHeight(RowDefinition row, List<CodeItem> document)
        {
            try
            {
                if (row == null) return;
                if (!Settings.Default.ShowMargin)
                {
                    row.Height = new GridLength(0);
                }
                else
                {
                    row.Height = IsEmpty(document) ? new GridLength(0) : new GridLength(0, GridUnitType.Star);
                }
            }
            catch (InvalidOperationException)
            {
                // Ignore if we are not allowed to set the width
            }
        }

        /// <summary>
        /// Toggle visibility of the CodeNav margin
        /// </summary>
        /// <param name="column">the grid column of which the visibility will be toggled</param>
        /// <param name="condition">if condition is True visibility will be set to hidden</param>
        public static void SetMarginWidth(ColumnDefinition column, bool condition)
        {
            if (column == null) return;
            column.Width = condition ? new GridLength(0) : new GridLength(Settings.Default.Width);
        }

        /// <summary>
        /// Toggle visibility of the CodeNav margin
        /// </summary>
        /// <param name="column">the grid column of which the visibility will be toggled</param>
        /// <param name="document">the list of codeitems to determine if there is anything to show at all</param>
        public static void SetMarginWidth(ColumnDefinition column, List<CodeItem> document)
        {
            try
            {
                if (column == null) return;
                if (!Settings.Default.ShowMargin)
                {
                    column.Width = new GridLength(0);
                }
                else
                {
                    column.Width = IsEmpty(document) ? new GridLength(0) : new GridLength(Settings.Default.Width);
                }
            }
            catch (InvalidOperationException)
            {
                // Ignore if we are not allowed to set the width
            }
        }

        public static bool IsEmpty(List<CodeItem> document)
        {
            if (!document.Any()) return true;

            var isEmpty = true;
            foreach (var item in document)
            {
                if (item is IMembers)
                {
                    isEmpty = !(item as IMembers).Members.Any();
                }
            }
            return isEmpty;
        }

        /// <summary>
        /// Set opacity of code item to value given in the filter window
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static double SetOpacity(CodeItem item)
        {
            var filterRule = GetFilterRule(item);

            if (filterRule != null)
            {
                return GetOpacityValue(filterRule.Opacity);
            }

            return 1.0;
        }

        /// <summary>
        /// Get opacity value from filter rule setting
        /// </summary>
        /// <param name="opacitySetting"></param>
        /// <returns></returns>
        private static double GetOpacityValue(string opacitySetting)
        {
            if (string.IsNullOrEmpty(opacitySetting)) return 1.0;

            double.TryParse(opacitySetting, out var opacity);

            if (opacity < 0 || opacity > 1) return 1.0;

            return opacity;
        }

        /// <summary>
        /// Determine if an item should be visible
        /// </summary>
        /// <param name="item">CodeItem that is checked</param>
        /// <param name="name">Text filter</param>
        /// <param name="filterOnBookmarks">Are we only showing bookmarks?</param>
        /// <param name="bookmarks">List of current bookmarks</param>
        /// <returns></returns>
        private static bool ShouldBeVisible(CodeItem item, string name = "",
            bool filterOnBookmarks = false, Dictionary<string, int> bookmarks = null)
        {
            var visible = true;

            var filterRule = GetFilterRule(item);

            if (filterRule != null && filterRule.Visible == false)
            {
                return false;
            }

            if (filterOnBookmarks)
            {
                visible = BookmarkHelper.IsBookmark(bookmarks, item);
            }

            if (!string.IsNullOrEmpty(name))
            {
                visible = visible && item.Name.Contains(name, StringComparison.OrdinalIgnoreCase);
            }

            // If an item has any visible members, it should be visible.
            // If an item does not have any visible members, hide it depending on an option
            if (item is IMembers hasMembersItem &&
                hasMembersItem?.Members != null)
            {
                if (hasMembersItem.Members.Any(m => m.IsVisible == Visibility.Visible))
                {
                    visible = true;
                }
                else if (!hasMembersItem.Members.Any(m => m.IsVisible == Visibility.Visible) && filterRule != null)
                {
                    visible = !filterRule.HideIfEmpty;
                }
            }

            return visible;
        }

        public static bool ShouldBeVisible(CodeItemKindEnum kind)
        {
            var visible = true;

            if (SettingsHelper.FilterRules != null)
            {
                var filterRule = SettingsHelper.FilterRules.LastOrDefault(f =>
                    (f.Kind == kind || f.Kind == CodeItemKindEnum.All));

                if (filterRule != null)
                {
                    visible = filterRule.Visible;
                }
            }

            return visible;
        }

        public static Visibility GetIgnoreVisibility(CodeItem item)
        {
            var filterRule = GetFilterRule(item);

            if (filterRule != null)
            {
                return filterRule.Ignore ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        private static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source != null && toCheck != null && source.IndexOf(toCheck, comp) >= 0;
        }

        private static FilterRule GetFilterRule(CodeItem item)
        {
            if (SettingsHelper.FilterRules == null) return null;

            var filterRule = SettingsHelper.FilterRules.LastOrDefault(f =>
                    (f.Access == item.Access || f.Access == CodeItemAccessEnum.All) &&
                    (f.Kind == item.Kind || f.Kind == CodeItemKindEnum.All));

            return filterRule;
        }
    }
}
