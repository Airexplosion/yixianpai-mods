using System;
using System.Collections.Generic;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Game;

namespace YxCanju
{
    /// <summary>
    /// 从游戏读残局局面，组成求解请求（字段与 sim 的 PuzzleRequest / OracleSide 一一对应）。
    /// 双方对局数据取「战斗引擎开战时实际读的位置」—— 与 arenaprobe B7 写入的字段相同，那套输入已在真机对拍里验证：
    ///   我方（摆牌期实时）：公开数据 + 私有数据（仙命 / 共鸣 / 刻印 / 私有天赋数据在私有数据里）；
    ///   对方：公开数据的上一轮快照 lastRoundData（游戏自己的「对手信息」面板也只读这份）。
    /// 我方牌：牌桌（每个已解锁格）+ 手牌 = 牌池；牌桌原顺序作局部搜索起点。只读，不改任何状态。
    /// </summary>
    public static class PuzzleReader
    {
        /// <summary>
        /// 仅允许原生残局 / 复盘，以及已登记且离线保护有效的练习场；PvP 与普通练习模式不可用。
        /// </summary>
        public static bool CanSolve(ModContext ctx, BattleManager bm)
        {
            if (bm == null || bm.currentGameStatus == null || bm.currentScene != SceneType.修炼阶段
                || SceneLoader.isLoading || SceneLoader.currentSceneName != "Battle") return false;
            if (bm.defaultBattleExecuter != null && bm.defaultBattleExecuter.isExecuting) return false;
            if (bm.gameMode == GameMode.ReviewMode) return true;
            if (bm.gameMode != GameMode.PracticeMode) return false;
            var client = GameClientUtil.client;
            if (client == null || client.isInRoom || client.needReconnectToRoom) return false;
            LocalPractice arena = ctx.Services.Get<LocalPractice>();
            return arena != null && arena.IsReady;
        }

        /// <summary>
        /// 局面签名：回合、格数、我方牌池（排序后，和摆放顺序无关）、双方对局数据、对方牌桌。
        /// 签名不变 = 同一个题，直接复用上次的结果（摆牌改的是顺序，不改签名）。
        /// </summary>
        public static string Signature(Dictionary<string, object> req)
        {
            return PuzzleSignature.Of(req);
        }

