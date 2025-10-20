using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Localization
{
	public class LocalizedTextBinder : MonoBehaviour
	{
		[SerializeField]
		private string key;

		[SerializeField]
		private string languageOverride = ""; // optional explicit lang like en_US

		private Text uiText;
		private TextMeshProUGUI tmpUiText;
		private TextMeshPro tmp3DText;

		private void Awake()
		{
			uiText = GetComponent<Text>();
			tmpUiText = GetComponent<TextMeshProUGUI>();
			tmp3DText = GetComponent<TextMeshPro>();
		}

		private void OnEnable()
		{
			Apply();
			if (LocalizationManager.Instance != null)
			{
				LocalizationManager.Instance.OnLanguageChanged += HandleLanguageChanged;
			}
		}

		private void OnDisable()
		{
			if (LocalizationManager.Instance != null)
			{
				LocalizationManager.Instance.OnLanguageChanged -= HandleLanguageChanged;
			}
		}

		private void HandleLanguageChanged()
		{
			Apply();
		}

		public void SetKey(string newKey)
		{
			key = newKey;
			Apply();
		}

		public void SetLanguageOverride(string lang)
		{
			languageOverride = lang;
			Apply();
		}

		private void Apply()
		{
			var loc = LocalizationManager.Instance;
			if (loc == null || string.IsNullOrEmpty(key)) return;

			string textValue = string.IsNullOrEmpty(languageOverride)
				? loc.Localize(key)
				: loc.Localize(key, languageOverride);

			if (uiText != null)
			{
				uiText.text = textValue;
			}
			else if (tmpUiText != null)
			{
				tmpUiText.text = textValue;
			}
			else if (tmp3DText != null)
			{
				tmp3DText.text = textValue;
			}
		}
	}
}


