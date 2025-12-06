using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace WinFinder {
    public class NaturalStringComparer : IComparer<string>, IComparer {
        public int Compare(string x, string y) {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var regex = new Regex(@"(\d+|\D+)");
            var xParts = regex.Split(x);
            var yParts = regex.Split(y);
            int maxLength = Math.Max(xParts.Length, yParts.Length);

            for (int i = 0; i < maxLength; i++) {
                string xPart = i < xParts.Length ? xParts[i] : string.Empty;
                string yPart = i < yParts.Length ? yParts[i] : string.Empty;

                if (int.TryParse(xPart, out int xNum) && int.TryParse(yPart, out int yNum)) {
                    int result = xNum.CompareTo(yNum);
                    if (result != 0) return result;
                } else {
                    int result = string.Compare(xPart, yPart, StringComparison.OrdinalIgnoreCase);
                    if (result != 0) return result;
                }
            }
            return 0;
        }

        public int Compare(object x, object y) {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            string xStr = x?.ToString();
            string yStr = y?.ToString();

            return Compare(xStr, yStr);
        }

        public static IComparer CreateComparerForProperty(string propertyName, ListSortDirection sortDirection) {
            return new NaturalStringComparerForProperty(propertyName, sortDirection);
        }

        private class NaturalStringComparerForProperty : IComparer {
            private readonly string _propertyName;
            private readonly ListSortDirection _sortDirection;

            public NaturalStringComparerForProperty(string propertyName, ListSortDirection sortDirection) {
                _propertyName = propertyName;
                _sortDirection = sortDirection;
            }

            public int Compare(object x, object y) {
                if (x == null || y == null) {
                    if (x == null && y == null) return 0;
                    return x == null ? -1 : 1;
                }

                var xPropertyValue = GetPropertyValue(x, _propertyName);
                var yPropertyValue = GetPropertyValue(y, _propertyName);

                int comparisonResult = new NaturalStringComparer().Compare(xPropertyValue, yPropertyValue);
                return _sortDirection == ListSortDirection.Ascending ? comparisonResult : -comparisonResult;
            }

            private string GetPropertyValue(object obj, string propertyName) {
                var property = obj.GetType().GetProperty(propertyName);
                if (property != null) {
                    var value = property.GetValue(obj);
                    return value?.ToString() ?? string.Empty;
                }
                return string.Empty;
            }
        }
    }
}
