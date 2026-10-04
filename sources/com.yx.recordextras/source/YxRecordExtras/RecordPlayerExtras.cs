using System.Collections.Generic;
using System.Globalization;
using Proto;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace YxRecordExtras
{
	internal sealed class RecordPlayerExtras
	{
		private sealed class FateIconTooltip
		{
			private readonly RectTransform _rect;

			private int _strategyId;

			public FateIconTooltip(RectTransform rect)
			{
				_rect = rect;
			}

			public void SetStrategy(int strategyId)
			{
				_strategyId = strategyId;
			}

			public void Show()
			{
				if (_strategyId > 0 && !(_rect == null))
				{
					FateStrategyConfig fateStrategyConfig = ConfigManager.GetFateStrategyConfig(_strategyId);
					if (fateStrategyConfig != null)
					{
						ILRPanelBase.FindILRPanel<TooltipsPanel>().FindILRSubPanel<TalentDescriptionPanel>().ShowBox(_rect, fateStrategyConfig.GetName(), fateStrategyConfig.ParseDescription(), fateStrategyConfig.GetKeywordsDesc(), TooltipBoxAlignment.Left);
					}
				}
			}
		}

		private static class FateCircleSprite
		{
			private static Texture2D _texture;

			private static Sprite _sprite;

			public static Sprite Get()
			{
				if (_sprite != null)
				{
					return _sprite;
				}
				_texture = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
				_texture.name = "YxRecordFateCircleMask";
				_texture.filterMode = FilterMode.Bilinear;
				for (int i = 0; i < 64; i++)
				{
					for (int j = 0; j < 64; j++)
					{
						float num = ((float)j + 0.5f - 32f) / 32f;
						float num2 = ((float)i + 0.5f - 32f) / 32f;
						float num3 = Mathf.Sqrt(num * num + num2 * num2);
						float a = Mathf.Clamp01((1f - num3) * 8f);
						_texture.SetPixel(j, i, new Color(1f, 1f, 1f, a));
					}
				}
				_texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
				_sprite = Sprite.Create(_texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);
				_sprite.name = "YxRecordFateCircleMask";
				return _sprite;
			}
		}

		private const int StrategyIconCount = 3;

		private const float TalentScale = 0.74f;

		private const float IconSize = 54f;

		private const float IconGap = 10f;

		private const float HoverHitPadding = 10f;

		private PlayerBattleRoundInfoItem _item;

		private Transform _talents;

		private Vector3 _originalTalentScale;

		private RectTransform _iconsRoot;

		private TextMeshProUGUI _expLabel;

		private readonly GameObject[] _fateSlots = new GameObject[3];

		private readonly RectTransform[] _fateHitRects = new RectTransform[3];

		private readonly Image[] _fateIcons = new Image[3];

		private readonly FateIconTooltip[] _tooltips = new FateIconTooltip[3];

		private int _hoveredIndex = -1;

		private int[] _lastFates;

		private string _originalExpText;

		private float _originalExpFontSize;

		public bool Matches(PlayerBattleRoundInfoItem item)
		{
			return _item == item;
		}

		public void Refresh(PlayerBattleRoundInfoItem item, PlayerData data)
		{
			if (item == null || data == null || data.publicData == null || data.publicData.lastRoundData == null)
			{
				return;
			}
			TextMeshProUGUI textMeshProUGUI = item.FindComponent<TextMeshProUGUI>("ExpLabel", debugLog: false);
			GameObject gameObject = item.FindGameObject("Talents");
			Transform transform = ((gameObject == null) ? null : gameObject.transform);
			if (textMeshProUGUI == null || transform == null)
			{
				return;
			}
			if (_iconsRoot == null || _item != item || _talents != transform)
			{
				Destroy();
				try
				{
					if (!Build(item, textMeshProUGUI, transform))
					{
						return;
					}
				}
				catch
				{
					Destroy();
					throw;
				}
			}
			_item = item;
			_expLabel = textMeshProUGUI;
			ApplySpeedToCultivation(textMeshProUGUI, data);
			RenderFates(data.publicData.lastRoundData.fateStrategies);
		}

		private bool Build(PlayerBattleRoundInfoItem item, TextMeshProUGUI expLabel, Transform talents)
		{
			if (talents as RectTransform == null || talents.parent == null || !(talents.parent is RectTransform))
			{
				return false;
			}
			_item = item;
			_talents = talents;
			_expLabel = expLabel;
			_originalExpText = expLabel.text;
			_originalExpFontSize = expLabel.fontSize;
			_originalTalentScale = talents.localScale;
			GameObject gameObject = new GameObject("YxRecordFateStrategies");
			_iconsRoot = gameObject.AddComponent(typeof(RectTransform)) as RectTransform;
			_iconsRoot.SetParent(talents.parent, worldPositionStays: false);
			RectTransform iconsRoot = _iconsRoot;
			Vector2 anchorMin = (_iconsRoot.anchorMax = new Vector2(0.5f, 0.5f));
			iconsRoot.anchorMin = anchorMin;
			_iconsRoot.pivot = new Vector2(0f, 0.5f);
			_iconsRoot.sizeDelta = new Vector2(192f, 64f);
			(gameObject.AddComponent(typeof(LayoutElement)) as LayoutElement).ignoreLayout = true;
			for (int i = 0; i < 3; i++)
			{
				GameObject gameObject2 = new GameObject("FateStrategyIcon" + i.ToString(CultureInfo.InvariantCulture));
				_fateSlots[i] = gameObject2;
				gameObject2.transform.SetParent(_iconsRoot, worldPositionStays: false);
				RectTransform rectTransform = gameObject2.AddComponent(typeof(RectTransform)) as RectTransform;
				anchorMin = (rectTransform.anchorMax = new Vector2(0f, 0.5f));
				rectTransform.anchorMin = anchorMin;
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				float num = 64f;
				rectTransform.anchoredPosition = new Vector2((float)i * 64f + num * 0.5f, 0f);
				rectTransform.sizeDelta = new Vector2(num, num);
				_fateHitRects[i] = rectTransform;
				GameObject gameObject3 = new GameObject("Circle");
				gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
				RectTransform obj = gameObject3.AddComponent(typeof(RectTransform)) as RectTransform;
				anchorMin = (obj.anchorMax = new Vector2(0.5f, 0.5f));
				obj.anchorMin = anchorMin;
				obj.pivot = new Vector2(0.5f, 0.5f);
				obj.sizeDelta = new Vector2(54f, 54f);
				Image obj2 = gameObject3.AddComponent(typeof(Image)) as Image;
				obj2.sprite = FateCircleSprite.Get();
				obj2.color = new Color(0.08f, 0.12f, 0.17f, 1f);
				obj2.raycastTarget = false;
				(gameObject3.AddComponent(typeof(Mask)) as Mask).showMaskGraphic = true;
				GameObject gameObject4 = new GameObject("Icon");
				gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
				RectTransform obj3 = gameObject4.AddComponent(typeof(RectTransform)) as RectTransform;
				obj3.anchorMin = Vector2.zero;
				obj3.anchorMax = Vector2.one;
				obj3.offsetMin = new Vector2(3f, 3f);
				obj3.offsetMax = new Vector2(-3f, -3f);
				Image image = gameObject4.AddComponent(typeof(Image)) as Image;
				image.preserveAspect = true;
				image.raycastTarget = false;
				_fateIcons[i] = image;
				_tooltips[i] = new FateIconTooltip(rectTransform);
				GameObject gameObject5 = new GameObject("FateStrategyOutline");
				gameObject5.transform.SetParent(gameObject3.transform, worldPositionStays: false);
				RectTransform obj4 = gameObject5.AddComponent(typeof(RectTransform)) as RectTransform;
				obj4.anchorMin = Vector2.zero;
				obj4.anchorMax = Vector2.one;
				obj4.offsetMin = Vector2.zero;
				obj4.offsetMax = Vector2.zero;
				Image obj5 = gameObject5.AddComponent(typeof(Image)) as Image;
				obj5.raycastTarget = false;
				obj5.LoadSprite("Icon_FateStrategyOutline_2");
				gameObject2.SetActive(value: false);
			}
			Observable.EveryUpdate().Subscribe(OnHoverUpdate).AddTo(gameObject);
			PositionIcons();
			return true;
		}

		private void OnHoverUpdate(long frame)
		{
			if (_iconsRoot == null || !_iconsRoot.gameObject.activeInHierarchy)
			{
				return;
			}
			int num = -1;
			Vector2 screenPoint = Input.mousePosition;
			for (int i = 0; i < 3; i++)
			{
				RectTransform rectTransform = _fateHitRects[i];
				if (rectTransform != null && rectTransform.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, UIManager.worldCamera))
				{
					num = i;
					break;
				}
			}
			if (num != _hoveredIndex)
			{
				_hoveredIndex = num;
				if (num >= 0)
				{
					_tooltips[num].Show();
				}
			}
		}

		private void PositionIcons()
		{
			if (_talents == null || _iconsRoot == null)
			{
				return;
			}
			bool flag = _lastFates != null && _lastFates.Length != 0;
			_talents.localScale = (flag ? new Vector3(_originalTalentScale.x * 0.74f, _originalTalentScale.y * 0.74f, _originalTalentScale.z) : _originalTalentScale);
			if (!flag)
			{
				_hoveredIndex = -1;
				_iconsRoot.gameObject.SetActive(value: false);
				return;
			}
			RectTransform rectTransform = _talents as RectTransform;
			if (rectTransform == null)
			{
				return;
			}
			if (!TryGetLastVisibleTalentCenter(rectTransform, out var center))
			{
				Rect rect = rectTransform.rect;
				center = rectTransform.TransformPoint(new Vector3(rect.xMin, rect.center.y, 0f));
			}
			RectTransform rectTransform2 = _talents.parent as RectTransform;
			if (!(rectTransform2 == null))
			{
				Vector3 vector = center + rectTransform2.TransformVector(new Vector3(8f, 0f, 0f));
				if ((_iconsRoot.position - vector).sqrMagnitude > 0.01f)
				{
					_hoveredIndex = -1;
				}
				_iconsRoot.position = vector;
				_iconsRoot.SetAsLastSibling();
				_iconsRoot.gameObject.SetActive(value: true);
			}
		}

		private bool TryGetLastVisibleTalentCenter(RectTransform talentsRect, out Vector3 center)
		{
			center = Vector3.zero;
			bool flag = false;
			float num = float.MinValue;
			Vector3[] array = new Vector3[4];
			for (int i = 0; i < talentsRect.childCount; i++)
			{
				Transform child = talentsRect.GetChild(i);
				if (child == null || child == _iconsRoot || !child.gameObject.activeSelf)
				{
					continue;
				}
				RectTransform rectTransform = child as RectTransform;
				if (!(rectTransform == null))
				{
					rectTransform.GetWorldCorners(array);
					float num2 = (array[2].x + array[3].x) * 0.5f;
					if (!flag || num2 > num)
					{
						flag = true;
						num = num2;
						center = (array[2] + array[3]) * 0.5f;
					}
				}
			}
			return flag;
		}

		private void ApplySpeedToCultivation(TextMeshProUGUI label, PlayerData data)
		{
			label.fontSize = Mathf.Min(label.fontSize, 16f);
			label.text = RecordSpeed.Format(label.text, data);
		}

		private void RenderFates(List<int> values)
		{
			int[] array = new int[3];
			int num = 0;
			if (values != null)
			{
				for (int i = 0; i < values.Count; i++)
				{
					if (num >= 3)
					{
						break;
					}
					if (values[i] > 0)
					{
						array[num++] = values[i];
					}
				}
			}
			if (num != array.Length)
			{
				int[] array2 = new int[num];
				for (int j = 0; j < num; j++)
				{
					array2[j] = array[j];
				}
				array = array2;
			}
			if (Same(_lastFates, array))
			{
				PositionIcons();
				return;
			}
			for (int k = 0; k < array.Length; k++)
			{
				int num2 = array[k];
				FateStrategyConfig fateStrategyConfig = ConfigManager.GetFateStrategyConfig(num2);
				Image image = _fateIcons[k];
				if (fateStrategyConfig == null)
				{
					_fateSlots[k].SetActive(value: false);
					_tooltips[k].SetStrategy(0);
					continue;
				}
				string iconPath = GetIconPath(fateStrategyConfig);
				image.sprite = null;
				image.LoadSprite(iconPath, AssetNameUtil.DEFAULT_TALENT_SPRITE_NAME);
				_tooltips[k].SetStrategy(num2);
				_fateSlots[k].SetActive(value: true);
			}
			for (int l = array.Length; l < 3; l++)
			{
				_fateSlots[l].SetActive(value: false);
			}
			_lastFates = array;
			_hoveredIndex = -1;
			PositionIcons();
		}

		private static string GetIconPath(FateStrategyConfig config)
		{
			if (!string.IsNullOrEmpty(config.overrideSpritePath))
			{
				return config.overrideSpritePath;
			}
			if (config.category == FateStrategyCategory.FateStrategyMing && config.otherParams.Count > 0)
			{
				return AssetNameUtil.GetTalentSpriteName(config.otherParams[0]);
			}
			if ((config.category == FateStrategyCategory.JiCard || config.category == FateStrategyCategory.GetCardOnRound || config.category == FateStrategyCategory.AddCardPool) && config.otherParams.Count > 0)
			{
				return AssetNameUtil.GetCardSpriteName(config.otherParams[0]);
			}
			return AssetNameUtil.GetFateStrategyIconPath(config.id);
		}

		private static bool Same(int[] left, int[] right)
		{
			if (left == null || right == null)
			{
				return left == right;
			}
			if (left.Length != right.Length)
			{
				return false;
			}
			for (int i = 0; i < left.Length; i++)
			{
				if (left[i] != right[i])
				{
					return false;
				}
			}
			return true;
		}

		public void Destroy()
		{
			if (_expLabel != null && _originalExpText != null)
			{
				_expLabel.text = _originalExpText;
			}
			if (_expLabel != null && _originalExpFontSize > 0f)
			{
				_expLabel.fontSize = _originalExpFontSize;
			}
			if (_talents != null)
			{
				_talents.localScale = _originalTalentScale;
			}
			if (_iconsRoot != null)
			{
				Object.Destroy(_iconsRoot.gameObject);
			}
			_item = null;
			_talents = null;
			_iconsRoot = null;
			_expLabel = null;
			_originalExpText = null;
			_originalExpFontSize = 0f;
			_lastFates = null;
			_hoveredIndex = -1;
			for (int i = 0; i < 3; i++)
			{
				_fateSlots[i] = null;
				_fateHitRects[i] = null;
				_fateIcons[i] = null;
				_tooltips[i] = null;
			}
		}
	}
}
