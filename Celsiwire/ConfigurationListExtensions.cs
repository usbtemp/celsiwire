using System.Collections.Generic;

namespace Configuration
{
    public class IEnabled
    {
        public bool enabled;
    }

    /*
     * Razširitev seznama List, da podpira stanje: omogočen ali ne
     */
    public static class ListExtensions
    {
        public static void Update<T>(this List<T> list, T obj, bool value) where T : IEnabled
        {
            if (list.Contains(obj))
            {
                list[list.IndexOf(obj)].enabled = value;
            }
        }

        public static T ContainsT<T>(this List<T> list, T obj)
        {
            return list.Contains(obj) ? list[list.IndexOf(obj)] : default(T);
        }

        public static void AppendIfNot<T>(this List<T> list, ref T obj)
        {
            /* Če objekt obstaja, ga vrne, sicer doda podanega. */
            if (list.Contains(obj))
            {
                int index = list.IndexOf(obj);
                obj = list[index];
            }
            else
            {
                list.Add(obj);
            }
        }

        public static bool AppendIfNot<T>(this List<T> list, T obj)
        {
            if (!list.Contains(obj))
            {
                list.Add(obj);
                return true;
            }
            return false;
        }
    }
}
