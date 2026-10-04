using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Game;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxCanju
{
	public sealed class ResultView
	{
		private const float W = 470f;

		private const float H = 560f;

		private readonly UiKit _ui;

		private readonly ModContext _ctx;

		private GameObject _root;

		private TextMeshProUGUI _text;

		public ResultView(ModContext ctx)
		{
			_ctx = ctx;
			_ui = new UiKit(ctx);
		}

		public void Status(string line)
		{
			SetText(line);
			try
			{
				Ui.Toast("残局求解：" + line);
			}
			catch (Exception)
			{
			}
		}

		public void Show(Dictionary<string, object> r)
		{
			StringBuilder stringBuilder = new StringBuilder();
			char c = '\n';
			string value = StateText(Json.GetString(r, "state", ""));
			long ms = (long)Json.GetNumber(r, "elapsedMs", 0.0);
			long num = (long)Json.GetNumber(r, "evaluated", 0.0);
			double number = Json.GetNumber(r, "candidates", 0.0);
			stringBuilder.Append("残局求解：").Append(value);
			stringBuilder.Append("\u3000").Append(Seconds(ms)).Append(" 秒，已算 ")
				.Append(num.ToString(CultureInfo.InvariantCulture))
				.Append(" 场");
			stringBuilder.Append(c);
			stringBuilder.Append("候选摆法约 ").Append(Big(number));
			if (Json.GetBool(r, "exhaustive", fallback: false))
			{
				stringBuilder.Append("（已穷举，全局最优）");
			}
			stringBuilder.Append("\u3000种子 ").Append(((int)Json.GetNumber(r, "seed", 1.0)).ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append(c);
			string value2 = Json.GetString(r, "error", null);
			if (!string.IsNullOrEmpty(value2))
			{
				stringBuilder.Append("错误：").Append(value2).Append(c);
			}
			List<object> array = Json.GetArray(r, "best");
			if (array == null || array.Count == 0)
			{
				stringBuilder.Append("（还没有结果）");
			}
			else
			{
				for (int i = 0; i < array.Count; i++)
				{
					if (array[i] is Dictionary<string, object> e)
					{
						AppendEntry(stringBuilder, i + 1, e, c);
					}
				}
			}
			stringBuilder.Append(c).Append("仅为模拟建议，请自己摆牌。再按 Ctrl+Alt+J：算着时取消 / 算完重算");
			SetText(stringBuilder.ToString());
		}

		private static void AppendEntry(StringBuilder sb, int rank, Dictionary<string, object> e, char nl)
		{
			sb.Append(nl).Append("第 ").Append(rank.ToString(CultureInfo.InvariantCulture))
				.Append(" 名\u3000");
			List<object> array = Json.GetArray(e, "orders");
			if (array != null)
			{
				for (int i = 0; i < array.Count; i++)
				{
					if (array[i] is Dictionary<string, object> dictionary)
					{
						string text = Json.GetString(dictionary, "first", "");
						string text2 = Json.GetString(dictionary, "result", "");
						string value = "后手";
						if (text == "me")
						{
							value = "先手";
						}
						string value2 = "输";
						if (text2 == "win")
						{
							value2 = "赢";
						}
						int num = (int)Json.GetNumber(dictionary, "myHp", 0.0);
						int num2 = (int)Json.GetNumber(dictionary, "foeHp", 0.0);
						string value3 = RandomText(dictionary);
						sb.Append(value).Append(value2).Append("(")
							.Append(num.ToString(CultureInfo.InvariantCulture))
							.Append(":")
							.Append(num2.ToString(CultureInfo.InvariantCulture))
							.Append(value3)
							.Append(")\u3000");
					}
				}
			}
			sb.Append(nl);
			List<object> array2 = Json.GetArray(e, "cards");
			if (array2 != null)
			{
				for (int j = 0; j < array2.Count; j++)
				{
					int id = (int)Convert.ToDouble(array2[j], CultureInfo.InvariantCulture);
					sb.Append("  ").Append((j + 1).ToString(CultureInfo.InvariantCulture)).Append(". ")
						.Append(Name(id))
						.Append(nl);
				}
			}
		}

		private static string RandomText(Dictionary<string, object> order)
		{
			if (!Json.GetBool(order, "hadRandom", fallback: false))
			{
				return "";
			}
			int num = (int)Json.GetNumber(order, "randomWins", 0.0);
			int num2 = (int)Json.GetNumber(order, "randomRuns", 0.0);
			return ";随机 " + num.ToString(CultureInfo.InvariantCulture) + "/" + num2.ToString(CultureInfo.InvariantCulture);
		}

		private static string Name(int id)
		{
			if (id == 0)
			{
				return "（空格 / 普攻）";
			}
			string text = null;
			try
			{
				text = CardUi.CardName(id);
			}
			catch (Exception)
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				text = id.ToString(CultureInfo.InvariantCulture);
			}
			int num = id / 10000 % 100;
			if (num > 0)
			{
				text = text + "+" + num.ToString(CultureInfo.InvariantCulture);
			}
			return text;
		}

		private static string StateText(string state)
		{
			switch (state)
			{
			case "done":
				return "完成";
			case "cancelled":
				return "已取消";
			case "error":
				return "出错";
			default:
				return "计算中…";
			}
		}

		private static string Seconds(long ms)
		{
			return ((double)ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
		}

		private static string Big(double v)
		{
			if (v >= 100000000.0)
			{
				return (v / 100000000.0).ToString("0.0", CultureInfo.InvariantCulture) + " 亿";
			}
			if (v >= 10000.0)
			{
				return (v / 10000.0).ToString("0.0", CultureInfo.InvariantCulture) + " 万";
			}
			return ((long)v).ToString(CultureInfo.InvariantCulture);
		}

		private void SetText(string text)
		{
			try
			{
				if (Build())
				{
					_root.SetActive(value: true);
					_text.text = text;
				}
			}
			catch (Exception ex)
			{
				_ctx.Log.Warn("结果面板显示失败：" + ex.Message);
			}
		}

		private bool Build()
		{
			if (_root != null)
			{
				return true;
			}
			Vector2 vector = new Vector2(1f, 0.5f);
			_root = _ui.Panel("YxCanjuResult", vector, vector, new Vector2(-12f, 0f), new Vector2(470f, 560f), new Color(0.05f, 0.07f, 0.12f, 0.88f), blockClicks: false);
			if (_root == null)
			{
				return false;
			}
			_text = _ui.Label(_root.transform, "text", "", new Vector2(12f, -10f), new Vector2(446f, 540f), 17f);
			if (_text != null)
			{
				_text.alignment = TextAlignmentOptions.TopLeft;
			}
			return _text != null;
		}

		public void Close()
		{
			_ui.DestroyAll();
			_root = null;
			_text = null;
		}
	}
}
