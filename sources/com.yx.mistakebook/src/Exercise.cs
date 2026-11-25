using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.Shared;

namespace YxMistakeBook
{
    // The saved request is the immutable pre-battle snapshot. Each native call gets its own copy.
    internal sealed class Exercise
    {
        public const string Rules = "001.0008.0007";
        public const string LegacyRules = "001.0008.0006";
        public readonly Dictionary<string, object> Data;
        public readonly Dictionary<string, object> Request;
        public readonly int[] Pool, Original, Opponent;
        public string First { get { return Json.GetString(Request, "first", ""); } }
        public string Key { get { return Json.GetString(Data, "key", ""); } }
        public int Round { get { return Number(Request, "round"); } }
        public int[] Answer { get { return Cards(Data, "answer"); } }
        public int Attempts { get { return Number(Data, "attempts"); } }
        public string RulesVersion { get { return Json.GetString(Data, "rules", ""); } }
        public bool CanGrade { get { return RulesVersion == Rules; } }

        public Exercise(Dictionary<string, object> data)
        {
            if (data == null) throw new ArgumentException("缺少题目快照");
            Data = data;
            Request = Json.GetObject(data, "request");
            var me = Json.GetObject(Request, "me");
            Pool = Cards(me, "cards");
            Original = Cards(me, "initial");
            Opponent = Cards(Json.GetObject(Request, "foe"), "cards");
            if ((!CanGrade && RulesVersion != LegacyRules) || Key.Length == 0 ||
                Round < 1 || Original.Length < 1 || Original.Length > 10 ||
                Original.Length != Number(Request, "slots") || Opponent.Length < 1 || Opponent.Length > 10 ||
                (First != "me" && First != "foe") || Number(Request, "maxConsumeSustain", -1) < 0)
                throw new ArgumentException("题目快照不完整或规则版本不匹配");
        }

        public static Exercise TryRead(object row)
        {
            try
            {
                var exercise = new Exercise(row as Dictionary<string, object>);
                // Validate fields used when drawing the saved row, without changing its data.
                if (exercise.Attempts < 0 || exercise.Answer.Length > 10) return null;
                Number(Json.GetObject(exercise.Request, "me"), "characterId");
                Number(Json.GetObject(exercise.Request, "foe"), "characterId");
                return exercise;
            }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
            catch (InvalidCastException) { return null; }
            catch (OverflowException) { return null; }
        }

        void RequireCurrentRules()
        {
            if (!CanGrade) throw new InvalidOperationException("旧版题目只供查看，不用新版规则重新计分");
        }

        public static Exercise Create(string key, Dictionary<string, object> request)
        {
            return new Exercise(new Dictionary<string, object> {
                { "rules", Rules }, { "key", key }, { "request", request }
            });
        }

        public static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        public static int Number(Dictionary<string, object> data, string key, int fallback = 0)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) && value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
        }

        public static int[] Cards(Dictionary<string, object> data, string key)
        {
            var list = Json.GetArray(data, key);
            if (list == null) return new int[0];
            var cards = new int[list.Count];
            for (int i = 0; i < cards.Length; i++)
                cards[i] = Convert.ToInt32(list[i], CultureInfo.InvariantCulture);
            return cards;
        }

        public static List<object> ListOf(int[] cards)
        {
            var list = new List<object>();
            for (int i = 0; i < cards.Length; i++) list.Add(cards[i]);
            return list;
        }

        public int[] Remaining(int[] board)
        {
            if (board.Length != Original.Length) throw new ArgumentException("牌位数量不对");
            var used = new bool[Pool.Length];
            int count = Pool.Length;
            for (int i = 0; i < board.Length; i++)
            {
                if (board[i] == 0) continue;
                int j = 0;
                while (j < Pool.Length && (used[j] || Pool[j] != board[i])) j++;
                if (j == Pool.Length) throw new ArgumentException("摆入的牌超过当轮持有数量");
                used[j] = true;
                count--;
            }
            var rest = new int[count];
            int at = 0;
            for (int i = 0; i < Pool.Length; i++) if (!used[i]) rest[at++] = Pool[i];
            return rest;
        }

        public string Prediction(int[] board)
        {
            RequireCurrentRules();
            var hand = Remaining(board);
            var request = new Dictionary<string, object>(Request);
            var me = new Dictionary<string, object>(Json.GetObject(Request, "me"));
            me["cards"] = ListOf(board);
            me["handCards"] = ListOf(hand);
            request["me"] = me;
            request["samples"] = 1;
            request["deathFightMode"] = "record";
            return Json.Serialize(request);
        }

        public string Search()
        {
            var request = new Dictionary<string, object>(Request);
            RequireCurrentRules();
            request["searchMode"] = "existence";
            request["deathFightMode"] = "record";
            request["samples"] = 1;
            request["topN"] = 1;
            request["threads"] = 2;
            return Json.Serialize(request);
        }

        public int[] WinningBoard(Dictionary<string, object> report)
        {
            RequireCurrentRules();
            if (!Json.GetBool(report, "canWin", false)) throw new ArgumentException("本次搜索没有找到赢法");
            if (Json.GetBool(report, "hasRandom", true)) throw new ArgumentException("本轮搜索遇到随机结算，未收录");
            var best = Json.GetArray(report, "best");
            if (best != null) for (int i = 0; i < best.Count; i++)
            {
                var entry = best[i] as Dictionary<string, object>;
                var orders = Json.GetArray(entry, "orders");
                if (orders == null) continue;
                for (int j = 0; j < orders.Count; j++)
                {
                    var order = orders[j] as Dictionary<string, object>;
                    if (Json.GetString(order, "first", "") == First &&
                        Json.GetString(order, "result", "") == "win" && !Json.GetBool(order, "hadRandom", true))
                    {
                        int[] cards = Cards(entry, "cards");
                        Remaining(cards);
                        return cards;
                    }
                }
            }
            throw new ArgumentException("没有可复验的确定赢法");
        }

        public Dictionary<string, object> HistoricalRun(Dictionary<string, object> report)
        {
            RequireCurrentRules();
            var runs = Json.GetArray(report, "runs");
            if (runs != null) for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i] as Dictionary<string, object>;
                object order;
                if (run == null || !run.TryGetValue("meFirst", out order) || !(order is bool) || (bool)order != (First == "me")) continue;
                if (Json.GetBool(run, "random", true)) throw new ArgumentException("此摆法触发随机效果，本次不计分");
                if (!run.ContainsKey("winner") || !run.ContainsKey("myHp") || !run.ContainsKey("foeHp"))
                    throw new ArgumentException("缺少结算信息");
                object winner = run["winner"];
                if (winner != null && !object.Equals(winner, "L") && !object.Equals(winner, "R"))
                    throw new ArgumentException("无法识别结算胜方");
                return run;
            }
            throw new ArgumentException("缺少实战先后手的结算结果");
        }
    }
}
