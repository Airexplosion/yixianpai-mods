using System;
using System.Collections.Generic;
using Proto;
using TMPro;
using UnityEngine;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxMistakeBook
{
    public sealed partial class BookMod
    {
        GameObject _entry, _window;
        UiButton _collectButton;
        int _entryMode, _bookPage, _handPage, _cursor;
        int[] _board, _types;
        bool _reveal, _details, _confirmDelete;
        string _notice = "";
        const int BookPageSize = 5, HandPageSize = 10;

        void Destroy(ref GameObject root)
        {
            if (root != null) { root.SetActive(false); _ui.Destroy(root); }
            root = null;
        }

        void RefreshEntry()
        {
            if (_disabled || _ui == null) return;
            int mode = _visible ? 0 : RecordOpen ? 2 : AtHome ? 1 : 0;
            if (mode != _entryMode || (mode != 0 && _entry == null))
            {
                Destroy(ref _entry);
                _entryMode = mode;
                _collectButton = null;
                if (mode != 0)
                {
                    var anchor = new Vector2(1f, 0f);
                    float width = mode == 2 ? 316 : 145;
                    _entry = _ui.Panel("MistakeBookEntry", anchor, anchor, new Vector2(-16, 80),
                        new Vector2(width, 48), new Color(0.12f, 0.13f, 0.13f, 0.94f));
                    if (_entry == null) return;
                    _ui.TextButton(_entry.transform, "book", "败局错题本", new Vector2(4,-4), new Vector2(137,40), ToggleBook);
                    if (mode == 2) _collectButton = _ui.TextButton(_entry.transform, "collect", "收藏本轮",
                        new Vector2(146,-4), new Vector2(166,40), Collect);
                }
            }
            if (_collectButton != null) _collectButton.SetText(_capture == null ? "收藏本轮" : "取消验题");
        }

        void ToggleBook()
        {
            if (_visible) { CloseBook(); return; }
            if (!AtHome && !RecordOpen) { Ui.Toast("请在大厅或战绩详情打开错题本"); return; }
            StopCapture();
            _visible = true;
            _bookPage = 0;
            Library();
            RefreshEntry();
        }

        void CloseBook()
        {
            StopJob();
            _visible = false;
            _active = null;
            Destroy(ref _window);
            RefreshEntry();
        }

        void Library()
        {
            StopJob();
            _active = null;
            _notice = "";
            _confirmDelete = false;
            _details = false;
            Draw();
        }

        void Draw()
        {
            Destroy(ref _window);
            if (!_visible) return;
            var center = new Vector2(0.5f, 0.5f);
            _window = _ui.Panel("MistakeNotebook", center, center, Vector2.zero, new Vector2(900, 620),
                new Color(0.085f, 0.105f, 0.105f, 0.98f));
            if (_window == null) { CloseBook(); return; }
            var parent = _window.transform.parent as RectTransform;
            if (parent != null && parent.rect.width > 0 && parent.rect.height > 0)
            {
                float scale = Mathf.Min(1f, Mathf.Min(parent.rect.width / 920f, parent.rect.height / 640f));
                _window.transform.localScale = new Vector3(scale, scale, 1);
            }
            Button("关闭", 794, 12, 86, 38, CloseBook);
            if (_active == null) DrawLibrary();
            else
            {
                Label((_active.Key == Sample.Key ? "示例　" : "") + Title(_active), 22, 14, 745, 40, 26);
                if (_details) DrawDetails();
                else if (_reveal) DrawResult();
                else DrawBoard();
            }
        }

        void Label(string text, int x, int y, int w, int h, int size)
        {
            var label = _ui.Label(_window.transform, "text", text, new Vector2(x,-y), new Vector2(w,h), size);
            label.richText = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = Math.Min(16, size);
            label.fontSizeMax = size;
        }

        UiButton Button(string text, int x, int y, int w, int h, Action action)
        {
            var button = _ui.TextButton(_window.transform, text, text, new Vector2(x,-y), new Vector2(w,h), action);
            var label = button.GameObject.GetComponentInChildren(typeof(TextMeshProUGUI), true) as TextMeshProUGUI;
            if (label != null) { label.richText = false; label.enableAutoSizing = true; label.fontSizeMin = 15; label.fontSizeMax = 22; }
            return button;
        }

        string Title(Exercise e)
        {
            var me = Json.GetObject(e.Request, "me");
            var foe = Json.GetObject(e.Request, "foe");
            string order = e.First == "me" ? "我方先手" : "我方后手";
            return "第 " + Exercise.N(e.Round) + " 轮　" + Character(me) + " 对 " + Character(foe) + "　" + order;
        }

        static string Character(Dictionary<string, object> side)
        {
            int id = Exercise.Number(side, "characterId");
            try { string name = TranslateUtil.GetCharacterNameTranslate(id); if (!string.IsNullOrEmpty(name)) return name; }
            catch (Exception) { }
            return "角色 " + Exercise.N(id);
        }

        static string Card(int id)
        {
            if (id == 0) return "普攻";
            var config = CardFactory.FindCardConfig(id);
            string name = config == null || config.id != id ? "" : config.name;
            if (string.IsNullOrEmpty(name)) return "#" + Exercise.N(id);
            return name + "·" + Exercise.N(id / 10000 % 100 + 1);
        }

        static string Line(int[] cards)
        {
            string result = "";
            for (int i = 0; i < cards.Length; i++) result += (i == 0 ? "" : " → ") + Card(cards[i]);
            return result;
        }

        void DrawLibrary()
        {
            Label("败局错题本", 22, 14, 620, 40, 30);
            if (_book.Count == 0)
                Label("还没有收藏。打开自己的战绩详情，选中输掉的一轮，点「收藏本轮」。", 26, 115, 830, 80, 24);
            int pages = Math.Max(1, (_book.Count + BookPageSize - 1) / BookPageSize);
            _bookPage = Math.Max(0, Math.Min(_bookPage, pages - 1));
            for (int row = 0; row < BookPageSize; row++)
            {
                int index = _bookPage * BookPageSize + row;
                if (index >= _book.Count) break;
                var e = new Exercise(_book[index] as Dictionary<string, object>);
                string state = Json.GetBool(e.Data, "passed", false) ? "已攻克" : e.Attempts > 0 ? "待再练" : "未做";
                string text = Title(e) + "\n" + Json.GetString(e.Data, "saved", "") + "　" + state + "　" + Exercise.N(e.Attempts) + " 次";
                Button(text, 22, 66 + row * 83, 856, 72, delegate { OpenExercise(e); });
            }
            Label(_notice, 22, 488, 850, 44, 18);
            Button("上一页", 22, 549, 126, 45, delegate { _bookPage--; Draw(); }).SetInteractable(_bookPage > 0);
            Label(Exercise.N(_bookPage + 1) + " / " + Exercise.N(pages) + " 页", 166, 557, 134, 35, 20);
            Button("下一页", 301, 549, 126, 45, delegate { _bookPage++; Draw(); }).SetInteractable(_bookPage + 1 < pages);
            Button("试做示例", 692, 549, 180, 45, delegate { OpenExercise(Sample.Create()); });
        }

        void OpenExercise(Exercise e)
        {
            _active = e;
            _board = new int[e.Original.Length];
            // Sort unique IDs so the original board prefix in the captured pool cannot give away its order.
            var sorted = (int[])e.Pool.Clone();
            Array.Sort((Array)sorted);
            int count = 0;
            for (int i = 0; i < sorted.Length; i++)
                if (count == 0 || sorted[count - 1] != sorted[i]) sorted[count++] = sorted[i];
            _types = new int[count];
            Array.Copy(sorted, _types, count);
            _cursor = 0; _handPage = 0; _reveal = false; _details = false; _confirmDelete = false;
            _notice = "";
            Draw();
        }

        void DrawBoard()
        {
            bool editing = _job == null;
            Label("对手牌序　" + Line(_active.Opponent), 22, 59, 850, 62, 21);
            Label("我的牌序　选中一格，再点手牌放入。空格按普通攻击结算。", 22, 127, 720, 31, 20);
            for (int i = 0; i < _board.Length; i++)
            {
                int slot = i;
                string marker = slot == _cursor ? "▶ " : "";
                Button(marker + Exercise.N(slot + 1) + "　" + Card(_board[slot]), 22 + slot % 5 * 172,
                    162 + slot / 5 * 46, 165, 42, delegate { _cursor = slot; Draw(); }).SetInteractable(editing);
            }
            Label("当轮可用手牌", 22, 268, 355, 34, 22);
            var remaining = _active.Remaining(_board);
            int pages = Math.Max(1, (_types.Length + HandPageSize - 1) / HandPageSize);
            Button("上页", 572, 261, 84, 37, delegate { _handPage--; Draw(); }).SetInteractable(editing && _handPage > 0);
            Label(Exercise.N(_handPage + 1) + " / " + Exercise.N(pages), 669, 267, 90, 30, 19);
            Button("下页", 777, 261, 92, 37, delegate { _handPage++; Draw(); }).SetInteractable(editing && _handPage + 1 < pages);
            for (int j = 0; j < HandPageSize && _handPage * HandPageSize + j < _types.Length; j++)
            {
                int id = _types[_handPage * HandPageSize + j];
                int count = 0;
                for (int k = 0; k < remaining.Length; k++) if (remaining[k] == id) count++;
                Button(Card(id) + " ×" + Exercise.N(count), 22 + j % 5 * 172, 311 + j / 5 * 49, 165, 45,
                    delegate { Place(id); }).SetInteractable(editing && count > 0 && Placeable(id));
            }
            Button("本格留空", 22, 421, 150, 44, delegate { Place(0); }).SetInteractable(editing);
            Button("双方状态", 183, 421, 150, 44, delegate { _details = true; Draw(); }).SetInteractable(editing);
            Label(_notice, 22, 471, 858, 62, 19);
            Button("回题本", 22, 550, 130, 47, Library);
            if (_active.Key != Sample.Key)
                Button(_confirmDelete ? "确认移除" : "移除本题", 168, 550, 136, 47, Remove).SetInteractable(editing);
            Button(editing ? "交卷验算" : "正在验算", 675, 546, 201, 52, Submit).SetInteractable(editing);
        }

        static bool Placeable(int id)
        {
            if (id == 0) return true;
            var c = CardFactory.FindCardConfig(id);
            return id != 1 && c != null && c.id == id && c.cardType != CardType.Refine && c.cardType != CardType.Change;
        }

        void Place(int id)
        {
            if (_job != null || _reveal) return;
            _board[_cursor] = id;
            _cursor = (_cursor + 1) % _board.Length;
            _notice = ""; _confirmDelete = false;
            Draw();
        }

        void Submit()
        {
            if (_job != null || _reveal) return;
            int limited = 0;
            for (int i = 0; i < _board.Length; i++)
            {
                int id = _board[i];
                if (!Placeable(id)) { _notice = "牌桌中含有不能上阵的牌"; Draw(); return; }
                if (id == 0) continue;
                var type = CardFactory.FindCardConfig(id).cardType;
                if (type == CardType.Consume || type == CardType.Sustain) limited++;
            }
            if (limited > Exercise.Number(_active.Request, "maxConsumeSustain"))
            { _notice = "消耗牌和持续牌超过当轮上限，请调整后再交卷"; Draw(); return; }
            _notice = "正在按历史先后手结算……";
            Run("predict", _active.Prediction(_board), Graded);
            Draw();
        }

        void Graded(Dictionary<string, object> report)
        {
            var run = _active.HistoricalRun(report);
            string winner = Json.GetString(run, "winner", "");
            bool won = winner == "L";
            var updated = new Dictionary<string, object>(_active.Data);
            updated["attempts"] = _active.Attempts + 1;
            updated["passed"] = won || Json.GetBool(updated, "passed", false);
            if (_active.Key != Sample.Key)
            {
                var next = new List<object>(_book);
                next[Find(_active.Key)] = updated;
                Save(next);
            }
            _active = new Exercise(updated);
            _notice = (won ? "翻盘成功" : winner == "R" ? "本次未翻盘" : "本次平局") +
                "　｜　终局生命　我方 " + Exercise.N(Exercise.Number(run, "myHp")) + "　对手 " + Exercise.N(Exercise.Number(run, "foeHp"));
            _job = null; _reveal = true;
            Draw();
        }

        void DrawResult()
        {
            Label(_notice, 22, 75, 848, 60, 26);
            Label("本次提交", 22, 149, 220, 35, 22);
            Label(Line(_board), 22, 190, 848, 69, 22);
            Label(_active.Key == Sample.Key ? "示例原摆法" : "实战原摆法", 22, 269, 250, 35, 22);
            Label(Line(_active.Original), 22, 310, 848, 68, 22);
            Label("一种已复验的赢法", 22, 398, 325, 35, 22);
            Label(Line(_active.Answer), 22, 439, 848, 70, 22);
            Button("回题本", 22, 550, 140, 47, Library);
            Button("重新盲摆", 681, 550, 192, 47, delegate { OpenExercise(_active); });
        }

        static string Skills(Dictionary<string, object> side, string key)
        {
            int[] ids = Exercise.Cards(side, key);
            string text = "";
            for (int i = 0; i < ids.Length; i++)
            {
                string name = "";
                try { name = key == "talents" ? TranslateUtil.GetTalentTranslate(ids[i]) : TranslateUtil.GetFateStrategyNameTranslate(ids[i]); }
                catch (Exception) { }
                text += (i == 0 ? "" : "、") + (string.IsNullOrEmpty(name) ? Exercise.N(ids[i]) : name);
            }
            return text.Length == 0 ? "无" : text;
        }

        void DrawDetails()
        {
            for (int i = 0; i < 2; i++)
            {
                var side = Json.GetObject(_active.Request, i == 0 ? "me" : "foe");
                int y = 78 + i * 220;
                Label((i == 0 ? "我方　" : "对手　") + Character(side) + "　修为 " + Exercise.N(Exercise.Number(side,"exp")) +
                    "　命元 " + Exercise.N(Exercise.Number(side,"life")), 24, y, 850, 46, 24);
                Label("仙命　" + Skills(side, "talents") + "\n天衍　" + Skills(side, "fate"), 24, y + 50, 850, 151, 20);
            }
            Button("继续摆牌", 681, 550, 192, 47, delegate { _details = false; Draw(); });
        }

        void Remove()
        {
            if (!_confirmDelete) { _confirmDelete = true; _notice = "再次点击确认移除这道题"; Draw(); return; }
            var next = new List<object>(_book);
            next.RemoveAt(Find(_active.Key));
            Save(next);
            Library();
        }
    }
}
