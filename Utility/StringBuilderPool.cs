using System.Text;
using UnityEngine.Pool;

namespace GoodAI.Core.Text
{
    public static class StringBuilderPool
    {
        // private members

        static readonly ObjectPool<StringBuilder> s_pool = new(
            () => new(),
            null,
            sb => sb.Clear()
        );

        // public methods

        public static StringBuilder Get()                    => s_pool.Get();
        public static void          Return(StringBuilder sb) => s_pool.Release(sb);

        public static string GetStringAndReturn(StringBuilder sb)
        {
            if (sb == null)
                return "";

            var result = sb.ToString();

            s_pool.Release(sb);

            return result;
        }

        public static void Clear() => s_pool.Clear();
    }
}
