using System;
using TMPro;
using UnityEngine;

namespace Localization
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Header("Localization")]
        [Tooltip("Translation key in format 'category.key' (e.g., 'lobby.title')")]
        [SerializeField] string localizationKey;

        [Header("Formatting (Optional)")]
        [Tooltip("Enable if this text uses string formatting (e.g., 'Score: {0}')")]
        [SerializeField] bool usesFormatting;

        [Tooltip("Format arguments (if usesFormatting is true). Set values via script.")]
        [SerializeField] string[] formatArgs = Array.Empty<string>();

        TextMeshProUGUI _textComponent;
        bool _isInitialized;

        void Awake()
        {
            _textComponent = GetComponent<TextMeshProUGUI>();
            Initialize();
        }

        void OnEnable()
        {
            if (LocalizationManager.Instance == null)
            {
                return;
            }
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
            UpdateText();
        }

        void OnDisable()
        {
            if (LocalizationManager.Instance)
            {
                LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
            }
        }

        void Initialize()
        {
            if (_isInitialized) return;

            if (_textComponent == null)
            {
                _textComponent = GetComponent<TextMeshProUGUI>();
            }

            _isInitialized = true;
        }

        void OnLanguageChanged(string newLanguage)
        {
            UpdateText();
        }

        void UpdateText()
        {
            if (_textComponent == null || string.IsNullOrEmpty(localizationKey))
            {
                return;
            }

            if (LocalizationManager.Instance == null)
            {
                Debug.LogWarning($"[LocalizedText] LocalizationManager not found. GameObject: {gameObject.name}");
                return;
            }

            string localizedText;
            if (usesFormatting && formatArgs is { Length: > 0 })
            {
                localizedText = LocalizationManager.Instance.GetText(localizationKey, formatArgs);
            }
            else
            {
                localizedText = LocalizationManager.Instance.GetText(localizationKey);
            }

            _textComponent.text = localizedText;
        }

        public void SetKey(string key)
        {
            localizationKey = key;
            UpdateText();
        }

        public void SetFormatArgs(params string[] args)
        {
            formatArgs = args;
            usesFormatting = args is { Length: > 0 };
            UpdateText();
        }

        public void SetFormatArgs(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                formatArgs = Array.Empty<string>();
                usesFormatting = false;
            }
            else
            {
                formatArgs = new string[args.Length];
                for (var i = 0; i < args.Length; i++)
                {
                    formatArgs[i] = args[i]?.ToString() ?? "";
                }
                usesFormatting = true;
            }
            UpdateText();
        }

        #region Editor Helpers

#if UNITY_EDITOR
        [ContextMenu("Update Text Now")]
        void UpdateTextEditor()
        {
            Initialize();
            UpdateText();
        }

        [ContextMenu("Preview Key")]
        void PreviewKey()
        {
            if (_textComponent == null)
                _textComponent = GetComponent<TextMeshProUGUI>();

            if (string.IsNullOrEmpty(localizationKey))
            {
                Debug.LogWarning("Localization key is empty!");
                return;
            }

            Debug.LogWarning($"Key: '{localizationKey}' on GameObject: {gameObject.name}");
        }
#endif

        #endregion
    }
}
