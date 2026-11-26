using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.Shared;

namespace YxCanju
{
    public static class SolutionCards
    {
        public static int[] Get(Dictionary<string, object> report, int index)
        {
            if (report == null || index < 0) return null;
            List<object> best = Json.GetArray(report, "best");
            if (best == null || index >= best.Count) return null;
            var entry = best[index] as Dictionary<string, object>;
            List<object> cards = entry == null ? null : Json.GetArray(entry, "cards");
            if (cards == null || cards.Count == 0) return null;
            var target = new int[cards.Count];
            for (int i = 0; i < cards.Count; i++) target[i] = (int)Convert.ToDouble(cards[i], CultureInfo.InvariantCulture);
            return target;
        }
    }
}
