using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localization
{
    public sealed class LocalizationManager : MonoBehaviour
    {
        static LocalizationManager _instance;
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance)
                {
                    return _instance;
                }
                var go = new GameObject("LocalizationManager");
                _instance = go.AddComponent<LocalizationManager>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Header("Configuration")]
        [SerializeField] string defaultLanguage = "en";
        [SerializeField] List<string> supportedLanguages = new List<string> { "en", "hu" };

        string _currentLanguage;
        public string CurrentLanguage => _currentLanguage;

        readonly Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>();

        public event Action<string> OnLanguageChanged;

        void Awake()
        {
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            string savedLanguage = PlayerPrefs.GetString("Language", defaultLanguage);
            LoadLanguage(savedLanguage);
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void LoadLanguage(string languageCode)
        {
            if (!supportedLanguages.Contains(languageCode))
            {
                Debug.LogWarning($"[LocalizationManager] Language '{languageCode}' not supported. Using '{defaultLanguage}'.");
                languageCode = defaultLanguage;
            }

            var path = $"Localization/{languageCode}";
            var jsonFile = Resources.Load<TextAsset>(path);

            if (!jsonFile)
            {
                Debug.LogError($"[LocalizationManager] Translation file not found: {path}");
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<LocalizationData>(jsonFile.text);

                _translations.Clear();

                StoreCategory("common", data.common);
                StoreCategory("connection", data.connection);
                StoreCategory("lobby", data.lobby);
                StoreCategory("roomBrowser", data.roomBrowser);
                StoreCategory("room", data.room);
                StoreCategory("game", data.game);
                StoreCategory("playerBoard", data.playerBoard);
                StoreCategory("endGame", data.endGame);
                StoreCategory("rules", data.rules);
                StoreCategory("settings", data.settings);
                StoreCategory("errors", data.errors);
                StoreCategory("help", data.help);

                _currentLanguage = languageCode;
                PlayerPrefs.SetString("Language", languageCode);

                OnLanguageChanged?.Invoke(languageCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalizationManager] Failed to parse translation file: {e.Message}");
            }
        }

        void StoreCategory(string category, CategoryData categoryData)
        {
            if (categoryData?.entries == null) return;

            var dict = new Dictionary<string, string>();
            foreach (Entry entry in categoryData.entries)
            {
                dict[entry.key] = entry.value;
            }
            _translations[category] = dict;
        }

        public string GetText(string key, params string[] args)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[LocalizationManager] GetText called with empty key");
                return "[MISSING KEY]";
            }

            string[] parts = key.Split('.');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"[LocalizationManager] Invalid key format: '{key}'. Expected 'category.key'");
                return $"[{key}]";
            }

            string category = parts[0];
            string subKey = parts[1];

            if (_translations.TryGetValue(category, out Dictionary<string, string> categoryDict))
            {
                if (categoryDict.TryGetValue(subKey, out string translation))
                {
                    if (args != null && args.Length > 0)
                    {
                        try
                        {
                            return string.Format(translation, args);
                        }
                        catch (FormatException)
                        {
                            Debug.LogWarning($"[LocalizationManager] Format error for key '{key}' with {args.Length} args");
                            return translation;
                        }
                    }
                    return translation;
                }
            }

            Debug.LogWarning($"[LocalizationManager] Translation not found: '{key}'");
            return $"[{key}]";
        }

        public void SetLanguage(string languageCode)
        {
            if (languageCode == _currentLanguage) return;
            LoadLanguage(languageCode);
        }

        public List<string> GetSupportedLanguages()
        {
            return new List<string>(supportedLanguages);
        }

        public string GetLanguageName(string languageCode)
        {
            return languageCode switch
            {
                "en" => "English",
                "hu" => "Magyar",
                _ => languageCode.ToUpper()
            };
        }

        #region JSON Data Structures

        [Serializable]
        public class LocalizationData
        {
            public CategoryData common;
            public CategoryData connection;
            public CategoryData lobby;
            public CategoryData roomBrowser;
            public CategoryData room;
            public CategoryData game;
            public CategoryData playerBoard;
            public CategoryData endGame;
            public CategoryData rules;
            public CategoryData settings;
            public CategoryData errors;
            public CategoryData help;
        }

        [Serializable]
        public class CategoryData
        {
            public Entry[] entries;
        }

        [Serializable]
        public class Entry
        {
            public string key;
            public string value;
        }

        #endregion
    }
}
