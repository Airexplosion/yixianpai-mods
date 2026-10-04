using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxCanju
{
	public sealed class CanjuMod : YxMod
	{
		public const string Lib = "solver";

		public const string Hotkey = "Ctrl+Alt+J";

		private const float PollSeconds = 0.3f;

		private const float ToastSeconds = 1.5f;

		private ConfigEntry<int> _topN;

		private ConfigEntry<int> _timeLimitMs;

		private ConfigEntry<int> _samples;

		private ConfigEntry<int> _seed;

		private int _job;

		private bool _polling;

		private float _sinceToast;

		private ResultView _fallback;

		private EsotericView _eso;

		private AutoPlacer _placer;

		private UiButton _btn;

		private int _frame;

		private bool _btnShown;

		private string _sig;

		private string _pendingSig;

		private Dictionary<string, object> _result;

		private int _charId;

		private int _sect;

		private readonly List<int> _pool = new List<int>();

		public override void OnLoad(ModContext ctx)
		{
			_topN = ctx.Config.Bind("solve", "topN", 5, ctx.T("显示前几名摆法", "How many arrangements to show"));
			_timeLimitMs = ctx.Config.Bind("solve", "timeLimitMs", 8000, ctx.T("最长求解时间（毫秒）；多数局面会提前收敛结束", "Max solve time (ms); most positions converge earlier"));
			_samples = ctx.Config.Bind("solve", "samples", 3, ctx.T("随机局面抽样次数；越大越稳定但更慢", "Random position samples; higher is steadier but slower"));
			_seed = ctx.Config.Bind("solve", "seed", 1, ctx.T("随机种子；相同种子和局面得到相同结果", "Random seed; same seed and position give the same result"));
			ctx.Input.RegisterHotkey("solve", "Ctrl+Alt+J", OnButton);
			_fallback = new ResultView(ctx);
			_eso = new EsotericView(ctx, OnPlace, OnResolve);
			_eso.Install();
			_placer = new AutoPlacer(ctx);
			if (!ctx.Native.IsAvailable("solver"))
			{
				ctx.Log.Warn("求解库不可用（manifest 没声明，或管理器版本不支持原生库）");
			}
		}

		public override void OnDisable()
		{
			if (_job != 0)
			{
				Cancel();
			}
			if (_placer != null)
			{
				_placer.Stop("mod 已停用");
			}
			if (_eso != null)
			{
				_eso.Cleanup();
			}
			if (_eso != null)
			{
				_eso.Uninstall();
			}
			if (_fallback != null)
			{
				_fallback.Close();
			}
			if (_btn != null && _btn.IsAlive)
			{
				UnityEngine.Object.Destroy(_btn.GameObject);
			}
			_btn = null;
		}

		public override void OnUpdate()
		{
			if (_frame++ % 20 != 0)
			{
				return;
			}
			try
			{
				InjectButton();
				_eso.Tick();
			}
			catch (Exception ex)
			{
				base.Context.Log.Warn("界面刷新失败：" + ex.Message);
			}
		}

		private void OnButton()
		{
			if (!PuzzleReader.CanSolve(base.Context, BattleManager.Instance))
			{
				Toast("只能在残局 / 复盘 / 本地练习场的备战界面使用");
				return;
			}
			string error;
			Dictionary<string, object> dictionary = PuzzleReader.Read(base.Context, out error);
			if (dictionary == null)
			{
				Toast("读不到局面：" + error);
				return;
			}
			ApplySettings(dictionary);
			string text = PuzzleReader.Signature(dictionary);
			if (_result != null && text == _sig)
			{
				ShowResult();
				return;
			}
			RememberMe(dictionary);
			if (!_eso.Show(null, _charId, _sect, _pool))
			{
				Toast("秘籍面板暂不可用，请稍后重试");
			}
			_eso.SetSolving(_job != 0);
		}

		private void OnResolve()
		{
			if (!PuzzleReader.CanSolve(base.Context, BattleManager.Instance))
			{
				Toast("只能在残局 / 复盘 / 本地练习场的备战界面使用");
				return;
			}
			if (_job != 0)
			{
				Toast("正在求解，请稍候…");
				return;
			}
			if (_placer.Running)
			{
				Toast("正在摆牌，请稍候…");
				return;
			}
			string error;
			Dictionary<string, object> dictionary = PuzzleReader.Read(base.Context, out error);
			if (dictionary == null)
			{
				Toast("读不到局面：" + error);
				return;
			}
			ApplySettings(dictionary);
			Solve(dictionary, PuzzleReader.Signature(dictionary));
		}

		private void OnPlace(int index)
		{
			if (_job != 0)
			{
				Toast("正在求解，请稍候…");
				return;
			}
			if (_placer.Running)
			{
				Toast("正在摆牌…");
				return;
			}
			if (!ResultIsCurrent())
			{
				Toast("局面已改变，请重新求解");
				return;
			}
			int[] array = SolutionCards.Get(_result, index);
			if (array == null)
			{
				Toast("还没有求解结果");
				return;
			}
			_eso.Hide();
			Toast("开始按方案 " + (index + 1).ToString(CultureInfo.InvariantCulture) + " 摆牌…");
			_placer.Start(array, _sig, OnPlaced);
		}

		private void OnPlaced(string msg)
		{
			Toast(msg);
			base.Context.Log.Info("一键摆牌：" + msg);
		}

		private void Solve(Dictionary<string, object> req, string sig)
		{
			ModContext context = base.Context;
			if (!context.Native.IsAvailable("solver"))
			{
				Toast("求解库不可用");
				return;
			}
			ApplySettings(req);
			RememberMe(req);
			_result = null;
			_sig = null;
			_eso.Show(null, _charId, _sect, _pool);
			_eso.SetSolving(solving: true);
			_pendingSig = sig;
			string text = Json.Serialize(req);
			context.Log.Info("求解请求：" + text);
			Toast("开始求解…");
			_sinceToast = 0f;
			_job = -1;
			context.Native.Call("solver", "solve_start", text, (Action<string>)OnStarted);
		}

		private void OnStarted(string json)
		{
			Dictionary<string, object> dictionary = Parse(json);
			if (dictionary == null || !Json.GetBool(dictionary, "ok", fallback: false))
			{
				_job = 0;
				_eso.SetSolving(solving: false);
				string text = json;
				if (dictionary != null)
				{
					text = Json.GetString(dictionary, "error", json);
				}
				Toast("求解启动失败：" + text);
			}
			else
			{
				_job = (int)Json.GetNumber(dictionary, "job", 0.0);
				SchedulePoll();
			}
		}

		private void SchedulePoll()
		{
			if (!_polling)
			{
				_polling = true;
				base.Context.MainThread.Delay(0.3f, Poll);
			}
		}

		private void Poll()
		{
			_polling = false;
			if (_job > 0)
			{
				string text = "{\"job\":" + _job.ToString(CultureInfo.InvariantCulture) + "}";
				base.Context.Native.Call("solver", "solve_poll", text, (Action<string>)OnPoll);
			}
		}

		private void OnPoll(string json)
		{
			Dictionary<string, object> dictionary = Parse(json);
			if (dictionary == null)
			{
				_job = 0;
				_eso.SetSolving(solving: false);
				Toast("结果解析失败");
				return;
			}
			string text = Json.GetString(dictionary, "state", "");
			switch (text)
			{
			default:
				if (Json.GetBool(dictionary, "ok", fallback: true))
				{
					_sinceToast += 0.3f;
					if (_sinceToast >= 1.5f)
					{
						_sinceToast = 0f;
						Toast(Progress(dictionary));
					}
					SchedulePoll();
					return;
				}
				break;
			case "done":
			case "cancelled":
			case "error":
				break;
			}
			_job = 0;
			_eso.SetSolving(solving: false);
			base.Context.Log.Info("求解结束：" + json);
			if (text != "done")
			{
				Toast("求解没有完成：" + text + " " + Json.GetString(dictionary, "error", ""));
				return;
			}
			_result = dictionary;
			_sig = _pendingSig;
			if (!ResultIsCurrent())
			{
				Toast("求解完成，但局面已改变，请在当前局面重新求解");
				return;
			}
			Toast("求解完成：" + BestLine(dictionary));
			ShowResult();
		}

		private void ShowResult()
		{
			bool flag = false;
			try
			{
				flag = _eso.Show(_result, _charId, _sect, _pool);
			}
			catch (Exception ex)
			{
				base.Context.Log.Warn("秘籍面板显示失败，改用文字面板：" + ex.Message);
			}
			if (!flag)
			{
				_fallback.Show(_result);
			}
			_eso.SetSolving(_job != 0);
		}

		private bool ResultIsCurrent()
		{
			string error;
			Dictionary<string, object> dictionary = PuzzleReader.Read(base.Context, out error);
			if (dictionary != null)
			{
				ApplySettings(dictionary);
			}
			if (dictionary != null)
			{
				return PuzzleReader.Signature(dictionary) == _sig;
			}
			return false;
		}

		private void Cancel()
		{
			if (_job > 0)
			{
				string text = "{\"job\":" + _job.ToString(CultureInfo.InvariantCulture) + "}";
				base.Context.Native.Call("solver", "solve_cancel", text, (Action<string>)OnCancelled);
			}
			_job = 0;
		}

		private void OnCancelled(string json)
		{
		}

		private void InjectButton()
		{
			bool flag = PuzzleReader.CanSolve(base.Context, BattleManager.Instance);
			if (_btn != null && _btn.IsAlive)
			{
				if (flag != _btnShown)
				{
					_btn.GameObject.SetActive(flag);
					_btnShown = flag;
				}
				return;
			}
			_btn = null;
			if (!flag)
			{
				return;
			}
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			if (battlePanel == null || battlePanel.readyLayer == null)
			{
				return;
			}
			Button button = NativeUi.FindButton(battlePanel.readyLayer.transform, "RecommendDeckButton");
			if (button == null)
			{
				return;
			}
			Transform parent = button.transform.parent;
			bool flag2 = parent != null && parent.gameObject.activeInHierarchy;
			Transform transform = (flag2 ? parent : battlePanel.readyLayer.transform);
			Vector3 position = button.transform.position;
			_btn = NativeUi.CloneButton(base.Context, button, transform, "残局求解", "残局求解", (Action)OnButton);
			if (_btn == null || !_btn.IsAlive)
			{
				_btn = null;
				return;
			}
			RectTransform rectTransform = button.GetComponent(typeof(RectTransform)) as RectTransform;
			RectTransform rectTransform2 = _btn.GameObject.GetComponent(typeof(RectTransform)) as RectTransform;
			if (rectTransform != null && rectTransform2 != null)
			{
				rectTransform2.anchorMin = rectTransform.anchorMin;
				rectTransform2.anchorMax = rectTransform.anchorMax;
				rectTransform2.pivot = rectTransform.pivot;
				rectTransform2.sizeDelta = rectTransform.sizeDelta;
				if (flag2)
				{
					rectTransform2.anchoredPosition = rectTransform.anchoredPosition - new Vector2(rectTransform.rect.width + 12f, 0f);
				}
				else
				{
					float x = rectTransform.lossyScale.x;
					rectTransform2.position = position - new Vector3((rectTransform.rect.width + 12f) * x, 0f, 0f);
				}
			}
			_btn.GameObject.SetActive(value: true);
			_btnShown = true;
		}

		private static string Progress(Dictionary<string, object> r)
		{
			string text = ((long)Json.GetNumber(r, "evaluated", 0.0)).ToString(CultureInfo.InvariantCulture);
			string text2 = (Json.GetNumber(r, "elapsedMs", 0.0) / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
			return "求解中… " + text2 + " 秒，已算 " + text + " 场，当前最好：" + BestLine(r);
		}

		private void ApplySettings(Dictionary<string, object> req)
		{
			req["topN"] = _topN.Value;
			req["timeLimitMs"] = _timeLimitMs.Value;
			req["samples"] = Math.Max(1, _samples.Value);
			req["seed"] = _seed.Value;
		}

		private static string BestLine(Dictionary<string, object> r)
		{
			List<object> array = Json.GetArray(r, "best");
			if (array == null || array.Count == 0)
			{
				return "暂无";
			}
			List<object> list = ((array[0] is Dictionary<string, object> d) ? Json.GetArray(d, "orders") : null);
			if (list == null)
			{
				return "暂无";
			}
			string text = "";
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] is Dictionary<string, object> d2)
				{
					string text2 = "后手";
					if (Json.GetString(d2, "first", "") == "me")
					{
						text2 = "先手";
					}
					string text3 = "输";
					if (Json.GetString(d2, "result", "") == "win")
					{
						text3 = "赢";
					}
					string text4 = "";
					if (Json.GetBool(d2, "hadRandom", fallback: false))
					{
						int num = (int)Json.GetNumber(d2, "randomWins", 0.0);
						int num2 = (int)Json.GetNumber(d2, "randomRuns", 0.0);
						text4 = "（随机 " + num.ToString(CultureInfo.InvariantCulture) + "/" + num2.ToString(CultureInfo.InvariantCulture) + ")";
					}
					if (text.Length > 0)
					{
						text += " / ";
					}
					text = text + text2 + text3 + text4;
				}
			}
			return text;
		}

		private void RememberMe(Dictionary<string, object> req)
		{
			_pool.Clear();
			_charId = 0;
			_sect = 0;
			if (!(req["me"] is Dictionary<string, object> dictionary))
			{
				return;
			}
			_charId = (int)dictionary["characterId"];
			_sect = (int)dictionary["sect"];
			if (dictionary["cards"] is List<object> list)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_pool.Add((int)list[i]);
				}
			}
		}

		private static void Toast(string msg)
		{
			try
			{
				Ui.Toast("残局求解：" + msg);
			}
			catch (Exception)
			{
			}
		}

		private static Dictionary<string, object> Parse(string json)
		{
			try
			{
				return Json.ParseObject(json);
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
