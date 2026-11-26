using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;

namespace YxCanju
{
    /// <summary>Read pre-battle snapshots without selecting a round or changing game state.</summary>
    public static class RecordPuzzleReader
    {
        // Verified against the current RecordDetailPanel's own review compatibility check.
        public const string RecordVersion = "001.0008.0007";
        public static Dictionary<string, object> ReadRound(BattleResult result, RecentBattleInfo info, out string error)
        {
            if (info == null || info.roundStats == null || info.roundStats.Count == 0)
                return Fail("战绩缺少轮次数据", out error);
            if (info.version != RecordVersion)
                return Fail("战绩版本不匹配：" + info.version + "；当前支持 " + RecordVersion, out error);
            BattleResult first = info.roundStats[0];
            if (first == null || first.p1 == null || first.p1.publicData == null || first.p2 == null || first.p2.publicData == null)
                return Fail("战绩缺少首轮玩家数据", out error);
            return Read(result, info.GetRoundUid(), out error);
        }

        public static Dictionary<string, object> Read(BattleResult result, string uid, out string error)
        {
            error = null;
            if (result != null && result.gameMode != GameMode.NormalMode && result.gameMode != GameMode.PracticeMode
                && result.gameMode != GameMode.RankMode && result.gameMode != GameMode.CupMode
                && result.gameMode != GameMode.SilenceMode && result.gameMode != GameMode.ReviewMode)
                return Fail("当前求解器未支持本轮特殊模式", out error);
            if (result == null || result.round < 1 || !Valid(result.p1) || !Valid(result.p2))
                return Fail("本轮缺少完整战斗快照", out error);
            PlayerData me = result.p1.publicData.uid == uid ? result.p1
                : result.p2.publicData.uid == uid ? result.p2 : null;
            if (me == null) return Fail("本轮没有我方参战数据", out error);
            PlayerData foe = object.ReferenceEquals(me, result.p1) ? result.p2 : result.p1;
            if (result.firstPlayerId != me.publicData.uid && result.firstPlayerId != foe.publicData.uid)
                return Fail("本轮缺少先后手记录", out error);
            var last = me.publicData.lastRoundData;
            int slots = Slots(me);
            if (slots < 1 || slots > 10 || last.usedCards.Count > slots
                || Slots(foe) < 1 || Slots(foe) > 10 || foe.publicData.lastRoundData.usedCards.Count > Slots(foe))
                return Fail("本轮牌位数据不完整", out error);
            var initial = new List<object>();
            var pool = new List<object>();
            for (int i = 0; i < slots; i++)
            {
                int id = i < last.usedCards.Count ? last.usedCards[i] : 0;
                initial.Add(id);
                if (id != 0) pool.Add(id);
            }
            for (int i = 0; i < last.handCards.Count; i++)
                if (last.handCards[i] != 0) pool.Add(last.handCards[i]);
            if (PreviewCardGuard.Contains(pool) || PreviewCardGuard.Contains(foe.publicData.lastRoundData.usedCards)
                || PreviewCardGuard.Contains(foe.publicData.lastRoundData.handCards))
                return Fail("含尚未支持的预览牌", out error);
            var mySide = Side(me);
            mySide["cards"] = pool;
            mySide["initial"] = initial;
            var foeSide = Side(foe);
            var foeCards = foeSide["cards"] as List<object>;
            while (foeCards.Count < Slots(foe)) foeCards.Add(0);
            int cap = 2;
            if (last.usedKeYinCards != null)
                for (int i = 0; i < last.usedKeYinCards.Count; i++)
                    if (last.usedKeYinCards[i] % 10000 == 64) cap++;
            int extra;
            if (last.permanentBuffTempDatas != null
                && last.permanentBuffTempDatas.TryGetValue((int)BuffType.JiaChiXuXiaoHaoWei, out extra)) cap += extra;
            var request = new Dictionary<string, object>();
            request["round"] = result.round;
            request["slots"] = slots;
            request["first"] = "reachable";
            request["maxConsumeSustain"] = Math.Max(0, cap);
            request["me"] = mySide;
            request["foe"] = foeSide;
            return request;
        }

        static Dictionary<string, object> Side(PlayerData player)
        {
            var side = SnapshotSide(player.publicData);
            side["maxKeYin"] = player.privateData.keYinData != null ? player.privateData.keYinData.maxKeYin : 0;
            var temp = new Dictionary<string, object>();
            if (player.privateData.xianMoData != null && player.privateData.xianMoData.tempDatas != null)
                foreach (KeyValuePair<int, int> entry in player.privateData.xianMoData.tempDatas)
                    temp[entry.Key.ToString(CultureInfo.InvariantCulture)] = entry.Value;
            side["xianMoTempDatas"] = temp;
            return side;
        }

        static bool Valid(PlayerData player)
        {
            return player != null && player.publicData != null && player.privateData != null
                && !string.IsNullOrEmpty(player.publicData.uid) && player.publicData.characterId > 0
                && player.publicData.lastRoundData != null && player.publicData.lastRoundData.usedCards != null
                && player.publicData.lastRoundData.handCards != null;
        }

        static int Slots(PlayerData player)
        {
            int slots = player.publicData.lastRoundData.unlockGrids;
            return slots > 0 ? slots : player.privateData.unlockGrids;
        }

        static Dictionary<string, object> Fail(string why, out string error) { error = why; return null; }
        internal static Dictionary<string, object> SnapshotSide(BattlePlayerData opp)
        {
            BattlePlayerLastRoundData lrd = opp.lastRoundData;
            var s = new Dictionary<string, object>();
            s["hp"] = lrd != null ? lrd.extraMaxHp : opp.extraMaxHp;
            s["level"] = (int)opp.level;
            s["life"] = lrd != null ? lrd.life : opp.life;
            s["exp"] = lrd != null ? lrd.exp : opp.exp;
            s["characterId"] = opp.characterId;
            s["sect"] = (int)opp.sect;
            s["career"] = (int)opp.career;
            s["resonanceFlags"] = IntMap(opp.resonanceTalentFlags);
            if (lrd == null)
            {
                s["talents"] = Ints(opp.talents);
                s["cards"] = new List<object>();
                return s;
            }
            s["talents"] = Ints(lrd.talents);
            s["keyin"] = Ints(lrd.usedKeYinCards);
            s["fate"] = Ints(lrd.fateStrategies);
            s["xianMoStrategies"] = Ints(lrd.xianMoStrategies);
            s["resonance"] = Resonance(lrd.talentResonanceData);
            s["permanentBuffs"] = IntMap(lrd.permanentBuffTempDatas);
            s["talentTempDatas"] = IntMap(lrd.talentTempDatas);
            s["talentDatas"] = ParamMap(lrd.talentDatas);
            s["privateTalentDatas"] = ParamMap(lrd.privateTalentDatas);
            s["handCards"] = Ints(lrd.handCards);
            s["cards"] = Ints(lrd.usedCards);
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

        static Dictionary<string, object> IntMap(Dictionary<int, int> src)
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
