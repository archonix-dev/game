using System.Collections.Generic;
using Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
	public class LocalizationDropdown : MonoBehaviour
	{
		[SerializeField]
		public Dropdown dropdown;

		private void OnEnable()
		{
			RefreshOptions();
			if (LocalizationManager.Instance != null)
			{
				LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
			}
			dropdown.onValueChanged.AddListener(HandleSelectionChanged);
		}

		private void OnDisable()
		{
			dropdown.onValueChanged.RemoveListener(HandleSelectionChanged);
			if (LocalizationManager.Instance != null)
			{
				LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
			}
		}

		private void HandleLanguageChanged()
		{
			SyncSelectionWithCurrentLanguage();
		}

		private void HandleSelectionChanged(int index)
		{
			if (LocalizationManager.Instance == null) return;
			var langs = LocalizationManager.Instance.GetAvailableLanguages();
			if (index < 0 || index >= langs.Count) return;
			LocalizationManager.Instance.SetLanguage(langs[index]);
		}

		private void RefreshOptions()
		{
			if (LocalizationManager.Instance == null || dropdown == null)
			{
				return;
			}

			var langs = LocalizationManager.Instance.GetAvailableLanguages();
			dropdown.ClearOptions();
			var options = new List<Dropdown.OptionData>();
			for (int i = 0; i < langs.Count; i++)
			{
				options.Add(new Dropdown.OptionData(langs[i]));
			}
			dropdown.AddOptions(options);
			SyncSelectionWithCurrentLanguage();
		}

		private void SyncSelectionWithCurrentLanguage()
		{
			if (LocalizationManager.Instance == null || dropdown == null) return;
			var current = LocalizationManager.Instance.GetCurrentLanguage();
			var langs = LocalizationManager.Instance.GetAvailableLanguages();
			int idx = -1;
			for (int i = 0; i < langs.Count; i++)
			{
				if (string.Equals(langs[i], current, System.StringComparison.OrdinalIgnoreCase))
				{
					idx = i;
					break;
				}
			}
			if (idx >= 0)
			{
				dropdown.SetValueWithoutNotify(idx);
			}
		}
	}
}


