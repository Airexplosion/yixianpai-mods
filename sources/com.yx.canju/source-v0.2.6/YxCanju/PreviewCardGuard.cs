using System.Collections.Generic;

namespace YxCanju
{
	public static class PreviewCardGuard
	{
		public static bool IsPreview(int id)
		{
			int num = id / 10000 % 100;
			int num2 = id - num * 10000;
			if (num >= 0 && num <= 2)
			{
				if (num2 < 7009001 || num2 > 7009007)
				{
					if (num2 >= 10009001)
					{
						return num2 <= 10009008;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public static bool Contains(List<int> cards)
		{
			if (cards == null)
			{
				return false;
			}
			for (int i = 0; i < cards.Count; i++)
			{
				if (IsPreview(cards[i]))
				{
					return true;
				}
			}
			return false;
		}

		public static bool Contains(List<object> cards)
		{
			if (cards == null)
			{
				return false;
			}
			for (int i = 0; i < cards.Count; i++)
			{
				if (cards[i] is int && IsPreview((int)cards[i]))
				{
					return true;
				}
			}
			return false;
		}
	}
}
