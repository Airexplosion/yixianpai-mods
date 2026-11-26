using System.Collections.Generic;
using Yx.Shared;

namespace YxCanju
{
    /// <summary>摆牌顺序不影响题目；编辑方、牌池、对手或任何对局属性变化都使旧结果失效。</summary>
    public static class PuzzleSignature
    {
        // Accept each already-applied target grid when validating placement of this
        // particular solution. All unrelated edits still invalidate its signature.
        public static void RestoreRecommendedGrids(Dictionary<string, object> live, Dictionary<string, object> original,
            Dictionary<string, object> recommended)
        {
            if (live == null || original == null || recommended == null) return;
            foreach (var grid in recommended)
            {
                object value;
                int current = live.TryGetValue(grid.Key, out value) ? System.Convert.ToInt32(value) : 0;
                if (current != System.Convert.ToInt32(grid.Value)) continue;
                object previous;
                if (original.TryGetValue(grid.Key, out previous)) live[grid.Key] = previous;
                else live.Remove(grid.Key);
            }
        }

        public static string Of(Dictionary<string, object> req)
        {
            var me = req["me"] as Dictionary<string, object>;
            var copy = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kv in me)
                if (kv.Key != "cards" && kv.Key != "initial") copy[kv.Key] = kv.Value;
            var pool = new List<int>();
            var cards = me["cards"] as List<object>;
            if (cards != null) for (int i = 0; i < cards.Count; i++) pool.Add((int)cards[i]);
            pool.Sort();
            var sorted = new List<object>();
            for (int i = 0; i < pool.Count; i++) sorted.Add(pool[i]);
            copy["pool"] = sorted;
            var sig = new Dictionary<string, object>();
            sig["round"] = req["round"];
            sig["deathFightMode"] = req.ContainsKey("deathFightMode") ? req["deathFightMode"] : "record";
            sig["allowRefine"] = !req.ContainsKey("allowRefine") || (bool)req["allowRefine"];
            sig["slots"] = req["slots"];
            sig["first"] = req.ContainsKey("first") ? req["first"] : "both";
            sig["weightByCurrentInitiative"] = req.ContainsKey("weightByCurrentInitiative") ? req["weightByCurrentInitiative"] : false;
            sig["currentOrderWeightPercent"] = req.ContainsKey("currentOrderWeightPercent") ? req["currentOrderWeightPercent"] : 50;
            sig["max"] = req["maxConsumeSustain"];
            sig["seed"] = req.ContainsKey("seed") ? req["seed"] : 1;
            sig["samples"] = req.ContainsKey("samples") ? req["samples"] : 3;
            sig["me"] = copy;
            sig["foe"] = req["foe"];
            sig["position"] = req["position"];
            return Json.Serialize(sig);
        }
    }
}
