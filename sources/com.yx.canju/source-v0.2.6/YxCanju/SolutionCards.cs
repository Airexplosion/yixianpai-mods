using System;
using System.Collections.Generic;
using System.Globalization;
using Yx.Shared;

namespace YxCanju
{
	public static class SolutionCards
	{
		public static int[] Get(Dictionary<string, object> report, int index)
		{
			if (report == null || index < 0)
			{
				return null;
			}
			List<object> array = Json.GetArray(report, "best");
			if (array == null || index >= array.Count)
			{
				return null;
			}
			List<object> list = ((!(array[index] is Dictionary<string, object> d)) ? null : Json.GetArray(d, "cards"));
			if (list == null || list.Count == 0)
			{
				return null;
			}
			int[] array2 = new int[list.Count];
			for (int i = 0; i < list.Count; i++)
			{
				array2[i] = (int)Convert.ToDouble(list[i], CultureInfo.InvariantCulture);
			}
			return array2;
		}
	}
}
