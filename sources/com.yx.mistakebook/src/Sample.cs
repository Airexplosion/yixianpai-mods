using System.Collections.Generic;

namespace YxMistakeBook
{
    internal static class Sample
    {
        public const string Key = "sample/cloud-swords";

        // A constructed two-slot exercise, not a player's record. Checked against the packaged native engine.
        public static Exercise Create()
        {
            var me = Side(1000001, new int[] { 19, 3, 8 });
            me["initial"] = Exercise.ListOf(new int[] { 19, 3 });
            me["handCards"] = Exercise.ListOf(new int[] { 8 });
            var request = new Dictionary<string, object> {
                { "me", me }, { "foe", Side(1000002, new int[] { 19, 8 }) }, { "round", 2 },
                { "first", "foe" }, { "slots", 2 }, { "maxConsumeSustain", 2 }
            };
            var e = Exercise.Create(Key, request);
            e.Data["answer"] = Exercise.ListOf(new int[] { 8, 3 });
            return e;
        }

        static Dictionary<string, object> Side(int character, int[] cards)
        {
            return new Dictionary<string, object> {
                { "characterId", character }, { "sect", 1 }, { "level", 1 }, { "life", 100 },
                { "hp", 0 }, { "exp", 0 }, { "cards", Exercise.ListOf(cards) }, { "handCards", new List<object>() }
            };
        }
    }
}
