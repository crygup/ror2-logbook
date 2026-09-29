using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace LogbookMarkRead
{
    internal static class PageRead
    {
        internal static int MarkCategory(ViewablesCatalog.Node category, UserProfile profile)
            => Mark(category?.Descendants().Select(node => node.fullName) ?? Enumerable.Empty<string>(), profile);

        internal static int Mark(IEnumerable<string> names, UserProfile profile)
        {
            if (profile == null) return 0;
            int count = 0;
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name) || profile.HasViewedViewable(name)) continue;
                var node = ViewablesCatalog.FindNode(name);
                if (node == null || node.isFolder || !node.shouldShowUnviewed(profile)) continue;
                profile.MarkViewableAsViewed(name);
                count++;
            }
            return count;
        }
    }
}
