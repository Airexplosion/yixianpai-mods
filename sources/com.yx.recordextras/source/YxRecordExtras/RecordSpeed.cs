using System.Collections.Generic;
using System.Globalization;
using Proto;

namespace YxRecordExtras
{
    internal static class RecordSpeed
    {
        // Mirror BattleCharacterUI.CalExtraSpeed using the recorded pre-battle
        // snapshot. Conditional bonuses are already resolved into this map;
        // recalculating them from the current round/hand would double-count them.
        public static int Extra(PlayerData player)
        {
            BattlePlayerLastRoundData snapshot = player.publicData.lastRoundData;
            int speed = Buff(snapshot.permanentBuffTempDatas, 10012)
                + Buff(snapshot.permanentBuffTempDatas, 10045)
                + Buff(snapshot.permanentBuffTempDatas, 10056)
                + Buff(snapshot.permanentBuffTempDatas, 369);

            List<int> talents = snapshot.talents;
            if (talents != null)
            {
                for (int i = 0; i < talents.Count; i++)
                {
                    int id = talents[i];
                    if (id % 10000 == 128 &&
                        (talents.Contains(278) || IsSameWuxing(snapshot.usedCards)))
                    {
                        TalentConfig config = ConfigManager.GetTalentConfig(id);
                        if (config != null && config.otherParams != null && config.otherParams.Count > 0)
                            speed += config.otherParams[0];
                    }
                }
            }

            List<int> keyin = snapshot.usedKeYinCards;
            if (keyin != null)
            {
                for (int i = 0; i < keyin.Count; i++)
                {
                    if (keyin[i] % 10000 != 42) continue;
                    KeYinCardConfig config = KeYinCardFactory.FindCardConfig(keyin[i]);
                    if (config != null && config.otherParams != null && config.otherParams.Count > 0)
                        speed += config.otherParams[0];
                }
            }
            return speed;
        }

        private static int Buff(Dictionary<int, int> buffs, int id)
        {
            int value;
            return buffs != null && buffs.TryGetValue(id, out value) ? value : 0;
        }

        private static bool IsSameWuxing(List<int> cards)
        {
            if (cards == null) return false;
            string[] elements = { "金灵", "木灵", "水灵", "火灵", "土灵" };
            string element = "";
            for (int i = 0; i < cards.Count; i++)
            {
                CardConfig card;
                if (!ConfigManager.cardConfigDict.TryGetValue(cards[i], out card) ||
                    card == null || string.IsNullOrEmpty(card.name)) continue;
                for (int j = 0; j < elements.Length; j++)
                {
                    if (!card.name.Contains(elements[j])) continue;
                    if (element == "")
                    {
                        element = elements[j];
                        break;
                    }
                    if (element != elements[j]) return false;
                }
            }
            return element != "";
        }

        public static string Format(string originalLabel, PlayerData player)
        {
            string text = originalLabel ?? "";
            int colon = text.LastIndexOf('：');
            if (colon < 0) colon = text.LastIndexOf(':');
            string prefix = colon < 0 ? "修为：" : text.Substring(0, colon + 1);
            int exp = player.publicData.lastRoundData.exp;
            int speed = Extra(player);
            string expText = exp.ToString(CultureInfo.InvariantCulture);
            string speedText = speed.ToString(CultureInfo.InvariantCulture);
            string totalText = (exp + speed).ToString(CultureInfo.InvariantCulture);
            string sign = speed < 0 ? "" : "+";
            return prefix + expText + sign + speedText + "（" + totalText + "）";
        }
    }
}
