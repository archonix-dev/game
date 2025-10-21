using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Localization
{
	public class LocalizationManager : MonoBehaviour
	{
		public static LocalizationManager Instance { get; private set; }

		[SerializeField]
		private string resourcesCsvPath = "localization"; // Resources/localization.csv

		private readonly Dictionary<string, Dictionary<string, string>> languageToKeyToText = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
		private readonly List<string> availableLanguages = new List<string>();

		private string currentLanguage = string.Empty;

		public event Action OnLanguageChanged;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);

			LoadCsv();
			SetupInitialLanguage();
		}

		private void OnEnable()
		{
			SceneManager.sceneLoaded += HandleSceneLoaded;
		}

		private void OnDisable()
		{
			SceneManager.sceneLoaded -= HandleSceneLoaded;
		}

		private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			ApplyLanguageToScene();
		}

		public IReadOnlyList<string> GetAvailableLanguages()
		{
			return availableLanguages;
		}

		public string GetCurrentLanguage()
		{
			return currentLanguage;
		}

		public void SetLanguage(string languageCode)
		{
			if (string.IsNullOrEmpty(languageCode)) return;
			if (!languageToKeyToText.ContainsKey(languageCode)) return;
			if (string.Equals(currentLanguage, languageCode, StringComparison.OrdinalIgnoreCase)) return;

			currentLanguage = languageCode;
			PlayerPrefs.SetString("Localization.Language", currentLanguage);
			PlayerPrefs.Save();

			ApplyLanguageToScene();
			OnLanguageChanged?.Invoke();
		}

		public bool TryGet(string key, out string value)
		{
			value = string.Empty;
			if (string.IsNullOrEmpty(currentLanguage)) return false;
			if (!languageToKeyToText.TryGetValue(currentLanguage, out var keyToText)) return false;
			if (string.IsNullOrEmpty(key)) return false;
			return keyToText.TryGetValue(key, out value);
		}

		public bool TryGet(string key, string languageCode, out string value)
		{
			value = string.Empty;
			if (string.IsNullOrEmpty(languageCode))
			{
				return TryGet(key, out value);
			}
			if (!languageToKeyToText.TryGetValue(languageCode, out var keyToText)) return false;
			if (string.IsNullOrEmpty(key)) return false;
			return keyToText.TryGetValue(key, out value);
		}

		public string Localize(string key, string fallback = null)
		{
			if (TryGet(key, out var value)) return value;
			return fallback ?? key ?? string.Empty;
		}

		public string Localize(string key, string languageCode, string fallback = null)
		{
			if (TryGet(key, languageCode, out var value)) return value;
			return fallback ?? key ?? string.Empty;
		}

		public string LocalizeFormat(string key, params object[] args)
		{
			var fmt = Localize(key);
			if (args == null || args.Length == 0) return fmt;
			try
			{
				return string.Format(CultureInfo.InvariantCulture, fmt, args);
			}
			catch
			{
				return fmt;
			}
		}

		public string LocalizeFormat(string key, string languageCode, params object[] args)
		{
			var fmt = Localize(key, languageCode);
			if (args == null || args.Length == 0) return fmt;
			try
			{
				return string.Format(CultureInfo.InvariantCulture, fmt, args);
			}
			catch
			{
				return fmt;
			}
		}

		private void SetupInitialLanguage()
		{
			var saved = PlayerPrefs.GetString("Localization.Language", string.Empty);
			if (!string.IsNullOrEmpty(saved) && languageToKeyToText.ContainsKey(saved))
			{
				currentLanguage = saved;
			}
			else
			{
				// Try system language
				var sys = Application.systemLanguage.ToString();
				// Try to map to codes like en_US, ru_RU if present
				var fallback = availableLanguages.FirstOrDefault(l => l.StartsWith(sys, StringComparison.OrdinalIgnoreCase))
										?? availableLanguages.FirstOrDefault();
				currentLanguage = string.IsNullOrEmpty(fallback) ? string.Empty : fallback;
			}

			if (!string.IsNullOrEmpty(currentLanguage))
			{
				ApplyLanguageToScene();
			}
		}

		private void LoadCsv()
		{
			languageToKeyToText.Clear();
			availableLanguages.Clear();

			var textAsset = Resources.Load<TextAsset>(resourcesCsvPath);
			if (textAsset == null)
			{
				return;
			}

			var lines = SplitLines(textAsset.text);
			if (lines.Count == 0) return;

			var delimiter = DetectDelimiter(lines[0]);
			var header = ParseCsvLine(lines[0], delimiter);
			if (header.Count <= 1)
			{
				return;
			}

			// header[0] is expected empty
			for (int i = 1; i < header.Count; i++)
			{
				var lang = header[i].Trim();
				if (string.IsNullOrEmpty(lang)) continue;
				if (!languageToKeyToText.ContainsKey(lang))
				{
					languageToKeyToText[lang] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
					availableLanguages.Add(lang);
				}
			}

			for (int r = 1; r < lines.Count; r++)
			{
				if (string.IsNullOrWhiteSpace(lines[r])) continue;
				var row = ParseCsvLine(lines[r], delimiter);
				if (row.Count == 0) continue;
				var key = row[0].Trim();
				if (string.IsNullOrEmpty(key)) continue;

				for (int c = 1; c < header.Count && c < row.Count; c++)
				{
					var lang = header[c].Trim();
					if (string.IsNullOrEmpty(lang)) continue;
					var value = row[c] ?? string.Empty;
					languageToKeyToText[lang][key] = value;
				}
			}
		}

		private static List<string> SplitLines(string text)
		{
			var result = new List<string>();
			using (var reader = new StringReader(text))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					result.Add(line);
				}
			}
			return result;
		}

		private static char DetectDelimiter(string headerLine)
		{
			// Support comma or semicolon; pick the one with more splits
			int commas = headerLine.Count(ch => ch == ',');
			int semicolons = headerLine.Count(ch => ch == ';');
			return semicolons > commas ? ';' : ',';
		}

		private static List<string> ParseCsvLine(string line, char delimiter)
		{
			var cells = new List<string>();
			bool inQuotes = false;
			var sb = new StringBuilder();
			for (int i = 0; i < line.Length; i++)
			{
				char ch = line[i];
				if (ch == '"')
				{
					if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
					{
						// Escaped quote
						sb.Append('"');
						i++;
					}
					else
					{
						inQuotes = !inQuotes;
					}
				}
				else if (ch == delimiter && !inQuotes)
				{
					cells.Add(sb.ToString());
					sb.Length = 0;
				}
				else
				{
					sb.Append(ch);
				}
			}
			cells.Add(sb.ToString());
			return cells;
		}

		public void ApplyLanguageToScene()
		{
			if (string.IsNullOrEmpty(currentLanguage)) return;
			if (!languageToKeyToText.TryGetValue(currentLanguage, out var keyToText)) return;

			// Unity UI Text
			var uiTexts = FindObjectsOfType<Text>(true);
			for (int i = 0; i < uiTexts.Length; i++)
			{
				var comp = uiTexts[i];
				if (comp == null) continue;
				var key = comp.gameObject.name;
				if (string.IsNullOrEmpty(key)) continue;
				if (keyToText.TryGetValue(key, out var localized))
				{
					comp.text = localized;
				}
			}

			// TextMeshPro UGUI
			var tmpUis = FindObjectsOfType<TextMeshProUGUI>(true);
			for (int i = 0; i < tmpUis.Length; i++)
			{
				var comp = tmpUis[i];
				if (comp == null) continue;
				var key = comp.gameObject.name;
				if (string.IsNullOrEmpty(key)) continue;
				if (keyToText.TryGetValue(key, out var localized))
				{
					comp.text = localized;
				}
			}

			// TextMeshPro (3D)
			var tmp3Ds = FindObjectsOfType<TextMeshPro>(true);
			for (int i = 0; i < tmp3Ds.Length; i++)
			{
				var comp = tmp3Ds[i];
				if (comp == null) continue;
				var key = comp.gameObject.name;
				if (string.IsNullOrEmpty(key)) continue;
				if (keyToText.TryGetValue(key, out var localized))
				{
					comp.text = localized;
				}
			}
		}
	}
}


