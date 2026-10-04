using System;
using System.Collections.Generic;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxRecordExtras
{
	public sealed class RecordExtrasMod : YxMod
	{
		private readonly List<RecordPlayerExtras> _views = new List<RecordPlayerExtras>();

		private ModLog _log;

		public override void OnLoad(ModContext ctx)
		{
			_log = ctx.Log;
			HookGroup val = ctx.Hooks.Group("record-details");
			val.Postfix("PlayerBattleRoundInfoItem", "Refresh", 3, (Action<HookContext>)OnPlayerRefresh);
			val.Postfix("RecordDetailPanel", "OnHide", 0, (Action<HookContext>)OnRecordHide);
			if (!val.Complete)
			{
				ctx.Log.Warn("战绩详情补充信息未能安装刷新钩子：" + val.Missing);
			}
			else
			{
				ctx.Log.Info("战绩详情补充信息已启用 v" + ctx.Version);
			}
		}

		private void OnPlayerRefresh(HookContext h)
		{
			try
			{
				PlayerBattleRoundInfoItem playerBattleRoundInfoItem = h.Instance as PlayerBattleRoundInfoItem;
				RecentBattleInfo recentBattleInfo = ((h.Args != null && h.Args.Length != 0) ? (h.Args[0] as RecentBattleInfo) : null);
				int num = ((h.Args != null && h.Args.Length > 1 && h.Args[1] is int) ? ((int)h.Args[1]) : (-1));
				bool flag = h.Args != null && h.Args.Length > 2 && h.Args[2] is bool && (bool)h.Args[2];
				if (playerBattleRoundInfoItem != null && recentBattleInfo != null && num >= 0 && num < recentBattleInfo.roundStats.Count)
				{
					PlayerData data = (flag ? recentBattleInfo.roundStats[num].p1 : recentBattleInfo.roundStats[num].p2);
					RecordPlayerExtras recordPlayerExtras = Find(playerBattleRoundInfoItem);
					if (recordPlayerExtras == null)
					{
						recordPlayerExtras = new RecordPlayerExtras();
						_views.Add(recordPlayerExtras);
					}
					recordPlayerExtras.Refresh(playerBattleRoundInfoItem, data);
				}
			}
			catch (Exception ex)
			{
				_log.Error("战绩详情补充信息刷新失败", ex);
			}
		}

		private RecordPlayerExtras Find(PlayerBattleRoundInfoItem item)
		{
			for (int i = 0; i < _views.Count; i++)
			{
				if (_views[i].Matches(item))
				{
					return _views[i];
				}
			}
			return null;
		}

		private void OnRecordHide(HookContext h)
		{
			ClearViews();
		}

		private void ClearViews()
		{
			for (int i = 0; i < _views.Count; i++)
			{
				_views[i].Destroy();
			}
			_views.Clear();
		}

		public override void OnDisable()
		{
			ClearViews();
		}
	}
}
