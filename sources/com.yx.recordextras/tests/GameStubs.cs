// Minimal data/config API for testing outside Unity. The release build uses
// the actual game DLL, not these types. Config values here are synthetic.
using System.Collections.Generic;

namespace Proto
{
    public class PlayerData { public BattlePlayerData publicData = new BattlePlayerData(); }
    public class BattlePlayerData
    {
        public int exp;
        public List<int> talents = new List<int>();
        public Dictionary<int, int> permanentBuffTempDatas = new Dictionary<int, int>();
        public BattlePlayerLastRoundData lastRoundData = new BattlePlayerLastRoundData();
    }
    public class BattlePlayerLastRoundData
    {
        public int exp;
        public List<int> talents = new List<int>();
        public List<int> handCards = new List<int>();
        public List<int> usedCards = new List<int>();
        public List<int> usedKeYinCards = new List<int>();
        public Dictionary<int, int> permanentBuffTempDatas = new Dictionary<int, int>();
    }
    public class BattleTempData
    {
        public PlayerData playerData;
        public List<BattleCardInfo> battleCardInfos = new List<BattleCardInfo>();
        public List<int> battleKeYinCards = new List<int>();
    }
    public class BattleCardInfo { public int id; }
    public class TalentConfig { public List<int> otherParams = new List<int>(); }
    public class CardConfig { public string name; }
    public class KeYinCardConfig { public List<int> otherParams = new List<int>(); }
}

public static class ConfigManager
{
    public static readonly Dictionary<int, Proto.CardConfig> cardConfigDict = new Dictionary<int, Proto.CardConfig>
    {
        { 1, new Proto.CardConfig { name = "金灵甲" } },
        { 2, new Proto.CardConfig { name = "金灵乙" } },
        { 3, new Proto.CardConfig { name = "木灵甲" } },
        { 4, new Proto.CardConfig { name = "水灵甲" } },
        { 5, new Proto.CardConfig { name = "火灵甲" } },
        { 6, new Proto.CardConfig { name = "土灵甲" } },
        { 7, new Proto.CardConfig { name = "普通牌" } },
        { 8, new Proto.CardConfig { name = "金灵木灵" } }
    };
    public static Proto.TalentConfig GetTalentConfig(int id)
    {
        if (id == 128) return new Proto.TalentConfig { otherParams = new List<int> { 3 } };
        if (id == 10128) return new Proto.TalentConfig { otherParams = new List<int> { 5 } };
        return null;
    }
}
public static class KeYinCardFactory
{
    public static Proto.KeYinCardConfig FindCardConfig(int id)
    {
        if (id == 40042) return new Proto.KeYinCardConfig { otherParams = new List<int> { 2 } };
        if (id == 50042) return new Proto.KeYinCardConfig { otherParams = new List<int> { 4 } };
        return null;
    }
}
