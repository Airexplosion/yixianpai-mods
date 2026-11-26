using System.Collections.Generic;

namespace YxCanju
{
    public static class PlacementSignature
    {
        public static string AfterReturn(Dictionary<string, object> request, int id, int linkedId)
        {
            var me = new Dictionary<string, object>((Dictionary<string, object>)request["me"]);
            var pool = new List<object>((List<object>)me["cards"]);
            int index = pool.IndexOf(id);
            if (index >= 0) pool[index] = linkedId;
            me["cards"] = pool;
            return PuzzleSignature.Of(new Dictionary<string, object>(request) { ["me"] = me });
        }
    }
}
