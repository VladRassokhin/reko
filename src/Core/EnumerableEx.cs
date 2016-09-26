﻿using Reko.Core.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Reko.Core
{
    /// <summary>
    /// Extension methods for Enumerable classes
    /// </summary>
    public static class EnumerableEx
    {
        public static SortedList<TKey, TSource> ToSortedList<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
        {
            SortedList<TKey, TSource> list = new SortedList<TKey, TSource>();
            foreach (TSource item in source)
            {
                list.Add(keySelector(item), item);
            }
            return list;
        }

        public static SortedList<TKey, TElement> ToSortedList<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector)
        {
            SortedList<TKey, TElement> list = new SortedList<TKey, TElement>();
            foreach (TSource item in source)
            {
                list.Add(keySelector(item), elementSelector(item));
            }
            return list;
        }

        public static SortedList<TKey, TSource> ToSortedList<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IComparer<TKey> comparer)
        {
            SortedList<TKey, TSource> list = new SortedList<TKey, TSource>(comparer);
            foreach (TSource item in source)
            {
                list.Add(keySelector(item), item);
            }
            return list;
        }

        public static SortedList<TKey, TValue> ToSortedList<TSource, TKey, TValue>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TValue> valueSelector,
            IComparer<TKey> comparer)
        {
            SortedList<TKey, TValue> list = new SortedList<TKey, TValue>(comparer);
            foreach (TSource item in source)
            {
                list.Add(keySelector(item), valueSelector(item));
            }
            return list;
        }

        public static HashSet<TElement> ToHashSet<TElement>(
            this IEnumerable<TElement> source)
        {
            return new HashSet<TElement>(source);
        }

        public static SortedSet<TElement> ToSortedSet<TElement>(
            this IEnumerable<TElement> source)
        {
            SortedSet<TElement> set = new SortedSet<TElement>();
            foreach (var element in source)
            {
                set.Add(element);
            }
            return set;
        }

        public static IEnumerable<IEnumerable<T>> Chunks<T>(
            this IEnumerable<T> enumerable,
            int chunkSize)
        {
            if (chunkSize < 1) throw new ArgumentException("chunkSize must be positive.");

            using (var e = enumerable.GetEnumerator())
            {
                while (e.MoveNext())
                {
                    var remaining = chunkSize;    // elements remaining in the current chunk
                    var innerMoveNext = new Func<bool>(() => --remaining > 0 && e.MoveNext());

                    yield return e.GetChunk(innerMoveNext);
                    while (innerMoveNext()) {/* discard elements skipped by inner iterator */}
                }
            }
        }

        private static IEnumerable<T> GetChunk<T>(this IEnumerator<T> e,
                                                  Func<bool> innerMoveNext)
        {
            do yield return e.Current;
            while (innerMoveNext());
        }


        public static IEnumerable<TResult> ZipMany<TSource, TResult>(
            IEnumerable<IEnumerable<TSource>> source,
            Func<IEnumerable<TSource>, TResult> selector)
        {
            // ToList is necessary to avoid deferred execution
            var enumerators = source.Select(seq => seq.GetEnumerator()).ToList();
            try
            {
                while (true)
                {
                    bool moved = false;
                    foreach (var e in enumerators)
                    {
                        bool b = e.MoveNext();
                        if (!b) yield break;
                        moved = true;
                    }
                    if (!moved)
                        yield break;
                    // Again, ToList (or ToArray) is necessary to avoid deferred execution
                    yield return selector(enumerators.Select(e => e.Current).ToList());
                }
            }
            finally
            {
                foreach (var e in enumerators)
                    e.Dispose();
            }
        }
    }
}
