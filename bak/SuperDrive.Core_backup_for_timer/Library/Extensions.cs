using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace SuperDrive.Core.Library
{
    public static class Extentions
    {
        public static TVal GetByID<TKey, TVal>(this Dictionary<TKey, TVal> dic, TKey key) where TVal : class
        {
            if (key == null) return null;

            return dic.ContainsKey(key) ? dic[key] : null;
        }

        public static void ForEach<T>(this ICollection<T> dic,Action<T> action)
        {
            Contract.Requires(dic != null);
            Contract.Requires(action != null);

            foreach (var item in dic)
            {
                action.Invoke(item);
            }
        }

    }
}