        /// <summary>读局面组成请求；读不到（不在复盘模式、不在摆牌界面…）时返回 null 并给出原因。</summary>
        public static Dictionary<string, object> Read(ModContext ctx, out string error)
        {
            error = null;
            BattleManager bm = BattleManager.Instance;
            if (bm == null || bm.currentGameStatus == null) { error = "不在对局中"; return null; }
            if (!CanSolve(ctx, bm)) { error = "只能在残局 / 复盘 / 本地练习场的备战界面使用"; return null; }
            GameStatus gs = bm.currentGameStatus;
            BattlePlayerData me = GameStatusExtension.GetMainPlayerData(gs);
            BattlePlayerPrivateData priv = gs.playerPrivateData;
            if (me == null || priv == null) { error = "没有我方数据"; return null; }
            BattlePlayerData opp = FindOpponent(gs, me);
            if (opp == null) { error = "没有对方数据"; return null; }

            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            CardPanel cp = bp != null ? bp.FindILRSubPanel<CardPanel>() : null;
            if (cp == null) { error = "不在摆牌界面"; return null; }
            if (cp.isDealingCards) { error = "正在发牌，请稍候"; return null; }
            // 炼化等操作先改备战面板持有的数据，不能只读尚未刷新的 GameStatus。
            if (bp.readyLayer != null && bp.readyLayer.playerSelfInfoItem != null
                && bp.readyLayer.playerSelfInfoItem.battlePlayerData != null)
                me = bp.readyLayer.playerSelfInfoItem.battlePlayerData;

            var pool = new List<object>();
            var initial = new List<object>();
            List<CardGrid> grids = cp.GetCardGrids();
            int slots = 0;
            if (grids != null)
            {
                for (int i = 0; i < grids.Count; i++)
                {
                    CardGrid g = grids[i];
                    if (g == null || !g.unlocked) continue;
                    slots++;
                    CardItem item = g.GetCard();
                    int id = item != null && item.cardInfo != null ? item.cardInfo.id : 0;
                    initial.Add(id);
                    if (id != 0) pool.Add(id);
                }
            }
            List<CardItem> hand = cp.GetHandCards();
            if (hand != null)
                for (int i = 0; i < hand.Count; i++)
                    if (hand[i] != null && hand[i].cardInfo != null && hand[i].cardInfo.id != 0) pool.Add(hand[i].cardInfo.id);
            if (slots == 0) { error = "没有已解锁的格子"; return null; }

            if (PreviewCardGuard.Contains(pool) || (opp.lastRoundData != null
                && (PreviewCardGuard.Contains(opp.lastRoundData.usedCards) || PreviewCardGuard.Contains(opp.lastRoundData.handCards))))
            {
                error = "局面含练习场新卡预览牌，当前求解器尚未实现这些效果；请手动试打，或移除双方手牌与牌桌里的预览牌";
                return null;
            }

            var req = new Dictionary<string, object>();
            req["round"] = gs.round;
            req["slots"] = slots;
            // Evaluate the actual preparations, including cards removed from the original board.
            req["first"] = "reachable";
            req["deathFightMode"] = "auto";
            req["allowRefine"] = !gs.ForbidRefineCard();
            req["weightByCurrentInitiative"] = bm.gameMode == GameMode.ReviewMode;
            req["deduplicateResults"] = true;
            req["maxConsumeSustain"] = cp.Get_MAX_CONSUMED_CONTINUOUS_CARD_COUNT();
            req["me"] = MeSide(me, priv, pool, initial);
            req["foe"] = FoeSide(opp);
            LocalPractice arena = ctx.Services.Get<LocalPractice>();
            var foeSide = req["foe"] as Dictionary<string, object>;
            if (bm.gameMode == GameMode.PracticeMode && arena != null && arena.IsReady)
            {
                string strategyJson = arena.ReadOpponentStrategyData();
                if (strategyJson != null)
                {
                    var strategyData = Yx.Shared.Json.ParseObject(strategyJson);
                    req["deathFightMode"] = Yx.Shared.Json.GetString(strategyData, "deathFightMode", "auto");
                    if (strategyData.ContainsKey("selfDragonSoulHp"))
                        ((Dictionary<string, object>)req["me"])["dragonSoulHp"] = strategyData["selfDragonSoulHp"];
                    foreach (KeyValuePair<string, object> kv in strategyData)
                        if (kv.Key == "xianMoStrategies" || kv.Key == "xianMoTempDatas" || kv.Key == "dragonSoulHp")
                            foeSide[kv.Key] = kv.Value;
                }
            }
            else if (bm.gameMode == GameMode.ReviewMode)
            {
                var data = GameStatusExtension.GetPlayerPrivateData(gs, opp.uid);
                if (data == null || data.uid != opp.uid)
                {
                    data = OpponentPrivate(BattleManager.currentBattleResult, gs.round, opp, false);
                    if (data == null && BattleManager.backType == BattleBackType.战绩详情面板)
                        data = OpponentPrivate(RecordDetailPanel.battleResult, gs.round, opp, true);
                }
                if (data != null && data.xianMoData != null)
                    foeSide["xianMoTempDatas"] = IntMap(data.xianMoData.tempDatas);
                else if (opp.lastRoundData != null && opp.lastRoundData.xianMoStrategies != null)
                    foreach (int id in opp.lastRoundData.xianMoStrategies)
                        if (id == 5001 || id == 5009 || (id >= 13001 && id <= 13019))
                        {
                            error = "缺少对手刻纹格位，请从这轮战绩详情重新进入复盘";
                            return null;
                        }
            }
            req["position"] = bm.gameMode == GameMode.PracticeMode && arena != null
                ? "arena:" + arena.Position : "review:" + me.uid;
            return req;
        }

        // A review room can replace the two original player UIDs. Match the selected
        // round against the visible opponent before using its private enchantment data.
        static BattlePlayerPrivateData OpponentPrivate(BattleResult result, int round, BattlePlayerData opp, bool remapped)
        {
            if (result == null || result.round != round) return null;
            var players = new PlayerData[] { result.p1, result.p2 };
            BattlePlayerPrivateData match = null;
            foreach (var player in players)
            {
                if (player == null || player.publicData == null || player.privateData == null) continue;
                var src = player.publicData;
                if (src.uid == opp.uid) return player.privateData;
                if (!remapped || src.characterId != opp.characterId || src.lastRoundData == null || opp.lastRoundData == null) continue;
                var a = src.lastRoundData;
                var b = opp.lastRoundData;
                if (a.exp != b.exp || a.extraMaxHp != b.extraMaxHp || !SameIds(a.usedCards, b.usedCards)
                    || !SameIds(a.xianMoStrategies, b.xianMoStrategies)) continue;
                if (match != null) return null;
                match = player.privateData;
            }
            return match;
        }

        static bool SameIds(List<int> a, List<int> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static BattlePlayerData FindOpponent(GameStatus gs, BattlePlayerData me)
        {
            BattlePlayerData opp = null;
            try { opp = GameStatusExtension.GetNextOpponentPlayerData(gs, me.uid); } catch (Exception) { }
            if (opp != null && opp.uid != me.uid) return opp;
            List<BattlePlayerData> all = gs.battlePlayerDatas;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].uid != me.uid) return all[i];
            return null;
        }

        static Dictionary<string, object> MeSide(BattlePlayerData me, BattlePlayerPrivateData priv, List<object> pool, List<object> initial)
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

        internal static Dictionary<string, object> FoeSide(BattlePlayerData opp) { return RecordPuzzleReader.SnapshotSide(opp); }

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
