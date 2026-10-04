using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxCanju
{
	public sealed class EsotericView
	{
		private sealed class RowTarget
		{
			public EsotericUnitData Data;

			public int Index;
		}

		private sealed class RowButton
		{
			public EsotericView Owner;

			public Transform Cell;

			public RowTarget Target;

			public UiButton Button;

			public void Click()
			{
				if (Target != null && !Owner._solving && Owner._panel != null && !Owner._panel.hiding)
				{
					Owner._onPlace(Target.Index);
				}
			}
		}

		private const int LocalShortId = -20260923;

		private const string PlaceName = "YxCanjuPlace";

		private const string ResolveName = "YxCanjuResolve";

		private readonly ModContext _ctx;

		private readonly Action<int> _onPlace;

		private readonly Action _onResolve;

		private BattleEsotericDetailPanel _panel;

		private UiButton _resolve;

		private Transform _mainButton;

		private bool _mainButtonActive;

		private TextMeshProUGUI _title;

		private bool _titleEnabled;

		private Button _template;

		private EsotericData _data;

		private bool _solving;

		private ISubscription _cellHook;

		private ISubscription _showHook;

		private RectTransform _movable;

		private Vector2 _anchorMin;

		private Vector2 _anchorMax;

		private Vector2 _pivot;

		private DrawerAnimationController _drawer;

		private Vector2 _closePos;

		private Vector2 _openPos;

		private readonly List<RowTarget> _targets = new List<RowTarget>();

		private readonly List<RowButton> _rowButtons = new List<RowButton>();

		public EsotericView(ModContext ctx, Action<int> onPlace, Action onResolve)
		{
			_ctx = ctx;
			_onPlace = onPlace;
			_onResolve = onResolve;
		}

		public void Install()
		{
			_cellHook = _ctx.Hooks.TryPostfix("EsotericDetailUnitCell", "UpdateContent", 1, OnCellUpdated);
			_showHook = _ctx.Hooks.TryPrefix("BattleEsotericDetailPanel", "Show", 1, OnPanelShow);
		}

		public void Uninstall()
		{
			if (_cellHook != null)
			{
				_cellHook.Cancel();
			}
			if (_showHook != null)
			{
				_showHook.Cancel();
			}
			_cellHook = null;
			_showHook = null;
		}

		private bool OnPanelShow(HookContext h)
		{
			if (_panel != null && h.Instance == _panel && (h.Args.Length == 0 || h.Args[0] != _data))
			{
				Cleanup();
			}
			return true;
		}

		public bool Show(Dictionary<string, object> report, int charId, int sect, List<int> pool)
		{
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			if (battlePanel == null || battlePanel.readyLayer == null)
			{
				return false;
			}
			BattleEsotericDetailPanel battleEsotericDetailPanel = battlePanel.FindILRSubPanelRuntime<BattleEsotericDetailPanel>(battlePanel.readyLayer.subPanelContainer);
			if (battleEsotericDetailPanel == null)
			{
				return false;
			}
			if (_panel != null && _panel != battleEsotericDetailPanel)
			{
				Cleanup();
			}
			_panel = battleEsotericDetailPanel;
			ClearRowButtons();
			_targets.Clear();
			_data = Build(report, charId, sect, pool);
			Transform transform = Find(battlePanel.readyLayer.transform, "RecommendDeckButton");
			_template = ((transform == null) ? null : (transform.GetComponent(typeof(Button)) as Button));
			if (_template == null)
			{
				_template = NativeUi.FindButton(battleEsotericDetailPanel.transform, "CardIllustrationButton");
			}
			battleEsotericDetailPanel.Show(_data);
			MoveToLeft(battleEsotericDetailPanel.transform);
			if (_mainButton == null)
			{
				_mainButton = Find(battleEsotericDetailPanel.transform, "EsotericMainButton");
				if (_mainButton != null)
				{
					_mainButtonActive = _mainButton.gameObject.activeSelf;
				}
			}
			if (_mainButton != null)
			{
				_mainButton.gameObject.SetActive(value: false);
			}
			AddButtons(battleEsotericDetailPanel.transform);
			return true;
		}

		private void MoveToLeft(Transform root)
		{
			if (_movable == null)
			{
				Transform transform = Find(root, "Movable");
				_movable = ((transform == null) ? null : (transform.GetComponent(typeof(RectTransform)) as RectTransform));
				if (_movable == null)
				{
					return;
				}
				_anchorMin = _movable.anchorMin;
				_anchorMax = _movable.anchorMax;
				_pivot = _movable.pivot;
				ProjectUtils.PrepareILRComponent(_movable.gameObject, ref _drawer);
				if (_drawer != null)
				{
					_closePos = _drawer.closePosition;
					_openPos = _drawer.openPosition;
				}
			}
			_movable.anchorMin = new Vector2(0f, _anchorMin.y);
			_movable.anchorMax = new Vector2(0f, _anchorMax.y);
			_movable.pivot = new Vector2(0f, _pivot.y);
			if (_drawer != null)
			{
				_drawer.SetDistance(new Vector2(0f - _movable.rect.width, 0f), Vector2.zero);
			}
		}

		public void SetSolving(bool solving)
		{
			_solving = solving;
			if (_resolve != null && _resolve.IsAlive)
			{
				_resolve.SetText(solving ? "求解中…" : "求解");
				_resolve.SetInteractable(!solving);
			}
			for (int i = 0; i < _rowButtons.Count; i++)
			{
				if (_rowButtons[i].Button != null)
				{
					_rowButtons[i].Button.SetInteractable(!solving);
				}
			}
		}

		public void Hide()
		{
			try
			{
				if (_panel != null && !_panel.hiding)
				{
					_panel.Hide();
				}
			}
			catch (Exception)
			{
			}
		}

		public void Tick()
		{
			if (_panel != null)
			{
				bool flag = true;
				try
				{
					flag = _panel.hiding || !_panel.gameObject.activeInHierarchy;
				}
				catch (Exception)
				{
				}
				if (flag)
				{
					Cleanup();
				}
			}
		}

		public void Cleanup()
		{
			ClearRowButtons();
			_targets.Clear();
			if (_resolve != null && _resolve.IsAlive)
			{
				UnityEngine.Object.Destroy(_resolve.GameObject);
			}
			_resolve = null;
			if (_mainButton != null)
			{
				try
				{
					_mainButton.gameObject.SetActive(_mainButtonActive);
				}
				catch (Exception)
				{
				}
			}
			if (_title != null)
			{
				_title.enabled = _titleEnabled;
			}
			_title = null;
			if (_movable != null)
			{
				_movable.anchorMin = _anchorMin;
				_movable.anchorMax = _anchorMax;
				_movable.pivot = _pivot;
				_movable.anchoredPosition = _closePos;
			}
			if (_drawer != null)
			{
				_drawer.SetDistance(_closePos, _openPos);
			}
			_movable = null;
			_drawer = null;
			_mainButton = null;
			_panel = null;
			_data = null;
		}

		private void AddButtons(Transform root)
		{
			if (_resolve != null && _resolve.IsAlive)
			{
				return;
			}
			Transform transform = Find(root, "TitleLabel");
			if (!(transform == null) && !(_template == null))
			{
				_title = transform.GetComponent(typeof(TextMeshProUGUI)) as TextMeshProUGUI;
				if (_title != null)
				{
					_titleEnabled = _title.enabled;
					_title.enabled = false;
				}
				_resolve = NativeUi.CloneButton(_ctx, _template, transform.parent, "求解", "求解", _onResolve);
				PlaceButton(_resolve, "YxCanjuResolve", Vector2.one * 0.5f, Vector2.zero, new Vector2(176f, 56f));
				SetSolving(_solving);
			}
		}

		private void OnCellUpdated(HookContext h)
		{
			EsotericDetailUnitCell esotericDetailUnitCell = h.Instance as EsotericDetailUnitCell;
			EsotericDetailUnitCell.CellData cellData = ((h.Args.Length == 0) ? null : (h.Args[0] as EsotericDetailUnitCell.CellData));
			if (esotericDetailUnitCell == null)
			{
				return;
			}
			RowButton rowButton = null;
			for (int i = 0; i < _rowButtons.Count; i++)
			{
				if (_rowButtons[i].Cell == esotericDetailUnitCell.transform)
				{
					rowButton = _rowButtons[i];
					break;
				}
			}
			RowTarget rowTarget = null;
			if (_panel != null && cellData != null && IsUnder(esotericDetailUnitCell.transform, _panel.transform))
			{
				for (int j = 0; j < _targets.Count; j++)
				{
					if (_targets[j].Data == cellData.unitData)
					{
						rowTarget = _targets[j];
						break;
					}
				}
			}
			if (rowTarget == null)
			{
				if (rowButton != null)
				{
					rowButton.Target = null;
					if (rowButton.Button != null && rowButton.Button.IsAlive)
					{
						rowButton.Button.GameObject.SetActive(value: false);
					}
				}
				return;
			}
			if (rowButton == null)
			{
				rowButton = new RowButton
				{
					Owner = this,
					Cell = esotericDetailUnitCell.transform
				};
				_rowButtons.Add(rowButton);
			}
			rowButton.Target = rowTarget;
			if (rowButton.Button == null || !rowButton.Button.IsAlive)
			{
				Transform transform = Find(esotericDetailUnitCell.transform, "Movable");
				if (transform == null)
				{
					transform = esotericDetailUnitCell.transform;
				}
				rowButton.Button = NativeUi.CloneButton(_ctx, _template, transform, "一键摆牌", "一键摆牌", (Action)rowButton.Click);
				RectTransform rectTransform = transform.GetComponent(typeof(RectTransform)) as RectTransform;
				Transform transform2 = Find(transform, "Cards");
				RectTransform rectTransform2 = ((transform2 == null) ? null : (transform2.GetComponent(typeof(RectTransform)) as RectTransform));
				float num = ((rectTransform != null && rectTransform2 != null) ? ((rectTransform.rect.width - rectTransform2.rect.width) / 2f) : 360f);
				PlaceButton(rowButton.Button, "YxCanjuPlace", new Vector2(0f, 0.5f), new Vector2(num / 2f, -30f), new Vector2(Math.Min(300f, Math.Max(140f, num - 40f)), 90f));
			}
			if (rowButton.Button != null && rowButton.Button.IsAlive)
			{
				rowButton.Button.GameObject.SetActive(value: true);
				rowButton.Button.SetInteractable(!_solving);
			}
		}

		private static bool IsUnder(Transform child, Transform root)
		{
			Transform transform = child;
			while (transform != null)
			{
				if (transform == root)
				{
					return true;
				}
				transform = transform.parent;
			}
			return false;
		}

		private static void PlaceButton(UiButton button, string name, Vector2 anchor, Vector2 position, Vector2 size)
		{
			if (button != null && button.IsAlive)
			{
				button.GameObject.name = name;
				RectTransform rectTransform = button.GameObject.GetComponent(typeof(RectTransform)) as RectTransform;
				if (!(rectTransform == null))
				{
					rectTransform.anchorMin = anchor;
					rectTransform.anchorMax = anchor;
					rectTransform.pivot = Vector2.one * 0.5f;
					rectTransform.anchoredPosition = position;
					rectTransform.sizeDelta = size;
				}
			}
		}

		private void ClearRowButtons()
		{
			for (int i = 0; i < _rowButtons.Count; i++)
			{
				_rowButtons[i].Target = null;
				if (_rowButtons[i].Button != null && _rowButtons[i].Button.IsAlive)
				{
					_rowButtons[i].Button.GameObject.SetActive(value: false);
					UnityEngine.Object.Destroy(_rowButtons[i].Button.GameObject);
				}
			}
			_rowButtons.Clear();
		}

		private EsotericData Build(Dictionary<string, object> report, int charId, int sect, List<int> pool)
		{
			EsotericData esotericData = new EsotericData();
			esotericData.shortId = -20260923;
			esotericData.complete = false;
			esotericData.isPrivate = true;
			esotericData.title = "残局求解";
			esotericData.authorName = "残局求解";
			esotericData.createTs = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
			esotericData.version = "001.0007.0016";
			try
			{
				esotericData.seasonId = SeasonManager.currentSeasonId;
			}
			catch (Exception)
			{
			}
			esotericData.charId = charId;
			esotericData.sect = (Sect)sect;
			esotericData.remark = ((report == null) ? "" : Summary(report));
			List<object> list = ((report == null) ? null : Json.GetArray(report, "best"));
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i] is Dictionary<string, object> e)
					{
						List<int> list2 = Cards(e);
						EsotericUnitData esotericUnitData = new EsotericUnitData();
						esotericUnitData.level = Level.InvalidLevel;
						esotericUnitData.remark = "方案 " + (i + 1).ToString(CultureInfo.InvariantCulture) + "：" + Orders(e) + EmptySlots(list2);
						for (int j = 0; j < list2.Count; j++)
						{
							esotericUnitData.cards.Add(list2[j]);
						}
						esotericData.unitDatas.Add(esotericUnitData);
						_targets.Add(new RowTarget
						{
							Data = esotericUnitData,
							Index = i
						});
						if (i == 0)
						{
							AddHandRow(esotericData, pool, list2);
						}
					}
				}
			}
			esotericData.unitCount = esotericData.unitDatas.Count;
			if (esotericData.unitDatas.Count > 0)
			{
				esotericData.cover = esotericData.unitDatas[0];
			}
			return esotericData;
		}

		private static string EmptySlots(List<int> cards)
		{
			string text = "";
			for (int i = 0; i < cards.Count; i++)
			{
				if (cards[i] == 0)
				{
					if (text.Length > 0)
					{
						text += "、";
					}
					text += (i + 1).ToString(CultureInfo.InvariantCulture);
				}
			}
			if (text.Length != 0)
			{
				return "；第 " + text + " 格留空（图中以普通攻击占位）";
			}
			return "";
		}

		private static void AddHandRow(EsotericData d, List<int> pool, List<int> board)
		{
			List<int> list = new List<int>(pool);
			for (int i = 0; i < board.Count; i++)
			{
				int num = list.IndexOf(board[i]);
				if (num >= 0)
				{
					list.RemoveAt(num);
				}
			}
			if (list.Count != 0)
			{
				EsotericUnitData esotericUnitData = new EsotericUnitData();
				esotericUnitData.level = Level.InvalidLevel;
				esotericUnitData.remark = "方案 1 留在手牌";
				for (int j = 0; j < list.Count && j < 8; j++)
				{
					esotericUnitData.cards.Add(list[j]);
				}
				d.unitDatas.Add(esotericUnitData);
			}
		}

		private static List<int> Cards(Dictionary<string, object> e)
		{
			List<int> list = new List<int>();
			List<object> array = Json.GetArray(e, "cards");
			if (array == null)
			{
				return list;
			}
			for (int i = 0; i < array.Count; i++)
			{
				list.Add((int)Convert.ToDouble(array[i], CultureInfo.InvariantCulture));
			}
			return list;
		}

		private static string Orders(Dictionary<string, object> e)
		{
			string text = "";
			List<object> array = Json.GetArray(e, "orders");
			if (array == null)
			{
				return text;
			}
			for (int i = 0; i < array.Count; i++)
			{
				if (array[i] is Dictionary<string, object> dictionary)
				{
					string text2 = "后手";
					if (Json.GetString(dictionary, "first", "") == "me")
					{
						text2 = "先手";
					}
					string text3 = "输";
					if (Json.GetString(dictionary, "result", "") == "win")
					{
						text3 = "赢";
					}
					string text4 = ((int)Json.GetNumber(dictionary, "myHp", 0.0)).ToString(CultureInfo.InvariantCulture);
					string text5 = ((int)Json.GetNumber(dictionary, "foeHp", 0.0)).ToString(CultureInfo.InvariantCulture);
					string text6 = RandomText(dictionary);
					if (text.Length > 0)
					{
						text += "，";
					}
					text = text + text2 + text3 + "（" + text4 + " : " + text5 + text6 + "）";
				}
			}
			return text;
		}

		private static string RandomText(Dictionary<string, object> order)
		{
			if (!Json.GetBool(order, "hadRandom", fallback: false))
			{
				return "";
			}
			int num = (int)Json.GetNumber(order, "randomWins", 0.0);
			int num2 = (int)Json.GetNumber(order, "randomRuns", 0.0);
			return "；随机胜 " + num.ToString(CultureInfo.InvariantCulture) + "/" + num2.ToString(CultureInfo.InvariantCulture);
		}

		private static string Summary(Dictionary<string, object> r)
		{
			string text = (Json.GetNumber(r, "elapsedMs", 0.0) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
			string text2 = ((long)Json.GetNumber(r, "evaluated", 0.0)).ToString(CultureInfo.InvariantCulture);
			string text3 = "局部搜索";
			if (Json.GetBool(r, "exhaustive", fallback: false))
			{
				text3 = "已穷举，全局最优";
			}
			return "本地模拟推荐 · 算了 " + text2 + " 场，" + text + " 秒，" + text3;
		}

		private static Transform Find(Transform t, string name)
		{
			if (t == null)
			{
				return null;
			}
			if (t.name == name)
			{
				return t;
			}
			for (int i = 0; i < t.childCount; i++)
			{
				Transform transform = Find(t.GetChild(i), name);
				if (transform != null)
				{
					return transform;
				}
			}
			return null;
		}
	}
}
