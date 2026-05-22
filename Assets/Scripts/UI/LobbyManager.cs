using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Localization;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace UI
{
    public sealed class LobbyManager : MonoBehaviourPunCallbacks
    {
        [Header("Connection Panel")]
        [SerializeField] GameObject connectionPanel;
        [SerializeField] TMP_InputField playerNameInput;
        [SerializeField] Button connectButton;
        [SerializeField] TextMeshProUGUI connectionStatusText;
        [SerializeField] Button exitGameButton;

        [Header("Lobby Panel")]
        [SerializeField] GameObject lobbyPanel;
        [SerializeField] Button createRoomButton;
        [SerializeField] Button joinRandomButton;
        [SerializeField] Button browseRoomsButton;
        [SerializeField] TMP_InputField roomNameInput;
        [SerializeField] Toggle createPrivateRoomToggle;
        [SerializeField] TMP_InputField roomCodeInput;
        [SerializeField] Button joinByCodeButton;
        [SerializeField] Button lobbyExitButton;

        [Header("Room Browser Panel")]
        [SerializeField] GameObject roomBrowserPanel;
        [SerializeField] Transform roomListContent;
        [SerializeField] GameObject roomListItemPrefab;
        [SerializeField] Button joinSelectedButton;
        [SerializeField] Button refreshRoomListButton;
        [SerializeField] Button backToLobbyButton;
        [SerializeField] TextMeshProUGUI emptyRoomListText;

        [Header("Room Panel")]
        [SerializeField] GameObject roomPanel;
        [SerializeField] TextMeshProUGUI roomNameText;
        [SerializeField] TextMeshProUGUI roomCodeDisplayText;
        [SerializeField] Button copyRoomCodeButton;
        [SerializeField] Transform playerListContent;
        [SerializeField] GameObject playerListItemPrefab;
        [SerializeField] Button startGameButton;
        [SerializeField] Button leaveRoomButton;

        [Header("Game Configuration (Master Only)")]
        [SerializeField] TMP_Dropdown playerCountDropdown;
        [SerializeField] Toggle grayBoxToggle;
        [SerializeField] Toggle turnTimerToggle;
        [SerializeField] TMP_InputField turnTimerInput;

        [Header("Settings")]
        [SerializeField] string gameSceneName = "GameScene";

        [Header("Confirmation Popup")]
        [SerializeField] GameObject confirmationPopup;
        [SerializeField] TextMeshProUGUI confirmationText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        Dictionary<int, GameObject> _playerListItems = new Dictionary<int, GameObject>();
        Dictionary<string, RoomInfo> _cachedRoomList = new Dictionary<string, RoomInfo>();
        Dictionary<string, GameObject> _roomListItems = new Dictionary<string, GameObject>();
        string _selectedRoomName;
        System.Action _pendingConfirmAction;
        bool _loadingGameScene;

        void Start()
        {
            ShowPanel(connectionPanel);

            string savedName = PlayerPrefs.GetString("PlayerName", "");
            if (!string.IsNullOrEmpty(savedName))
            {
                playerNameInput.text = savedName;
            }

            connectButton.onClick.AddListener(OnConnectClicked);
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            joinRandomButton.onClick.AddListener(OnJoinRandomClicked);
            browseRoomsButton.onClick.AddListener(OnBrowseRoomsClicked);
            joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);
            joinSelectedButton.onClick.AddListener(OnJoinSelectedClicked);
            refreshRoomListButton.onClick.AddListener(OnRefreshRoomsClicked);
            backToLobbyButton.onClick.AddListener(OnBackToLobbyClicked);
            startGameButton.onClick.AddListener(OnStartGameClicked);
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
            exitGameButton.onClick.AddListener(OnExitButtonClicked);
            lobbyExitButton.onClick.AddListener(OnExitButtonClicked);
            if (copyRoomCodeButton)
            {
                copyRoomCodeButton.onClick.AddListener(OnCopyRoomCodeClicked);
            }

            if (turnTimerToggle)
            {
                turnTimerToggle.onValueChanged.AddListener(OnTurnTimerToggleChanged);
            }

            if (confirmYesButton)
            {
                confirmYesButton.onClick.AddListener(OnConfirmYes);
            }
            if (confirmNoButton)
            {
                confirmNoButton.onClick.AddListener(OnConfirmNo);
            }

            if (confirmationPopup)
            {
                confirmationPopup.SetActive(false);
            }

            connectionStatusText.text = LocalizationManager.Instance.GetText("connection.disconnected");
        }

        #region Connection Callbacks

        void OnConnectClicked()
        {
            string playerName = playerNameInput.text.Trim();
            if (string.IsNullOrEmpty(playerName))
            {
                connectionStatusText.text = LocalizationManager.Instance.GetText("connection.enterName");
                return;
            }

            PlayerPrefs.SetString("PlayerName", playerName);
            PhotonNetwork.NickName = playerName;

            connectionStatusText.text = LocalizationManager.Instance.GetText("connection.connecting");
            connectButton.interactable = false;

            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster()
        {
            connectionStatusText.text = LocalizationManager.Instance.GetText("connection.connected");
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            ShowPanel(lobbyPanel);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[LobbyManager] Disconnected from Photon: {cause}");
            connectionStatusText.text = LocalizationManager.Instance.GetText("connection.disconnectedReason", cause.ToString());
            connectButton.interactable = true;
            ShowPanel(connectionPanel);
            _loadingGameScene = false;
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (_loadingGameScene)
            {
                return;
            }

            if (propertiesThatChanged != null
                && propertiesThatChanged.TryGetValue("GameStarted", out object gameStarted)
                && gameStarted is true)
            {
                _loadingGameScene = true;
                StartCoroutine(LoadGameSceneAfterSync());
            }
        }

        void OnExitButtonClicked()
        {
            string message = LocalizationManager.Instance.GetText("common.exitConfirmation");
            ShowConfirmation(message, Application.Quit);
        }

        #endregion

        #region Room Creation & Joining

        void OnCreateRoomClicked()
        {
            bool isPrivate = createPrivateRoomToggle && createPrivateRoomToggle.isOn;

            string roomCode = GenerateRoomCode();

            var roomOptions = new RoomOptions
            {
                MaxPlayers = 4,
                IsVisible = !isPrivate,
                IsOpen = true,
                CustomRoomProperties = new Hashtable
                {
                    ["UseGrayBox"] = false,
                    ["MaxPlayers"] = 4,
                    ["TurnTimer"] = 0,
                    ["GameStarted"] = false,
                    ["IsPrivate"] = isPrivate
                },
                CustomRoomPropertiesForLobby = new[] { "MaxPlayers", "GameStarted", "IsPrivate" }
            };

            PhotonNetwork.CreateRoom(roomCode, roomOptions);
        }

        static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var code = new char[6];
            for (var i = 0; i < 6; i++)
            {
                code[i] = chars[Random.Range(0, chars.Length)];
            }
            return new string(code);
        }

        static void OnJoinRandomClicked()
        {
            var expectedProperties = new Hashtable
            {
                ["IsPrivate"] = false
            };
            PhotonNetwork.JoinRandomRoom(expectedProperties, 0);
        }

        void OnJoinByCodeClicked()
        {
            if (roomCodeInput == null)
            {
                Debug.LogWarning("[LobbyManager] Room code input field not assigned");
                return;
            }

            string code = roomCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[LobbyManager] Please enter a room code");
                if (connectionStatusText)
                {
                    connectionStatusText.text = LocalizationManager.Instance.GetText("lobby.enterRoomCode");
                }
                return;
            }

            PhotonNetwork.JoinRoom(code);
        }

        public override void OnJoinedRoom()
        {
            ShowPanel(roomPanel);
            roomNameText.text = LocalizationManager.Instance.GetText("room.roomName", PhotonNetwork.CurrentRoom.Name);

            if (roomCodeDisplayText)
            {
                roomCodeDisplayText.text = LocalizationManager.Instance.GetText("room.roomCode", PhotonNetwork.CurrentRoom.Name);
                roomCodeDisplayText.gameObject.SetActive(true);
            }

            if (copyRoomCodeButton)
            {
                copyRoomCodeButton.gameObject.SetActive(true);
            }

            bool isMaster = PhotonNetwork.IsMasterClient;
            UpdateRoomConfigUI(isMaster);

            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("UseGrayBox", out object grayBox))
            {
                grayBoxToggle.isOn = (bool)grayBox;
            }

            if (isMaster)
            {
                foreach (Player player in PhotonNetwork.PlayerList)
                {
                    AssignColorToNewPlayer(player);
                }
            }

            UpdatePlayerList();
        }

        void UpdateRoomConfigUI(bool isMaster)
        {
            grayBoxToggle.interactable = isMaster;
            playerCountDropdown.interactable = isMaster;
            turnTimerToggle.interactable = isMaster;
            turnTimerInput.interactable = isMaster && (turnTimerToggle && turnTimerToggle.isOn);
            startGameButton.gameObject.SetActive(isMaster);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[LobbyManager] Create room failed: {message}");
            connectionStatusText.text = LocalizationManager.Instance.GetText("errors.createRoomFailed", message);
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[LobbyManager] Join random failed: {message}. Creating new room...");
            OnCreateRoomClicked();
        }

        public override void OnLeftRoom()
        {
            ShowPanel(lobbyPanel);
            ClearPlayerList();
            _loadingGameScene = false;
        }

        static void OnLeaveRoomClicked()
        {
            PhotonNetwork.LeaveRoom();
        }

        void OnCopyRoomCodeClicked()
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            string roomCode = PhotonNetwork.CurrentRoom.Name;
            GUIUtility.systemCopyBuffer = roomCode;

            if (!roomCodeDisplayText)
            {
                return;
            }

            string originalText = roomCodeDisplayText.text;
            roomCodeDisplayText.text = LocalizationManager.Instance.GetText("room.copied");
            StartCoroutine(ResetCopyButtonText(originalText));
        }

        System.Collections.IEnumerator ResetCopyButtonText(string originalText)
        {
            yield return new WaitForSeconds(1.5f);
            if (roomCodeDisplayText)
            {
                roomCodeDisplayText.text = originalText;
            }
        }

        #endregion

        #region Player List Management

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                AssignColorToNewPlayer(newPlayer);
            }

            UpdatePlayerList();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            UpdatePlayerList();
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (roomPanel && roomPanel.activeSelf)
                UpdatePlayerList();
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            bool isMaster = PhotonNetwork.IsMasterClient;
            grayBoxToggle.interactable = isMaster;
            playerCountDropdown.interactable = isMaster;
            turnTimerToggle.interactable = isMaster;
            turnTimerInput.interactable = isMaster && (turnTimerToggle && turnTimerToggle.isOn);
            startGameButton.gameObject.SetActive(isMaster);

            UpdatePlayerList();
        }

        void UpdatePlayerList()
        {
            ClearPlayerList();

            foreach (Player player in PhotonNetwork.PlayerList.OrderBy(p => p.ActorNumber))
            {
                GameObject item = Instantiate(playerListItemPrefab, playerListContent);
                var listItem = item.GetComponent<PlayerListItem>();

                if (listItem)
                {
                    byte colorIndex = GetPlayerColor(player);
                    listItem.SetPlayerInfo(player.NickName, player.IsMasterClient, colorIndex);
                }

                _playerListItems[player.ActorNumber] = item;
            }

            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            startGameButton.interactable = playerCount >= 2 && playerCount <= 4;
        }

        static void AssignColorToNewPlayer(Player player)
        {

            if (player.CustomProperties.ContainsKey("PlayerColor"))
            {
                return;
            }

            var usedColors = new HashSet<byte>();
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                if (p.CustomProperties.TryGetValue("PlayerColor", out object color) && color is byte b)
                {
                    usedColors.Add(b);
                }
            }

            var availableColors = new List<byte> { 0, 1, 2, 3, 4 }
                .Where(c => !usedColors.Contains(c))
                .ToList();

            if (availableColors.Count == 0)
            {
                Debug.LogError($"[LobbyManager] No available colors for {player.NickName}! Room is full.");
                return;
            }

            byte newColor = availableColors[Random.Range(0, availableColors.Count)];
            player.SetCustomProperties(new Hashtable { ["PlayerColor"] = newColor });
        }

        static byte GetPlayerColor(Player player)
        {
            if (player.CustomProperties.TryGetValue("PlayerColor", out object color) && color is byte b)
            {
                return b;
            }
            return 0;
        }

        void ClearPlayerList()
        {
            foreach (GameObject item in _playerListItems.Values)
            {
                Destroy(item);
            }
            _playerListItems.Clear();
        }

        #endregion

        #region Game Start

        void OnStartGameClicked()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            if (playerCount is < 2 or > 4)
            {
                Debug.LogWarning($"[LobbyManager] Invalid player count: {playerCount}");
                return;
            }

            string playerNames = string.Join(", ", PhotonNetwork.PlayerList.Select(p => p.NickName));
            string message = LocalizationManager.Instance.GetText("room.confirmStart", playerCount.ToString()) + "\n\n" + playerNames;
            ShowConfirmation(message, StartGame);
        }

        void StartGame()
        {
            var config = new Hashtable
            {
                ["UseGrayBox"] = grayBoxToggle.isOn,
                ["TurnTimer"] = int.TryParse(turnTimerInput.text, out int timer) ? timer : 0,
                ["GameStarted"] = true
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(config);

            PhotonNetwork.CurrentRoom.IsOpen = false;

            _loadingGameScene = true;
            StartCoroutine(LoadGameSceneAfterSync());
        }

        IEnumerator LoadGameSceneAfterSync()
        {
            const float timeout = 5f;
            var elapsed = 0f;

            while (elapsed < timeout)
            {
                if (PhotonNetwork.CurrentRoom?.CustomProperties != null
                    && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameStarted", out object started)
                    && started is true)
                {
                    PhotonNetwork.LoadLevel(gameSceneName);
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Debug.LogError("[LobbyManager] Timeout waiting for GameStarted property to sync!");
            PhotonNetwork.LoadLevel(gameSceneName);
        }

        void ShowConfirmation(string message, System.Action onConfirm)
        {
            if (!confirmationPopup)
            {

                onConfirm?.Invoke();
                return;
            }

            _pendingConfirmAction = onConfirm;

            if (confirmationText)
            {
                confirmationText.text = message;
            }

            confirmationPopup.SetActive(true);
        }

        void OnConfirmYes()
        {
            if (confirmationPopup)
            {
                confirmationPopup.SetActive(false);
            }

            _pendingConfirmAction?.Invoke();
            _pendingConfirmAction = null;
        }

        void OnConfirmNo()
        {
            if (confirmationPopup)
            {
                confirmationPopup.SetActive(false);
            }

            _pendingConfirmAction = null;
        }

        void OnTurnTimerToggleChanged(bool isOn)
        {
            if (!turnTimerInput)
            {
                return;
            }

            turnTimerInput.interactable = isOn;

            if (!isOn)
            {
                turnTimerInput.text = "0";
            }
        }

        #endregion

        #region UI Helpers

        void ShowPanel(GameObject panel)
        {
            connectionPanel.SetActive(panel == connectionPanel);
            lobbyPanel.SetActive(panel == lobbyPanel);
            roomBrowserPanel.SetActive(panel == roomBrowserPanel);
            roomPanel.SetActive(panel == roomPanel);
        }

        #endregion

        #region Room Browser

        void OnBrowseRoomsClicked()
        {
            ShowPanel(roomBrowserPanel);
            UpdateRoomBrowser();
        }

        void OnBackToLobbyClicked()
        {
            ShowPanel(lobbyPanel);
            ClearRoomBrowser();
        }

        void OnRefreshRoomsClicked()
        {
            UpdateRoomBrowser();
        }

        void OnJoinSelectedClicked()
        {
            if (string.IsNullOrEmpty(_selectedRoomName))
            {
                Debug.LogWarning("[LobbyManager] No room selected");
                return;
            }

            if (!_cachedRoomList.TryGetValue(_selectedRoomName, out RoomInfo selectedRoom))
            {
                Debug.LogWarning($"[LobbyManager] Selected room '{_selectedRoomName}' no longer exists");
                _selectedRoomName = null;
                UpdateRoomBrowser();
                return;
            }

            if (selectedRoom.PlayerCount >= selectedRoom.MaxPlayers)
            {
                Debug.LogWarning($"[LobbyManager] Room '{_selectedRoomName}' is full");
                return;
            }

            if (!selectedRoom.IsOpen)
            {
                Debug.LogWarning($"[LobbyManager] Room '{_selectedRoomName}' is closed");
                return;
            }

            PhotonNetwork.JoinRoom(_selectedRoomName);
        }

        public void OnRoomListItemClicked(string roomName)
        {
            _selectedRoomName = roomName;

            foreach (KeyValuePair<string, GameObject> kvp in _roomListItems)
            {
                var item = kvp.Value.GetComponent<RoomListItem>();
                if (item)
                {
                    item.SetSelected(kvp.Key == roomName);
                }
            }

            UpdateJoinButton();
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (RoomInfo info in roomList)
            {
                if (info.RemovedFromList)
                    _cachedRoomList.Remove(info.Name);
                else
                    _cachedRoomList[info.Name] = info;
            }

            if (roomBrowserPanel && roomBrowserPanel.activeSelf)
            {
                UpdateRoomBrowser();
            }
        }

        void UpdateRoomBrowser()
        {
            ClearRoomBrowser();

            List<RoomInfo> availableRooms = _cachedRoomList.Values
                .Where(room => room.IsVisible && room.IsOpen)
                .Where(room =>
                {
                    if (room.CustomProperties != null &&
                        room.CustomProperties.TryGetValue("GameStarted", out object started))
                    {
                        return !(bool)started;
                    }
                    return true;
                })
                .Where(room =>
                {
                    if (room.CustomProperties != null &&
                        room.CustomProperties.TryGetValue("IsPrivate", out object isPrivate))
                    {
                        return !(bool)isPrivate;
                    }
                    return true;
                })
                .OrderByDescending(room => room.PlayerCount)
                .ThenBy(room => room.Name)
                .ToList();

            if (emptyRoomListText)
            {
                emptyRoomListText.gameObject.SetActive(availableRooms.Count == 0);
            }

            foreach (RoomInfo roomInfo in availableRooms)
            {
                GameObject itemObj = Instantiate(roomListItemPrefab, roomListContent);
                var item = itemObj.GetComponent<RoomListItem>();

                if (item)
                {
                    item.Initialize(roomInfo, this);

                    if (roomInfo.Name == _selectedRoomName)
                    {
                        item.SetSelected(true);
                    }
                }

                _roomListItems[roomInfo.Name] = itemObj;
            }

            UpdateJoinButton();
        }

        void ClearRoomBrowser()
        {
            foreach (GameObject item in _roomListItems.Values.Where(item => item))
            {
                Destroy(item);
            }
            _roomListItems.Clear();
            _selectedRoomName = null;

            if (emptyRoomListText)
            {
                emptyRoomListText.gameObject.SetActive(false);
            }
        }

        void UpdateJoinButton()
        {
            if (!joinSelectedButton) return;

            var canJoin = false;

            if (!string.IsNullOrEmpty(_selectedRoomName) &&
                _cachedRoomList.TryGetValue(_selectedRoomName, out RoomInfo room))
            {
                canJoin = room.PlayerCount < room.MaxPlayers && room.IsOpen;
            }

            joinSelectedButton.interactable = canJoin;
        }

        #endregion
    }
}
