using System.Runtime.CompilerServices;
using UnityEngine;

public static class ObjectUtility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispose<T>(ref T disposable) where T : System.IDisposable
    {
        if (disposable == null)
            return;

        disposable.Dispose();
        disposable = default;
    }
}
