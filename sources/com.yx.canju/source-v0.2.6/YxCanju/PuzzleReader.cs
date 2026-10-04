using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.Shared;

namespace YxCanju
{
	public static class PuzzleReader
	{
		public static bool CanSolve(ModContext ctx, BattleManager bm)
		{
			if (bm == null || bm.currentGameStatus == null || bm.currentScene != SceneType.修炼阶段 || SceneLoader.isLoading || SceneLoader.currentSceneName != "Battle")
			{
				return false;
			}
			if (bm.defaultBattleExecuter != null && bm.defaultBattleExecuter.isExecuting)
			{
				return false;
			}
			if (bm.gameMode == GameMode.ReviewMode)
			{
				return true;
			}
			if (bm.gameMode != GameMode.PracticeMode)
			{
				return false;
			}
			GameClient client = GameClientUtil.client;
			if (client == null || client.isInRoom || client.needReconnectToRoom)
			{
				return false;
			}
			LocalPractice val = ctx.Services.Get<LocalPractice>();
			if (val != null)
			{
				return val.IsReady;
			}
			return false;
		}

		public static string Signature(Dictionary<string, object> req)
		{
			return PuzzleSignature.Of(req);
		}

		public static Dictionary<string, object> Read(ModContext ctx, out string error)
		{
			error = null;
			BattleManager instance = BattleManager.Instance;
			if (instance == null || instance.currentGameStatus == null)
			{
				error = "不在对局中";
				return null;
			}
			if (!CanSolve(ctx, instance))
			{
				error = "只能在残局 / 复盘 / 本地练习场的备战界面使用";
				return null;
			}
			GameStatus currentGameStatus = instance.currentGameStatus;
			BattlePlayerData battlePlayerData = currentGameStatus.GetMainPlayerData();
			BattlePlayerPrivateData playerPrivateData = currentGameStatus.playerPrivateData;
			if (battlePlayerData == null || playerPrivateData == null)
			{
				error = "没有我方数据";
				return null;
			}
			BattlePlayerData battlePlayerData2 = FindOpponent(currentGameStatus, battlePlayerData);
			if (battlePlayerData2 == null)
			{
				error = "没有对方数据";
				return null;
			}
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			CardPanel cardPanel = battlePanel?.FindILRSubPanel<CardPanel>();
			if (cardPanel == null)
			{
				error = "不在摆牌界面";
				return null;
			}
			if (cardPanel.isDealingCards)
			{
				error = "正在发牌，请稍候";
				return null;
			}
			if (battlePanel.readyLayer != null && battlePanel.readyLayer.playerSelfInfoItem != null && battlePanel.readyLayer.playerSelfInfoItem.battlePlayerData != null)
			{
				battlePlayerData = battlePanel.readyLayer.playerSelfInfoItem.battlePlayerData;
			}
			List<object> list = new List<object>();
			List<object> list2 = new List<object>();
			List<CardGrid> cardGrids = cardPanel.GetCardGrids();
			int num = 0;
			if (cardGrids != null)
			{
				for (int i = 0; i < cardGrids.Count; i++)
				{
					CardGrid cardGrid = cardGrids[i];
					if (cardGrid != null && cardGrid.unlocked)
					{
						num++;
						CardItem card = cardGrid.GetCard();
						int num2 = ((card != null && card.cardInfo != null) ? card.cardInfo.id : 0);
						list2.Add(num2);
						if (num2 != 0)
						{
							list.Add(num2);
						}
					}
				}
			}
			List<CardItem> handCards = cardPanel.GetHandCards();
			if (handCards != null)
			{
				for (int j = 0; j < handCards.Count; j++)
				{
					if (handCards[j] != null && handCards[j].cardInfo != null && handCards[j].cardInfo.id != 0)
					{
						list.Add(handCards[j].cardInfo.id);
					}
				}
			}
			if (num == 0)
			{
				error = "没有已解锁的格子";
				return null;
			}
			if (PreviewCardGuard.Contains(list) || (battlePlayerData2.lastRoundData != null && (PreviewCardGuard.Contains(battlePlayerData2.lastRoundData.usedCards) || PreviewCardGuard.Contains(battlePlayerData2.lastRoundData.handCards))))
			{
				error = "局面含练习场新卡预览牌，当前求解器尚未实现这些效果；请手动试打，或移除双方手牌与牌桌里的预览牌";
				return null;
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary["round"] = currentGameStatus.round;
			dictionary["slots"] = num;
			dictionary["first"] = "both";
			dictionary["deduplicateResults"] = true;
			dictionary["maxConsumeSustain"] = cardPanel.Get_MAX_CONSUMED_CONTINUOUS_CARD_COUNT();
			dictionary["me"] = MeSide(battlePlayerData, playerPrivateData, list, list2);
			dictionary["foe"] = FoeSide(battlePlayerData2);
			LocalPractice val = ctx.Services.Get<LocalPractice>();
			Dictionary<string, object> dictionary2 = dictionary["foe"] as Dictionary<string, object>;
			if (instance.gameMode == GameMode.PracticeMode && val != null && val.IsReady)
			{
				string text = val.ReadOpponentStrategyData();
				if (text != null)
				{
					foreach (KeyValuePair<string, object> item in Json.ParseObject(text))
					{
						dictionary2[item.Key] = item.Value;
					}
				}
			}
			else if (instance.gameMode == GameMode.ReviewMode)
			{
				BattleResult currentBattleResult = BattleManager.currentBattleResult;
				if (currentBattleResult != null && currentBattleResult.round == currentGameStatus.round)
				{
					PlayerData playerData = ((currentBattleResult.p1 != null && currentBattleResult.p1.publicData != null && currentBattleResult.p1.publicData.uid == battlePlayerData2.uid) ? currentBattleResult.p1 : currentBattleResult.p2);
					if (playerData != null && playerData.publicData != null && playerData.publicData.uid == battlePlayerData2.uid && playerData.privateData != null && playerData.privateData.xianMoData != null)
					{
						dictionary2["xianMoTempDatas"] = IntMap(playerData.privateData.xianMoData.tempDatas);
					}
				}
			}
			dictionary["position"] = ((instance.gameMode == GameMode.PracticeMode && val != null) ? ("arena:" + val.Position) : ("review:" + battlePlayerData.uid));
			return dictionary;
		}

		private static BattlePlayerData FindOpponent(GameStatus gs, BattlePlayerData me)
		{
			BattlePlayerData battlePlayerData = null;
			try
			{
				battlePlayerData = gs.GetNextOpponentPlayerData(me.uid);
			}
			catch (Exception)
			{
			}
			if (battlePlayerData != null && battlePlayerData.uid != me.uid)
			{
				return battlePlayerData;
			}
			List<BattlePlayerData> battlePlayerDatas = gs.battlePlayerDatas;
			if (battlePlayerDatas == null)
			{
				return null;
			}
			for (int i = 0; i < battlePlayerDatas.Count; i++)
			{
				if (battlePlayerDatas[i] != null && battlePlayerDatas[i].uid != me.uid)
				{
					return battlePlayerDatas[i];
				}
			}
			return null;
		}

		private static Dictionary<string, object> MeSide(BattlePlayerData me, BattlePlayerPrivateData priv, List<object> pool, List<object> initial)
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary["hp"] = me.extraMaxHp;
			dictionary["level"] = (int)me.level;
			dictionary["life"] = me.life;
			dictionary["exp"] = me.exp;
			dictionary["characterId"] = me.characterId;
			dictionary["sect"] = (int)me.sect;
			dictionary["career"] = (int)me.career;
			dictionary["talents"] = Ints(me.talents);
			dictionary["keyin"] = Ints((priv.keYinData != null) ? priv.keYinData.usedCards : null);
			dictionary["maxKeYin"] = ((priv.keYinData != null) ? priv.keYinData.maxKeYin : 0);
			List<object> list = new List<object>();
			if (priv.fateStrategyData != null && priv.fateStrategyData.strategies != null)
			{
				for (int i = 0; i < priv.fateStrategyData.strategies.Count; i++)
				{
					SelectionData selectionData = priv.fateStrategyData.strategies[i];
					if (selectionData != null && selectionData.selected != 0)
					{
						list.Add(selectionData.selected);
					}
				}
			}
			dictionary["fate"] = list;
			List<object> list2 = new List<object>();
			if (priv.xianMoData != null && priv.xianMoData.strategies != null)
			{
				for (int j = 0; j < priv.xianMoData.strategies.Count; j++)
				{
					SelectionData selectionData2 = priv.xianMoData.strategies[j];
					if (selectionData2 != null && selectionData2.selected != 0)
					{
						list2.Add(selectionData2.selected);
					}
				}
			}
			dictionary["xianMoStrategies"] = list2;
			dictionary["xianMoTempDatas"] = IntMap((priv.xianMoData != null) ? priv.xianMoData.tempDatas : null);
			dictionary["resonance"] = Resonance(priv.talentResonanceData);
			dictionary["permanentBuffs"] = IntMap(me.permanentBuffTempDatas);
			dictionary["talentTempDatas"] = IntMap(me.talentTempDatas);
			dictionary["resonanceFlags"] = IntMap(me.resonanceTalentFlags);
			dictionary["talentDatas"] = ParamMap(me.talentDatas);
			dictionary["privateTalentDatas"] = ParamMap(priv.talentDatas);
			dictionary["cards"] = pool;
			dictionary["initial"] = initial;
			return dictionary;
		}

