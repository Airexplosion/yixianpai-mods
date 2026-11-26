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
    /// <summary>
    /// 结果面板（屏幕右侧，不挡点击）：进度 + 前几名摆法，每名列出各格牌名与先手 / 后手的胜负、双方剩血。
    /// 只显示，不操作游戏。
    /// </summary>
    public sealed class ResultView
    {
        const float W = 650f;
        const float H = 810f;
        static readonly Color Ink = new Color(0.075f, 0.115f, 0.115f, 0.98f);
        static readonly Color Jade = new Color(0.13f, 0.22f, 0.21f, 1f);
        static readonly Color Gold = new Color(0.78f, 0.67f, 0.45f, 1f);

        readonly UiKit _ui;
        readonly ModContext _ctx;
        GameObject _root;
        TextMeshProUGUI _text, _title, _page;
        UiButton _prev, _next;
        Vector2 _canvasSize;

        public ResultView(ModContext ctx)
        {
            _ctx = ctx;
            _ui = new UiKit(ctx);
        }

        public void Status(string line)
        {
            SetText("求解进度", line);
            try { Ui.Toast("残局求解：" + line); } catch (Exception) { }
        }

        /// <summary>把一次 solve_poll 的报告画出来。</summary>
        public void Show(Dictionary<string, object> r)
        {
            var sb = new StringBuilder();
            char nl = (char)10;
            string state = Json.GetString(r, "state", "");
            string stateText = StateText(state);
            long ms = (long)Json.GetNumber(r, "elapsedMs", 0);
            long evaluated = (long)Json.GetNumber(r, "evaluated", 0);
            double candidates = Json.GetNumber(r, "candidates", 0);
            sb.Append("残局求解：").Append(stateText);
            sb.Append("　").Append(Seconds(ms)).Append(" 秒，已算 ").Append(evaluated.ToString(CultureInfo.InvariantCulture)).Append(" 场");
            sb.Append(nl);
            sb.Append("候选摆法约 ").Append(Big(candidates));
            if (Json.GetBool(r, "exhaustive", false)) sb.Append("（已穷举，全局最优）");
            sb.Append("　种子 ").Append(((int)Json.GetNumber(r, "seed", 1)).ToString(CultureInfo.InvariantCulture));
            sb.Append(nl);
            string err = Json.GetString(r, "error", null);
            if (!string.IsNullOrEmpty(err)) sb.Append("错误：").Append(err).Append(nl);

            List<object> best = Json.GetArray(r, "best");
            if (best == null || best.Count == 0) sb.Append("（还没有结果）");
            else
                for (int i = 0; i < best.Count; i++)
                {
                    var e = best[i] as Dictionary<string, object>;
                    if (e == null) continue;
                    AppendEntry(sb, i + 1, e, nl);
                }
            sb.Append(nl).Append("仅为模拟建议。Ctrl+Alt+J 打开秘籍，点「求解」重新计算。");
            SetText("求解结果", sb.ToString());
        }

        public void ShowEntry(Dictionary<string,object> report, int index)
        {
            var entries = Json.GetArray(report,"best");
            if (entries == null || index < 0 || index >= entries.Count) return;
            var entry = entries[index] as Dictionary<string,object>;
            if (entry == null) return;
            var sb = new StringBuilder();
            AppendEntry(sb,index+1,entry,(char)10, false);
            SetText("方案 " + (index + 1).ToString(CultureInfo.InvariantCulture) + "　操作详情", sb.ToString());
        }

        internal static string EnchantText(Dictionary<string, object> entry)
        {
            var grids = Json.GetObject(entry, "enchantGrids");
            if (grids == null || grids.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var pair in grids)
            {
                int id = int.Parse(pair.Key, CultureInfo.InvariantCulture);
                string name = XianMoTranslateUtil.GetStrategyNameTranslate(id);
                if (string.IsNullOrEmpty(name)) name = pair.Key;
                int slot = Convert.ToInt32(pair.Value, CultureInfo.InvariantCulture) + 1;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(name).Append("选牌桌第 ").Append(slot.ToString(CultureInfo.InvariantCulture)).Append(" 格");
            }
            return sb.ToString();
        }

        static void Steps(StringBuilder sb, Dictionary<string,object> entry, string key, string label, char nl)
        {
            var list = Json.GetArray(entry,key);
            if (list == null || list.Count == 0) return;
            Section(sb, label, nl);
            for (int i=0; i<list.Count; i++)
            {
                int id = Convert.ToInt32(list[i],CultureInfo.InvariantCulture);
                string name = id == 0 ? "留空" : Name(id);
                if (key == "keyin" && id != 0)
                {
                    var c = KeYinCardFactory.FindCardConfig(id);
                    name = c == null ? id.ToString(CultureInfo.InvariantCulture) : c.name;
                }
                if (key == "keyin" || key == "bottle") Slot(sb, i);
                else sb.Append("·  ");
                sb.Append(name).Append(nl);
            }
        }

        static void Section(StringBuilder sb, string label, char nl)
        {
            sb.Append(nl).Append("<color=#E9CD92><b>").Append(label).Append("</b></color>").Append(nl);
        }

        static void Slot(StringBuilder sb, int index)
        {
            sb.Append("<color=#A0CBC1>第 ").Append((index + 1).ToString(CultureInfo.InvariantCulture))
              .Append(" 格</color>　");
        }

        static void AppendEntry(StringBuilder sb, int rank, Dictionary<string, object> e, char nl, bool showRank = true)
        {
            if (showRank) Section(sb, "方案 " + rank.ToString(CultureInfo.InvariantCulture), nl);
            List<object> orders = Json.GetArray(e, "orders");
            if (orders != null)
                for (int k = 0; k < orders.Count; k++)
                {
                    var o = orders[k] as Dictionary<string, object>;
                    if (o == null) continue;
                    string first = Json.GetString(o, "first", "");
                    string res = Json.GetString(o, "result", "");
                    string who = "后手";
                    if (first == "me") who = "先手";
                    string verdict = "输";
                    if (res == "win") verdict = "赢";
                    int my = (int)Json.GetNumber(o, "myHp", 0);
                    int foe = (int)Json.GetNumber(o, "foeHp", 0);
                    string random = RandomText(o);
                    sb.Append(res == "win" ? "<color=#A8DDC4>" : "<color=#F1B3A9>")
                      .Append(who).Append(verdict).Append("</color>　我方剩血 ").Append(my.ToString(CultureInfo.InvariantCulture))
                      .Append(" / 对手 ").Append(foe.ToString(CultureInfo.InvariantCulture)).Append(random).Append(nl);
                }
            string grids = EnchantText(e);
            if (grids.Length > 0)
            {
                Section(sb, "刻纹位置", nl);
                sb.Append(grids).Append(nl);
            }
            Steps(sb,e,"keyin","刻印栏从左到右",nl);
            Steps(sb,e,"bottle","玉瓶从第一格起",nl);
            Steps(sb,e,"refined","需要炼化（桌上的牌先收回）",nl);
            Section(sb, "最终牌桌", nl);
            List<object> cards = Json.GetArray(e, "cards");
            for (int k = 0; cards != null && k < cards.Count; k++)
            {
                int id = (int)Convert.ToDouble(cards[k], CultureInfo.InvariantCulture);
                Slot(sb, k);
                sb.Append(Name(id)).Append(nl);
            }
            Steps(sb,e,"remainingHand","保留手牌",nl);
        }

        static string RandomText(Dictionary<string, object> order)
        {
            if (!Json.GetBool(order, "hadRandom", false)) return "";
            int wins = (int)Json.GetNumber(order, "randomWins", 0);
            int runs = (int)Json.GetNumber(order, "randomRuns", 0);
            return "　抽样胜场 " + wins.ToString(CultureInfo.InvariantCulture) + "/" + runs.ToString(CultureInfo.InvariantCulture);
        }

        static string Name(int id)
        {
            if (id == 0) return "（空格 / 普攻）";
            string n = null;
            try { n = CardUi.CardName(id); } catch (Exception) { }
            if (string.IsNullOrEmpty(n)) n = id.ToString(CultureInfo.InvariantCulture);
            int lv = id / 10000 % 100;
            if (lv > 0) n = n + "+" + lv.ToString(CultureInfo.InvariantCulture);
            return n;
        }

        static string StateText(string state)
        {
            if (state == "done") return "完成";
            if (state == "cancelled") return "已取消";
            if (state == "error") return "出错";
            return "计算中…";
        }

        static string Seconds(long ms)
        {
            return (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture);
        }

        static string Big(double v)
        {
            if (v >= 1e8) return (v / 1e8).ToString("0.0", CultureInfo.InvariantCulture) + " 亿";
            if (v >= 1e4) return (v / 1e4).ToString("0.0", CultureInfo.InvariantCulture) + " 万";
            return ((long)v).ToString(CultureInfo.InvariantCulture);
        }

        void SetText(string title, string text)
        {
            try
            {
                if (!Build()) return;
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
                _title.text = title;
                _text.pageToDisplay = 1;
                _text.text = text.TrimEnd();
                FitToCanvas();
                _text.ForceMeshUpdate();
                SetPage(1);
            }
            catch (Exception e)
            {
                _ctx.Log.Warn("结果面板显示失败：" + e.Message);
            }
        }

        void SetPage(int requested)
        {
            int count = Math.Max(1, _text.textInfo.pageCount);
            int page = Math.Max(1, Math.Min(requested, count));
            _text.pageToDisplay = page;
            _page.text = page.ToString(CultureInfo.InvariantCulture) + " / " + count.ToString(CultureInfo.InvariantCulture);
            _prev.SetInteractable(page > 1);
            _next.SetInteractable(page < count);
        }

        void Prev() { SetPage(_text.pageToDisplay - 1); }
        void Next() { SetPage(_text.pageToDisplay + 1); }
        void Hide() { if (_root != null) _root.SetActive(false); }

        public void Tick()
        {
            if (_root == null || !_root.activeInHierarchy || !FitToCanvas()) return;
            int page = _text.pageToDisplay;
            _text.ForceMeshUpdate();
            SetPage(page);
        }

        bool FitToCanvas()
        {
            var parent = _root.transform.parent as RectTransform;
            if (parent == null) return false;
            var available = parent.rect.size;
            if (available == _canvasSize || available.x < 1f || available.y < 1f) return false;
            _canvasSize = available;
            // Reflow and paginate at the chosen font size before shrinking a very small window.
            float scale = Mathf.Min(1f, Mathf.Min(available.x / 480f, available.y / 420f));
            var rt = _root.transform as RectTransform;
            rt.localScale = Vector3.one * scale;
            rt.anchoredPosition = new Vector2(-12f * scale, 0f);
            rt.sizeDelta = new Vector2(Mathf.Min(W, available.x / scale - 24f), Mathf.Min(H, available.y / scale - 24f));
            return true;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = insetMin;
            rt.offsetMax = insetMax;
        }

        bool Build()
        {
            if (_root != null) return true;
            var right = new Vector2(1f, 0.5f);
            _root = _ui.Panel("YxCanjuResult", right, right, Vector2.zero, new Vector2(W, H), Ink, true);
            if (_root == null) return false;
            var root = _root.transform;
            var top = _ui.Row(root, "Header", Vector2.zero, Vector2.zero, Jade);
            Stretch(top.transform as RectTransform, Vector2.up, Vector2.one, new Vector2(0f,-78f), Vector2.zero);
            var rule = _ui.Row(top.transform, "Divider", Vector2.zero, Vector2.zero, Gold);
            Stretch(rule.transform as RectTransform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0f,2f));
            _title = _ui.Label(top.transform, "Title", "", new Vector2(22f,-8f), new Vector2(320f,60f), 32f);
            _title.alignment = TextAlignmentOptions.MidlineLeft;
            _title.fontStyle = FontStyles.Bold;
            _title.enableWordWrapping = false;
            SolverButtonStyle.Create(_ui, top.transform, "Close", "关闭", Hide, right,
                new Vector2(-60f,0f), new Vector2(86f,46f), 23f, _title.font);

            _text = _ui.Label(root, "Steps", "", Vector2.zero, Vector2.zero, 27f);
            Stretch(_text.transform.parent as RectTransform, Vector2.zero, Vector2.one,
                new Vector2(22f,82f), new Vector2(-22f,-94f));
            _text.color = new Color(0.96f, 0.95f, 0.89f, 1f);
            _text.enableAutoSizing = false;
            _text.richText = true;
            _text.lineSpacing = 7f;
            _text.enableWordWrapping = true;
            _text.overflowMode = TextOverflowModes.Page;

            var foot = _ui.Row(root, "Footer", Vector2.zero, Vector2.zero, Jade);
            Stretch(foot.transform as RectTransform, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0f,70f));
            _page = _ui.Label(foot.transform, "Page", "", Vector2.zero, Vector2.zero, 24f);
            Stretch(_page.transform.parent as RectTransform, Vector2.zero, Vector2.one,
                new Vector2(132f,0f), new Vector2(-132f,0f));
            _page.alignment = TextAlignmentOptions.Center;
            _page.color = new Color(0.84f, 0.85f, 0.77f, 1f);
            var size = new Vector2(112f, 46f);
            _prev = SolverButtonStyle.Create(_ui, foot.transform, "Prev", "上一页", Prev,
                new Vector2(0f,0.5f), new Vector2(76f,0f), size, 23f, _title.font);
            _next = SolverButtonStyle.Create(_ui, foot.transform, "Next", "下一页", Next,
                right, new Vector2(-76f,0f), size, 23f, _title.font);
            return true;
        }

        public void Close()
        {
            _ui.DestroyAll();
            _root = null;
            _text = null;
        }
    }
}
