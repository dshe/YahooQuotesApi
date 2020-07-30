﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace YahooQuotesApi
{
    internal static class ExtensionMethods
    {
        internal static string ToPascal(this string source)
        {
            if (source.Count() <= 1)
                return source.ToUpper();
            return source.Substring(0, 1).ToUpper() + source.Substring(1);
        }

        internal static string Name<T>(this T source) where T : Enum
        {
            string name = source.ToString();
            if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr && attr.IsValueSetExplicitly)
                name = attr.Value;
            return name;
        }

        internal static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
            source.Where(item => item != null).Cast<T>();

        internal static IEnumerable<T> Unique<T,TKey>(this IEnumerable<T> source, Func<T, TKey> getKey)
        {
            var keys = new HashSet<TKey>();
            return source.Where(item => keys.Add(getKey(item)));
        }

        internal static IEnumerable<T> Append<T>(this IEnumerable<T> source, T value)
        {
            foreach (var item in source)
                yield return item;
            yield return value;
        }

        internal static string CheckSymbol(this string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException(nameof(symbol));
            if (symbol.Any(char.IsWhiteSpace))
                throw new ArgumentException($"Symbol: '{symbol}'.");
            return symbol.ToUpper(); // for simplicity
        }
    }
}
