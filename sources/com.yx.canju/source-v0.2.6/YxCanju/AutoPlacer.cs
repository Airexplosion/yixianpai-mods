using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.ModSdk;

namespace YxCanju
{
	public sealed class AutoPlacer
	{
		private const float StepSeconds = 0.35f;

		private const float PendingTimeout = 4f;

		private const int MaxSteps = 60;

		private readonly ModContext _ctx;

		private int[] _target;

		private int _steps;

		private bool _running;

		private int _pendingSlot = -1;

		private int _pendingId;

		private float _pendingWait;

		private Action<string> _onDone;

		private string _signature;

		public bool Running => _running;

		public AutoPlacer(ModContext ctx)
		{
			_ctx = ctx;
		}

		public void Start(int[] target, string signature, Action<string> onDone)
		{
			if (!_running)
			{
				_target = target;
				_signature = signature;
				_onDone = onDone;
				_steps = 0;
				_pendingSlot = -1;
				_running = true;
				Step();
			}
		}

		public void Stop(string why)
		{
			if (_running)
			{
				_running = false;
				if (_onDone != null)
				{
					_onDone(why);
				}
			}
		}

		private void Next()
		{
			_ctx.MainThread.Delay(0.35f, Step);
		}

		private void Step()
		{
			if (!_running)
			{
				return;
			}
			try
			{
				StepCore();
			}
			catch (Exception ex)
			{
				Stop("摆牌出错：" + ex.Message);
			}
		}

		private void StepCore()
		{
			if (!PuzzleReader.CanSolve(_ctx, BattleManager.Instance))
			{
				Stop("已离开可求解的备战界面");
				return;
			}
			if (++_steps > 60)
			{
				Stop("步数超过上限，已停止（牌面可能被手动改过）");
				return;
			}
			CardPanel cardPanel = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<CardPanel>();
			if (cardPanel == null)
			{
				Stop("不在摆牌界面");
				return;
			}
			if (cardPanel.isDealingCards)
			{
				Next();
				return;
			}
			List<CardGrid> list = new List<CardGrid>();
			List<CardItem> list2 = new List<CardItem>();
			List<int> list3 = new List<int>();
			List<CardGrid> cardGrids = cardPanel.GetCardGrids();
			if (cardGrids != null)
			{
				for (int i = 0; i < cardGrids.Count; i++)
				{
					CardGrid cardGrid = cardGrids[i];
					if (cardGrid != null && cardGrid.unlocked)
					{
						CardItem card = cardGrid.GetCard();
						list.Add(cardGrid);
						list2.Add(card);
						list3.Add((card != null && card.cardInfo != null) ? card.cardInfo.id : 0);
					}
				}
			}
			List<CardItem> handCards = cardPanel.GetHandCards();
			if (list.Count < _target.Length)
			{
				Stop("格子数和求解时不一致，停止");
				return;
			}
			if (_pendingSlot >= 0)
			{
				if (list3[_pendingSlot] != _pendingId && _pendingWait < 4f)
				{
					_pendingWait += 0.35f;
					Next();
					return;
				}
				_pendingSlot = -1;
			}
			if (AnyBusy(list2) || AnyBusy(handCards))
			{
				Next();
				return;
			}
			string error;
			Dictionary<string, object> dictionary = PuzzleReader.Read(_ctx, out error);
			if (dictionary == null || PuzzleReader.Signature(dictionary) != _signature)
			{
				Stop("局面已改变，请重新求解");
				return;
			}
			int num = ExcessSlot(list3);
			if (num >= 0)
			{
				cardPanel.MoveToHand(list2[num]);
				Pending(num, 0);
				return;
			}
			for (int j = 0; j < _target.Length; j++)
			{
				int num2 = _target[j];
				if (list3[j] == num2)
				{
					continue;
				}
				if (num2 == 0)
				{
					cardPanel.MoveToHand(list2[j]);
					Pending(j, 0);
					return;
				}
				for (int k = 0; k < list3.Count; k++)
				{
					if (k != j && list3[k] == num2 && (k >= _target.Length || _target[k] != num2))
					{
						cardPanel.MoveToGrid(list2[k], list[j]);
						Pending(j, num2);
						return;
					}
				}
				if (handCards != null)
				{
					for (int l = 0; l < handCards.Count; l++)
					{
						CardItem cardItem = handCards[l];
						if (cardItem != null && cardItem.cardInfo != null && cardItem.cardInfo.id == num2)
						{
							cardPanel.MoveToGrid(cardItem, list[j]);
							Pending(j, num2);
							return;
						}
					}
				}
				Stop("找不到要放的牌（" + num2.ToString(CultureInfo.InvariantCulture) + "），停止");
				return;
			}
			Stop("摆好了");
		}

		private void Pending(int slot, int id)
		{
			_pendingSlot = slot;
			_pendingId = id;
			_pendingWait = 0f;
			Next();
		}

		private int ExcessSlot(List<int> board)
		{
			for (int i = 0; i < board.Count; i++)
			{
				int num = board[i];
				if (num == 0 || Count(board, num) <= Count(_target, num))
				{
					continue;
				}
				for (int j = 0; j < board.Count; j++)
				{
					if (board[j] == num && (j >= _target.Length || _target[j] != num))
					{
						return j;
					}
				}
			}
			return -1;
		}

		private static int Count(List<int> list, int id)
		{
			int num = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] == id)
				{
					num++;
				}
			}
			return num;
		}

		private static int Count(int[] arr, int id)
		{
			int num = 0;
			for (int i = 0; i < arr.Length; i++)
			{
				if (arr[i] == id)
				{
					num++;
				}
			}
			return num;
		}

		private static bool AnyBusy(List<CardItem> items)
		{
			if (items == null)
			{
				return false;
			}
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i] != null && !items[i].interactable)
				{
					return true;
				}
			}
			return false;
		}
	}
}
