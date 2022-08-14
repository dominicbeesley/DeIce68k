using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisassShared
{
    public static class LinqExtend
    {
        public static IEnumerable<Tuple<int, int>> GroupConsecutive(this IEnumerable<int> source)
        {
            using (var e = source.GetEnumerator())
            {
                for (bool more = e.MoveNext(); more;)
                {
                    int first = e.Current, last = first, next;
                    while ((more = e.MoveNext()) && (next = e.Current) > last && next - last == 1)
                        last = next;
                    yield return new Tuple<int, int>(first, last);
                }
            }
        }

        public static IEnumerable<T> Intersperse<T>(this IEnumerable<T> source, T value)
        {
            bool first = true;
            foreach (T item in source)
            {
                if (first) { first = false; }
                else { yield return value; }
                yield return item;
            }
        }


    }
}
