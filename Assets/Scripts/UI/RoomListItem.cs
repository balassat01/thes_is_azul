using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class RoomListItem : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] TextMeshProUGUI roomNameText;
        [SerializeField] TextMeshProUGUI playerCountText;
        [SerializeField] TextMeshProUGUI settingsText;
        [SerializeField] Image backgroundImage;
        [SerializeField] Button selectButton;

        [Header("Visual Feedback")]
        [SerializeField] Color normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] Color selectedColor = new Color(0.4f, 0.6f, 1f, 1f);
        [SerializeField] Color fullRoomColor = new Color(0.3f, 0.1f, 0.1f, 1f);

        RoomInfo _roomInfo;
        LobbyManager _lobbyManager;
        bool _isSelected;

        public void Initialize(RoomInfo roomInfo, LobbyManager lobbyManager)
        {
            _roomInfo = roomInfo;
            _lobbyManager = lobbyManager;

            UpdateDisplay();

            if (!selectButton)
            {
                selectButton = GetComponent<Button>();
            }

            if (!selectButton)
            {
                return;
            }

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClicked);
        }

        void UpdateDisplay()
        {
            if (_roomInfo == null) return;

            if (roomNameText)
            {
                roomNameText.text = _roomInfo.Name;
            }

            if (playerCountText)
            {
                playerCountText.text = $"{_roomInfo.PlayerCount}/{_roomInfo.MaxPlayers}";

                if (_roomInfo.PlayerCount >= _roomInfo.MaxPlayers)
                {
                    playerCountText.color = Color.red;
                }
                else if (_roomInfo.PlayerCount > 0)
                {
                    playerCountText.color = Color.green;
                }
                else
                {
                    playerCountText.color = Color.white;
                }
            }

            if (settingsText)
            {
                string settings = BuildSettingsString();
                settingsText.text = string.IsNullOrEmpty(settings) ? "[Standard]" : settings;
            }

            UpdateBackgroundColor();
        }

        string BuildSettingsString()
        {
            var settings = "";

            if (_roomInfo.CustomProperties == null)
            {
                return settings.TrimEnd();
            }

            if (_roomInfo.CustomProperties.TryGetValue("UseGrayBox", out object grayBox) && (bool)grayBox)
            {
                settings += "[Gray] ";
            }
            else
            {
                settings += "[Normal] ";
            }

            if (_roomInfo.CustomProperties.TryGetValue("TurnTimer", out object timer))
            {
                int timerSeconds = timer is int timerInt ? timerInt : 0;
                if (timerSeconds > 0)
                {
                    settings += $"[Timer: {timerSeconds}s] ";
                }
                else
                {
                    settings += "[Timer: OFF]";
                }
            }

            if (_roomInfo.CustomProperties.TryGetValue("GameStarted", out object started) && (bool)started)
            {
                settings += "[IN PROGRESS] ";
            }

            return settings.TrimEnd();
        }

        void OnClicked()
        {
            if (_lobbyManager && CanJoin())
            {
                _lobbyManager.OnRoomListItemClicked(_roomInfo.Name);
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateBackgroundColor();
        }

        void UpdateBackgroundColor()
        {
            if (!backgroundImage) return;

            if (_isSelected)
            {
                backgroundImage.color = selectedColor;
            }
            else if (!CanJoin())
            {
                backgroundImage.color = fullRoomColor;
            }
            else
            {
                backgroundImage.color = normalColor;
            }
        }

        bool CanJoin()
        {
            if (_roomInfo == null) return false;

            bool hasSpace = _roomInfo.PlayerCount < _roomInfo.MaxPlayers;
            bool isOpen = _roomInfo.IsOpen;

            var gameStarted = false;
            if (_roomInfo.CustomProperties != null &&
                _roomInfo.CustomProperties.TryGetValue("GameStarted", out object started))
            {
                gameStarted = (bool)started;
            }

            return hasSpace && isOpen && !gameStarted;
        }

        public string GetRoomName()
        {
            return _roomInfo?.Name;
        }
    }
}
