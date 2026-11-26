using System.Collections.Generic;
using Proto;

namespace YxCanju
{
    internal static class PlayerSnapshot
    {
        internal static Dictionary<string, object> MeSide(BattlePlayerData me, BattlePlayerPrivateData priv, List<object> pool, List<object> initial)
        {
            var s = new Dictionary<string, object>();
            // 引擎开战时：生命上限 = 境界基础血 + lastRoundData.extraMaxHp（摆牌期我方的「本轮」值就是公开数据上的这份）。
            s["hp"] = me.extraMaxHp;
            s["level"] = (int)me.level;
            s["life"] = me.life;
            s["exp"] = me.exp;
            s["characterId"] = me.characterId;
            s["sect"] = (int)me.sect;
            s["career"] = (int)me.career;
            s["talents"] = Ints(me.talents);
            s["keyin"] = Ints(priv.keYinData != null ? priv.keYinData.usedCards : null);
            s["maxKeYin"] = priv.keYinData != null ? priv.keYinData.maxKeYin : 0;
            var fate = new List<object>();
            if (priv.fateStrategyData != null && priv.fateStrategyData.strategies != null)
                for (int i = 0; i < priv.fateStrategyData.strategies.Count; i++)
                {
                    SelectionData sd = priv.fateStrategyData.strategies[i];
                    if (sd != null && sd.selected != 0) fate.Add(sd.selected);
                }
            s["fate"] = fate;
            var xianmo = new List<object>();
            if (priv.xianMoData != null && priv.xianMoData.strategies != null)
                for (int i = 0; i < priv.xianMoData.strategies.Count; i++)
                {
                    SelectionData sd = priv.xianMoData.strategies[i];
                    if (sd != null && sd.selected != 0) xianmo.Add(sd.selected);
                }
            s["xianMoStrategies"] = xianmo;
            s["xianMoTempDatas"] = IntMap(priv.xianMoData != null ? priv.xianMoData.tempDatas : null);
            s["resonance"] = Resonance(priv.talentResonanceData);
            s["permanentBuffs"] = IntMap(me.permanentBuffTempDatas);
            s["talentTempDatas"] = IntMap(me.talentTempDatas);
            s["resonanceFlags"] = IntMap(me.resonanceTalentFlags);
            s["talentDatas"] = ParamMap(me.talentDatas);
            s["privateTalentDatas"] = ParamMap(priv.talentDatas);
            s["cards"] = pool;
            s["initial"] = initial;
            return s;
        }

        static int Resonance(TalentResonanceData d)
        {
            return d != null && d.selectionData != null ? d.selectionData.selected : 0;
        }

        static List<object> Ints(List<int> src)
        {
            var r = new List<object>();
            if (src == null) return r;
            for (int i = 0; i < src.Count; i++) r.Add(src[i]);
            return r;
        }

        internal static Dictionary<string, object> IntMap(Dictionary<int, int> src)
        {
            var r = new Dictionary<string, object>();
            if (src == null) return r;
            foreach (KeyValuePair<int, int> kv in src) r[kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = kv.Value;
            return r;
        }

        static Dictionary<string, object> ParamMap(Dictionary<int, BattleTalentData> src)
        {
            var r = new Dictionary<string, object>();
            if (src == null) return r;
            foreach (KeyValuePair<int, BattleTalentData> kv in src)
                r[kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture)] = Ints(kv.Value != null ? kv.Value.commonParams : null);
            return r;
        }
    }
}

