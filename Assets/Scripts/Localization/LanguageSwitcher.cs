using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Localization
{
    public sealed class LanguageSwitcher : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Button with globe icon that toggles the language panel")]
        [SerializeField] Button globeButton;

        [Tooltip("Image showing the current language flag")]
        [SerializeField] Image currentFlagImage;

        [Tooltip("Panel containing all language flag buttons (vertical layout)")]
        [SerializeField] GameObject flagPanel;

        [Tooltip("Container for language flag buttons (has Vertical Layout Group)")]
        [SerializeField] Transform flagContainer;

        [Tooltip("Prefab for individual flag button (Image + Button)")]
        [SerializeField] GameObject flagButtonPrefab;

        [Header("Flag Sprites")]
        [Tooltip("Flag sprite for English (UK/US flag)")]
        [SerializeField] Sprite englishFlag;

        [Tooltip("Flag sprite for Hungarian")]
        [SerializeField] Sprite hungarianFlag;

        [Header("Configuration")]
        [Tooltip("Languages to show (must match LocalizationManager supported languages)")]
        [SerializeField] List<string> availableLanguages = new List<string> { "en", "hu" };

        Dictionary<string, Sprite> _languageFlags = new Dictionary<string, Sprite>();
        bool _isExpanded;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            _languageFlags["en"] = englishFlag;
            _languageFlags["hu"] = hungarianFlag;

            if (globeButton)
            {
                globeButton.onClick.AddListener(OnGlobeClicked);
            }

            if (flagPanel)
            {
                flagPanel.SetActive(false);
            }

            CreateFlagButtons();
            UpdateCurrentFlag();
        }

        void CreateFlagButtons()
        {
            if (!flagContainer || !flagButtonPrefab)
            {
                Debug.LogWarning("[LanguageSwitcher] Flag container or prefab not assigned");
                return;
            }

            foreach (Transform child in flagContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (string langCode in availableLanguages)
            {
                if (!_languageFlags.ContainsKey(langCode))
                {
                    Debug.LogWarning($"[LanguageSwitcher] No flag sprite for language: {langCode}");
                    continue;
                }

                GameObject flagButtonObj = Instantiate(flagButtonPrefab, flagContainer);

                Image flagImage = flagButtonObj.GetComponent<Image>();
                if (flagImage)
                {
                    flagImage.sprite = _languageFlags[langCode];
                }

                Button flagButton = flagButtonObj.GetComponent<Button>();
                if (flagButton)
                {
                    string language = langCode;
                    flagButton.onClick.AddListener(() => OnFlagClicked(language));
                }

                flagButtonObj.name = $"FlagButton_{langCode}";
            }
        }

        void OnGlobeClicked()
        {
            _isExpanded = !_isExpanded;

            if (flagPanel)
            {
                flagPanel.SetActive(_isExpanded);
            }
        }

        void OnFlagClicked(string languageCode)
        {
            if (!LocalizationManager.Instance)
            {
                Debug.LogWarning("[LanguageSwitcher] LocalizationManager not available");
                return;
            }

            LocalizationManager.Instance.SetLanguage(languageCode);
            UpdateCurrentFlag();

            _isExpanded = false;
            if (flagPanel)
            {
                flagPanel.SetActive(false);
            }
        }

        void UpdateCurrentFlag()
        {
            if (!currentFlagImage || !LocalizationManager.Instance)
            {
                return;
            }

            string currentLang = LocalizationManager.Instance.CurrentLanguage;

            if (_languageFlags.TryGetValue(currentLang, out Sprite flagSprite))
            {
                currentFlagImage.sprite = flagSprite;
                currentFlagImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[LanguageSwitcher] No flag sprite for language: {currentLang}");
            }
        }

        void OnEnable()
        {
            if (LocalizationManager.Instance)
            {
                LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
            }
        }

        void OnDisable()
        {
            if (LocalizationManager.Instance)
            {
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
            }
        }

        void OnLanguageChanged(string newLanguage)
        {
            UpdateCurrentFlag();
        }

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Test Expand")]
        void TestExpand()
        {
            if (flagPanel)
            {
                flagPanel.SetActive(!flagPanel.activeSelf);
            }
        }

        [ContextMenu("Test Switch to Hungarian")]
        void TestSwitchHu()
        {
            OnFlagClicked("hu");
        }

        [ContextMenu("Test Switch to English")]
        void TestSwitchEn()
        {
            OnFlagClicked("en");
        }
#endif

        #endregion
    }
}
