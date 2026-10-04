using System.Collections.Generic;
using Yx.Shared;

namespace YxCanju
{
	public static class PuzzleSignature
	{
		public static string Of(Dictionary<string, object> req)
		{
			Dictionary<string, object> dictionary = req["me"] as Dictionary<string, object>;
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
			foreach (KeyValuePair<string, object> item in dictionary)
			{
				if (item.Key != "cards" && item.Key != "initial")
				{
					dictionary2[item.Key] = item.Value;
				}
			}
			List<int> list = new List<int>();
			if (dictionary["cards"] is List<object> list2)
			{
				for (int i = 0; i < list2.Count; i++)
				{
					list.Add((int)list2[i]);
				}
			}
			list.Sort();
			List<object> list3 = new List<object>();
			for (int j = 0; j < list.Count; j++)
			{
				list3.Add(list[j]);
			}
			dictionary2["pool"] = list3;
			return Json.Serialize(new Dictionary<string, object>
			{
				["round"] = req["round"],
				["slots"] = req["slots"],
				["max"] = req["maxConsumeSustain"],
				["seed"] = (req.ContainsKey("seed") ? req["seed"] : ((object)1)),
				["samples"] = (req.ContainsKey("samples") ? req["samples"] : ((object)3)),
				["me"] = dictionary2,
				["foe"] = req["foe"],
				["position"] = req["position"]
			});
		}
	}
}
