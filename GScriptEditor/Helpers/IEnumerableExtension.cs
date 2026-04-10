using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GScript.Editor.Helpers;

public static class IEnumerableExtension
{
    public static IEnumerable<T> IndirectInsert<T>(this IEnumerable<T> enumerable, T element)
    {
        if (!enumerable.Any())
            yield break;

        var enumerator = enumerable.GetEnumerator();

        enumerator.MoveNext();
        yield return enumerator.Current;

        while (enumerator.MoveNext())
        {
            yield return element;
            yield return enumerator.Current;
        }
    }

    public static IEnumerable<T> FlatIndirectInsert<T>(this IEnumerable<IEnumerable<T>> enumerables, T element)
    {
        if (!enumerables.Any())
            yield break;
        var enumerator = enumerables.GetEnumerator();
        enumerator.MoveNext();
        foreach (var item in enumerator.Current)
        {
            yield return item;
        }
        while (enumerator.MoveNext())
        {
            yield return element;
            foreach (var item in enumerator.Current)
            {
                yield return item;
            }
        }
    }

    public static IEnumerable<T> Flat<T>(this IEnumerable<IEnumerable<T>> enumerables)
    {
        foreach (var enumerable in enumerables)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }
}
