using System;
using System.Collections.Generic;
using System.Linq;
using Core.Domain;
using Core.Domain.Rules;
using ExitGames.Client.Photon;
using Localization;
using Netcode;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class EndGamePresenter : MonoBehaviourPunCallbacks
    {
        const string VotePropertyKey = "NewGameVote";
        const float MasterClientCooldownSeconds = 30f;

        [Header("End Game Panel")]
        [Tooltip("Root panel that shows/hides based on game over state")]
        [SerializeField] GameObject endGamePanel;

        [Header("Table - Player Columns")]
        [Tooltip("Parent GameObjects for each player column (to hide entire columns)")]
        [SerializeField] GameObject[] playerColumnRoots = new GameObject[4];

        [Header("Table - Player Names (Row 1)")]
        [SerializeField] TextMeshProUGUI[] playerNameTexts = new TextMeshProUGUI[4];

        [Header("Table - Base Score (Row 2)")]
        [SerializeField] TextMeshProUGUI[] baseScoreTexts = new TextMeshProUGUI[4];
        [SerializeField] Image[] baseScoreCrowns = new Image[4];

        [Header("Table - Completed Rows (Row 3)")]
        [SerializeField] TextMeshProUGUI[] completedRowTexts = new TextMeshProUGUI[4];
        [SerializeField] Image[] completedRowCrowns = new Image[4];

        [Header("Table - Completed Columns (Row 4)")]
        [SerializeField] TextMeshProUGUI[] completedColumnTexts = new TextMeshProUGUI[4];
        [SerializeField] Image[] completedColumnCrowns = new Image[4];

        [Header("Table - Completed Colors (Row 5)")]
        [SerializeField] TextMeshProUGUI[] completedColorTexts = new TextMeshProUGUI[4];
        [SerializeField] Image[] completedColorCrowns = new Image[4];

        [Header("Table - Final Score (Row 6)")]
        [SerializeField] TextMeshProUGUI[] finalScoreTexts = new TextMeshProUGUI[4];

        [Header("Table - Placement (Row 7)")]
        [Tooltip("Crown images showing placement (1st-4th)")]
        [SerializeField] Image[] placementImages = new Image[4];

        [Header("Crown Sprite")]
        [Tooltip("Crown sprite for placement display")]
        [SerializeField] Sprite placementCrownSprite;

        [Header("Placement Materials")]
        [Tooltip("Material for 1st place crown")]
        [SerializeField] Material goldMaterial;
        [Tooltip("Material for 2nd place crown")]
        [SerializeField] Material silverMaterial;
        [Tooltip("Material for 3rd place crown")]
        [SerializeField] Material bronzeMaterial;
        [Tooltip("Material for 4th place crown")]
        [SerializeField] Material ironMaterial;

        [Header("Action Buttons")]
        [SerializeField] Button returnToMenuButton;
        [SerializeField] Button newGameButton;

        [Header("Vote Counter Display")]
        [Tooltip("Green person icon next to new game button")]
        [SerializeField] Image votePersonIcon;
        [Tooltip("Text showing vote count")]
        [SerializeField] TextMeshProUGUI voteCountText;

        [Header("Confirmation Popup")]
        [SerializeField] GameObject confirmationPopup;
        [SerializeField] TextMeshProUGUI confirmationText;
        [SerializeField] Button confirmYesButton;
        [SerializeField] Button confirmNoButton;

        float _gameOverStartTime;
        bool _isGameOver;
        bool _hasVoted;
        Action _pendingConfirmAction;

        bool _currentGameStarted;

        void Awake()
        {

            if (endGamePanel) endGamePanel.SetActive(false);
            _currentGameStarted = false;
        }

        void Start()
        {
            NetEvents.OnSnapshot += Render;

            if (endGamePanel)
            {
                endGamePanel.SetActive(false);
            }

            if (confirmationPopup)
            {
                confirmationPopup.SetActive(false);
            }

            if (returnToMenuButton)
            {
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
            }

            if (newGameButton)
            {
                newGameButton.onClick.AddListener(OnNewGameClicked);
            }

            if (confirmYesButton)
            {
                confirmYesButton.onClick.AddListener(OnConfirmYes);
            }

            if (confirmNoButton)
            {
                confirmNoButton.onClick.AddListener(OnConfirmNo);
            }

            HideAllCrowns();
            UpdateVoteDisplay();
        }

        void Update()
        {
            if (_isGameOver)
            {
                UpdateNewGameButtonState();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            NetEvents.OnSnapshot -= Render;
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey(VotePropertyKey))
            {
                UpdateVoteDisplay();
                UpdateNewGameButtonState();
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            UpdateVoteDisplay();
            UpdateNewGameButtonState();
        }

        void Render(RoomPropsCodec.Snapshot snapshot)
        {

            if (snapshot.Phase == Phase.FactoryOffer || snapshot.Phase == Phase.BoxTiling)
                _currentGameStarted = true;

            bool wasGameOver = _isGameOver;

            if (snapshot.Phase != Phase.GameOver)
            {
                if (endGamePanel)
                {
                    endGamePanel.SetActive(false);
                }
                _isGameOver = false;
                return;
            }

            if (!_currentGameStarted)
            {
                if (endGamePanel) endGamePanel.SetActive(false);
                _isGameOver = false;
                return;
            }

            if (!wasGameOver)
            {
                _gameOverStartTime = Time.time;
                _hasVoted = false;
                ClearMyVote();
            }

            _isGameOver = true;

            if (endGamePanel)
            {
                endGamePanel.SetActive(true);
            }

            var playerData = new List<PlayerEndGameData>();
            for (var i = 0; i < snapshot.Players.Length; i++)
            {
                RoomPropsCodec.PlayerSnap playerSnap = snapshot.Players[i];
                int actorNumber = snapshot.Actors[i];

                string nickname = PhotonNetwork.CurrentRoom?.Players.TryGetValue(actorNumber, out Player player) == true
                    ? player.NickName
                    : $"P{actorNumber}";

                (int baseScore, int rows, int cols, int colors) stats = CalculatePlayerStats(playerSnap, snapshot.BaseOrder);

                playerData.Add(new PlayerEndGameData
                {
                    Nickname = nickname,
                    FinalScore = playerSnap.Score,
                    BaseScore = stats.baseScore,
                    CompletedRows = stats.rows,
                    CompletedColumns = stats.cols,
                    CompletedColors = stats.colors
                });
            }

            List<PlayerEndGameData> rankedPlayers = playerData.OrderByDescending(p => p.FinalScore)
                                          .ThenByDescending(p => p.CompletedRows)
                                          .ToList();

            for (var i = 0; i < rankedPlayers.Count; i++)
            {
                rankedPlayers[i].Placement = i + 1;
            }

            DisplayTable(rankedPlayers);
            DisplayCategoryCrowns(rankedPlayers);
            UpdateVoteDisplay();
            UpdateNewGameButtonState();
        }

        void UpdateNewGameButtonState()
        {
            if (!newGameButton) return;

            if (PhotonNetwork.IsMasterClient)
            {
                float elapsed = Time.time - _gameOverStartTime;
                int voteCount = GetVoteCount();

                bool cooldownPassed = elapsed >= MasterClientCooldownSeconds;
                bool hasVotes = voteCount > 0;

                newGameButton.interactable = cooldownPassed && hasVotes;
            }
            else
            {
                newGameButton.interactable = true;
            }
        }

        void UpdateVoteDisplay()
        {
            int voteCount = GetVoteCount();

            if (voteCountText)
            {
                voteCountText.text = voteCount.ToString();
                voteCountText.enabled = voteCount > 0;
            }

            if (votePersonIcon)
            {
                votePersonIcon.enabled = voteCount > 0;
            }
        }

        static int GetVoteCount()
        {
            if (PhotonNetwork.CurrentRoom == null) return 0;

            var count = 0;
            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (!player.CustomProperties.TryGetValue(VotePropertyKey, out object vote) || vote is not bool b || !b)
                {
                    continue;
                }

                if (!player.IsMasterClient)
                {
                    count++;
                }
            }
            return count;
        }

        static List<string> GetVoterNames()
        {
            var names = new List<string>();
            if (PhotonNetwork.CurrentRoom == null) return names;

            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (!player.CustomProperties.TryGetValue(VotePropertyKey, out object vote) || vote is not true)
                {
                    continue;
                }

                if (!player.IsMasterClient)
                {
                    names.Add(player.NickName);
                }
            }
            return names;
        }

        void SetMyVote(bool voted)
        {
            _hasVoted = voted;
            var props = new Hashtable { { VotePropertyKey, voted } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        void ClearMyVote()
        {
            _hasVoted = false;
            var props = new Hashtable { { VotePropertyKey, false } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        void OnNewGameClicked()
        {
            if (PhotonNetwork.IsMasterClient)
            {

                List<string> voterNames = GetVoterNames();
                if (voterNames.Count == 0) return;

                string namesStr = string.Join(", ", voterNames);
                ShowConfirmation(LocalizationManager.Instance.GetText("endGame.confirmNewGame", namesStr), StartNewGame);
            }
            else
            {

                _hasVoted = !_hasVoted;
                SetMyVote(_hasVoted);
            }
        }

        void OnReturnToMenuClicked()
        {
            ShowConfirmation(LocalizationManager.Instance.GetText("endGame.confirmReturn"), () =>
            {

                ClearMyVote();

                if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.LeaveRoom();
                }

                SceneManager.LoadScene("LobbyScene");
            });
        }

        void ShowConfirmation(string message, Action onConfirm)
        {
            if (confirmationPopup == null)
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

        static void StartNewGame()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("[EndGamePresenter] Only the master client can start a new game.");
                return;
            }

            PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        }

        #region Table Display Methods

        void DisplayTable(List<PlayerEndGameData> rankedPlayers)
        {
            int playerCount = Mathf.Min(rankedPlayers.Count, 4);

            for (var i = 0; i < 4; i++)
            {
                SetColumnActive(i, i < playerCount);
            }

            for (var col = 0; col < playerCount; col++)
            {
                PlayerEndGameData data = rankedPlayers[col];

                if (playerNameTexts != null && col < playerNameTexts.Length && playerNameTexts[col])
                    playerNameTexts[col].text = data.Nickname;

                if (baseScoreTexts != null && col < baseScoreTexts.Length && baseScoreTexts[col])
                    baseScoreTexts[col].text = data.BaseScore.ToString();

                if (completedRowTexts != null && col < completedRowTexts.Length && completedRowTexts[col])
                    completedRowTexts[col].text = data.CompletedRows.ToString();

                if (completedColumnTexts != null && col < completedColumnTexts.Length && completedColumnTexts[col])
                    completedColumnTexts[col].text = data.CompletedColumns.ToString();

                if (completedColorTexts != null && col < completedColorTexts.Length && completedColorTexts[col])
                    completedColorTexts[col].text = data.CompletedColors.ToString();

                if (finalScoreTexts != null && col < finalScoreTexts.Length && finalScoreTexts[col])
                    finalScoreTexts[col].text = data.FinalScore.ToString();

                if (placementImages != null && col < placementImages.Length && placementImages[col])
                    ApplyPlacementVisuals(placementImages[col], data.Placement);
            }
        }

        void SetColumnActive(int col, bool active)
        {
            if (playerColumnRoots != null && col < playerColumnRoots.Length && playerColumnRoots[col])
            {
                playerColumnRoots[col].SetActive(active);
            }
        }

        void DisplayCategoryCrowns(List<PlayerEndGameData> rankedPlayers)
        {
            HideAllCrowns();
            if (rankedPlayers.Count == 0) return;

            int maxBaseScore = rankedPlayers.Max(p => p.BaseScore);
            int maxRows = rankedPlayers.Max(p => p.CompletedRows);
            int maxCols = rankedPlayers.Max(p => p.CompletedColumns);
            int maxColors = rankedPlayers.Max(p => p.CompletedColors);

            for (var col = 0; col < rankedPlayers.Count && col < 4; col++)
            {
                PlayerEndGameData data = rankedPlayers[col];

                if (data.BaseScore == maxBaseScore && maxBaseScore > 0)
                    ShowCrown(baseScoreCrowns, col);

                if (data.CompletedRows == maxRows && maxRows > 0)
                    ShowCrown(completedRowCrowns, col);

                if (data.CompletedColumns == maxCols && maxCols > 0)
                    ShowCrown(completedColumnCrowns, col);

                if (data.CompletedColors == maxColors && maxColors > 0)
                    ShowCrown(completedColorCrowns, col);
            }
        }

        static void ShowCrown(Image[] crownArray, int index)
        {
            if (crownArray == null || index >= crownArray.Length || crownArray[index] == null)
                return;

            crownArray[index].enabled = true;
        }

        void HideAllCrowns()
        {
            HideCrownArray(baseScoreCrowns);
            HideCrownArray(completedRowCrowns);
            HideCrownArray(completedColumnCrowns);
            HideCrownArray(completedColorCrowns);
        }

        static void HideCrownArray(Image[] crowns)
        {
            if (crowns == null) return;
            foreach (Image crown in crowns)
                if (crown) crown.enabled = false;
        }

        void ApplyPlacementVisuals(Image image, int placement)
        {
            if (placementCrownSprite)
            {
                image.sprite = placementCrownSprite;
            }

            Material mat = placement switch
            {
                1 => goldMaterial,
                2 => silverMaterial,
                3 => bronzeMaterial,
                _ => ironMaterial
            };

            if (mat)
            {
                image.material = mat;
            }

            image.color = Color.white;
        }

        #endregion

        #region Score Calculation

        static (int baseScore, int rows, int cols, int colors) CalculatePlayerStats(RoomPropsCodec.PlayerSnap playerSnap, byte[] baseOrder)
        {
            var box = new bool[5][];
            for (var index = 0; index < 5; index++)
            {
                box[index] = new bool[5];
            }
            for (var r = 0; r < 5; r++)
            {
                byte bits = playerSnap.BoxBytes[r];
                for (var c = 0; c < 5; c++)
                    box[r][c] = (bits & (1 << c)) != 0;
            }

            var completedRows = 0;
            for (var r = 0; r < 5; r++)
            {
                var full = true;
                for (var c = 0; c < 5; c++)
                    if (!box[r][c]) { full = false; break; }
                if (full) completedRows++;
            }

            var completedCols = 0;
            for (var c = 0; c < 5; c++)
            {
                var full = true;
                for (var r = 0; r < 5; r++)
                    if (!box[r][c]) { full = false; break; }
                if (full) completedCols++;
            }

            var completedColors = 0;
            int[] baseOrderInt = Array.ConvertAll(baseOrder, b => (int)b);
            for (var color = 0; color < 5; color++)
            {
                var count = 0;
                for (var r = 0; r < 5; r++)
                    for (var c = 0; c < 5; c++)
                        if (box[r][c] && StandardBoxMap.ColorAtCell(baseOrderInt, r, c) == color)
                            count++;
                if (count == 5) completedColors++;
            }

            int rowBonus = completedRows * 2;
            int colBonus = completedCols * 7;
            int colorBonus = completedColors * 10;
            int totalBonus = rowBonus + colBonus + colorBonus;
            int baseScore = playerSnap.Score - totalBonus;

            return (baseScore, completedRows, completedCols, completedColors);
        }

        #endregion

        class PlayerEndGameData
        {
            public string Nickname;
            public int FinalScore;
            public int BaseScore;
            public int CompletedRows;
            public int CompletedColumns;
            public int CompletedColors;
            public int Placement;
        }
    }
}
