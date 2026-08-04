using System;
using System.Collections.Generic;

namespace InsightCanvas
{
    /// <summary>Portable owner bookkeeping shared by map overlays and the core harness.</summary>
    internal static class InsightOverlayOwnership
    {
        internal static int ClearOwner<T>(IList<T> entries, object ownerToken, Func<T, object> tokenSelector)
        {
            if (entries == null || tokenSelector == null) return 0;
            int removed = 0;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                if (!object.ReferenceEquals(tokenSelector(entries[index]), ownerToken)) continue;
                entries.RemoveAt(index);
                removed++;
            }
            return removed;
        }
    }
}
