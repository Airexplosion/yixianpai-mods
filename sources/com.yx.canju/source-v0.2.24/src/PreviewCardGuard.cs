using System.Collections.Generic;

namespace YxCanju
{
    /// <summary>The reveal pack has local effects which the native solver does not implement yet.</summary>
    public static class PreviewCardGuard
    {
        public static bool IsPreview(int id)
        {
            int rank = id / 10000 % 100;
            int baseId = id - rank * 10000;
            return rank >= 0 && rank <= 2 && ((baseId >= 7009001 && baseId <= 7009007)
                || (baseId >= 10009001 && baseId <= 10009008));
        }
        public static bool Contains(List<int> cards)
        {
            if (cards == null) return false;
            for (int i = 0; i < cards.Count; i++) if (IsPreview(cards[i])) return true;
            return false;
        }
        public static bool Contains(List<object> cards)
        {
            if (cards == null) return false;
            for (int i = 0; i < cards.Count; i++) if (cards[i] is int && IsPreview((int)cards[i])) return true;
            return false;
        }
    }
}