		private static Dictionary<string, object> FoeSide(BattlePlayerData opp)
		{
			BattlePlayerLastRoundData lastRoundData = opp.lastRoundData;
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary["hp"] = lastRoundData?.extraMaxHp ?? opp.extraMaxHp;
			dictionary["level"] = (int)opp.level;
			dictionary["life"] = lastRoundData?.life ?? opp.life;
			dictionary["exp"] = lastRoundData?.exp ?? opp.exp;
			dictionary["characterId"] = opp.characterId;
			dictionary["sect"] = (int)opp.sect;
			dictionary["career"] = (int)opp.career;
			dictionary["resonanceFlags"] = IntMap(opp.resonanceTalentFlags);
			if (lastRoundData == null)
			{
				dictionary["talents"] = Ints(opp.talents);
				dictionary["cards"] = new List<object>();
				return dictionary;
			}
			dictionary["talents"] = Ints(lastRoundData.talents);
			dictionary["keyin"] = Ints(lastRoundData.usedKeYinCards);
			dictionary["fate"] = Ints(lastRoundData.fateStrategies);
			dictionary["xianMoStrategies"] = Ints(lastRoundData.xianMoStrategies);
			dictionary["resonance"] = Resonance(lastRoundData.talentResonanceData);
			dictionary["permanentBuffs"] = IntMap(lastRoundData.permanentBuffTempDatas);
			dictionary["talentTempDatas"] = IntMap(lastRoundData.talentTempDatas);
			dictionary["talentDatas"] = ParamMap(lastRoundData.talentDatas);
			dictionary["privateTalentDatas"] = ParamMap(lastRoundData.privateTalentDatas);
			dictionary["handCards"] = Ints(lastRoundData.handCards);
			dictionary["cards"] = Ints(lastRoundData.usedCards);
			return dictionary;
		}

		private static int Resonance(TalentResonanceData d)
		{
			if (d == null || d.selectionData == null)
			{
				return 0;
			}
			return d.selectionData.selected;
		}

		private static List<object> Ints(List<int> src)
		{
			List<object> list = new List<object>();
			if (src == null)
			{
				return list;
			}
			for (int i = 0; i < src.Count; i++)
			{
				list.Add(src[i]);
			}
			return list;
		}

		private static Dictionary<string, object> IntMap(Dictionary<int, int> src)
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			if (src == null)
			{
				return dictionary;
			}
			foreach (KeyValuePair<int, int> item in src)
			{
				dictionary[item.Key.ToString(CultureInfo.InvariantCulture)] = item.Value;
			}
			return dictionary;
		}

		private static Dictionary<string, object> ParamMap(Dictionary<int, BattleTalentData> src)
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			if (src == null)
			{
				return dictionary;
			}
			foreach (KeyValuePair<int, BattleTalentData> item in src)
			{
				dictionary[item.Key.ToString(CultureInfo.InvariantCulture)] = Ints((item.Value != null) ? item.Value.commonParams : null);
			}
			return dictionary;
		}
	}
}
