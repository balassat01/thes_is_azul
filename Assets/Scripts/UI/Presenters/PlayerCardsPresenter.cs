using System;
using Core.Domain;
using Localization;
using Netcode;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class PlayerCardsPresenter : MonoBehaviour
    {
        [Header("Player Cards")]
        [SerializeField] Transform cardsRoot;
        [SerializeField] TextMeshProUGUI[] nameLabels;
        [SerializeField] TextMeshProUGUI[] scoreLabels;
        [SerializeField] Image[] cardBorders;
        [SerializeField] Image[] colorBadges;
        [SerializeField] GameObject[] playerCardObjects;

        [Header("Player Colors")]
        [SerializeField] Color bluePlayerColor = new Color(0.2588235f, 0.5607843f, 0.6078432f, 0.9f);
        [SerializeField] Color brownPlayerColor = new Color(0.5137255f, 0.3568628f, 0.1921569f, 0.9f);
        [SerializeField] Color whitePlayerColor = new Color(0.9764706f, 0.9019608f, 0.764706f, 0.9f);
        [SerializeField] Color redPlayerColor = new Color(0.5490196f, 0.09019608f, 0.2235294f, 0.9f);
        [SerializeField] Color blackPlayerColor = new Color(0.4627451f, 0.2156863f, 0.04705883f, 0.9f);

        [Header("Active Player Indicators")]
        [SerializeField] GameObject[] activePlayerIndicators;

        [Header("Game Status")]
        [Tooltip("Display current round number and phase")]
        [SerializeField] TextMeshProUGUI gameStateDisplay;

        public event Action<int> OnPlayerCardHoverEnter;
        public event Action OnPlayerCardHoverExit;

        RoomPropsCodec.Snapshot _currentSnapshot;
        byte[] _idColors;
        System.Collections.Generic.Dictionary<int, string> _playerNames = new System.Collections.Generic.Dictionary<int, string>();

        void Start()
        {
            SetupHoverHandlers();

            if (gameStateDisplay) gameStateDisplay.text = string.Empty;

            if (PhotonNetwork.CurrentRoom?.Players != null)
            {
                foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
                {
                    _playerNames[kvp.Key] = kvp.Value.NickName ?? $"P{kvp.Key}";
                }
            }
        }

        void SetCardEdgeColors(byte[] idColors)
        {
            if (playerCardObjects == null || idColors == null) return;

            for (var i = 0; i < playerCardObjects.Length && i < idColors.Length; i++)
            {
                if (!playerCardObjects[i]) {
                    continue;
                }

                var image = playerCardObjects[i].GetComponent<Image>();
                if (!image) {
                    continue;
                }

                byte colorIndex = idColors[i];
                image.color = colorIndex switch
                {
                    0 => bluePlayerColor,
                    1 => brownPlayerColor,
                    2 => whitePlayerColor,
                    3 => blackPlayerColor,
                    4 => redPlayerColor,
                    _ => image.color
                };
            }
        }

        public void Render(RoomPropsCodec.Snapshot snapshot)
        {
            _currentSnapshot = snapshot;

            if (_idColors == null && snapshot.IdColors != null)
            {
                _idColors = snapshot.IdColors;
            }

            if (_playerNames.Count == 0 && PhotonNetwork.CurrentRoom?.Players != null)
            {
                foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
                {
                    _playerNames[kvp.Key] = kvp.Value.NickName ?? $"P{kvp.Key}";
                }
            }

            if (gameStateDisplay)
            {
                string phase = GetPhase(snapshot);
                gameStateDisplay.text = LocalizationManager.Instance.GetText("game.roundPhase", snapshot.Round.ToString(), phase);
            }

            SetCardEdgeColors(_idColors);

            int playerCount = snapshot.Players.Length;

            for (var i = 0; i < playerCount; i++)
            {
                RoomPropsCodec.PlayerSnap playerSnap = snapshot.Players[i];
                int actorNumber = snapshot.Actors[i];
                bool isActivePlayer = actorNumber == snapshot.ActiveActor;

                if (playerCardObjects != null && i < playerCardObjects.Length && playerCardObjects[i])
                {
                    playerCardObjects[i].SetActive(true);
                }

                if (nameLabels != null && i < nameLabels.Length && nameLabels[i])
                {
                    nameLabels[i].text = _playerNames.TryGetValue(actorNumber, out string name)
                        ? name
                        : $"P{actorNumber}";
                }

                if (scoreLabels != null && i < scoreLabels.Length && scoreLabels[i])
                {
                    scoreLabels[i].text = LocalizationManager.Instance.GetText("game.score", playerSnap.Score.ToString());
                }

                if (colorBadges != null && i < colorBadges.Length && colorBadges[i]
                    && _idColors != null && i < _idColors.Length && TileColorManager.Instance)
                {
                    byte playerId = _idColors[i];

                    Sprite tileSprite = TileColorManager.Instance.GetTileSprite(playerId);

                    if (tileSprite)
                    {
                        colorBadges[i].sprite = tileSprite;
                        colorBadges[i].color = Color.white;
                    }
                    else
                    {
                        colorBadges[i].sprite = null;
                        colorBadges[i].color = TileColorManager.Instance.GetTileColor(playerId);
                    }

                    colorBadges[i].enabled = true;
                }

                if (activePlayerIndicators != null && i < activePlayerIndicators.Length && activePlayerIndicators[i])
                {
                    activePlayerIndicators[i].SetActive(isActivePlayer);
                }
            }

            if (playerCardObjects != null)
            {
                for (var i = playerCount; i < playerCardObjects.Length; i++)
                {
                    if (playerCardObjects[i])
                    {
                        playerCardObjects[i].SetActive(false);
                    }

                    if (activePlayerIndicators != null && i < activePlayerIndicators.Length && activePlayerIndicators[i])
                    {
                        activePlayerIndicators[i].SetActive(false);
                    }
                }
            }
        }

        static string GetPhase(RoomPropsCodec.Snapshot snapshot)
        {
            string key = snapshot.Phase switch
            {
                Phase.FactoryOffer => "game.phaseDrafting",
                Phase.BoxTiling => "game.phaseTiling",
                Phase.Refill => "game.phaseRefilling",
                Phase.GameOver => "game.phaseGameOver",
                _ => null
            };

            return key != null ? LocalizationManager.Instance.GetText(key) : snapshot.Phase.ToString();
        }

        void SetupHoverHandlers()
        {
            if (playerCardObjects == null) return;

            for (var i = 0; i < playerCardObjects.Length; i++)
            {
                if (playerCardObjects[i] == null) continue;

                int cardIndex = i;

                var trigger = playerCardObjects[i].GetComponent<EventTrigger>();
                if (!trigger)
                {
                    trigger = playerCardObjects[i].AddComponent<EventTrigger>();
                }

                trigger.triggers.Clear();

                var pointerEnter = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerEnter
                };
                pointerEnter.callback.AddListener((_) => OnCardHoverEnter(cardIndex));
                trigger.triggers.Add(pointerEnter);

                var pointerExit = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerExit
                };
                pointerExit.callback.AddListener((_) => OnCardHoverExit());
                trigger.triggers.Add(pointerExit);
            }
        }

        void OnCardHoverEnter(int cardIndex)
        {
            if (_currentSnapshot == null || cardIndex >= _currentSnapshot.Actors.Length) return;

            int actorNumber = _currentSnapshot.Actors[cardIndex];
            OnPlayerCardHoverEnter?.Invoke(actorNumber);
        }

        void OnCardHoverExit()
        {
            OnPlayerCardHoverExit?.Invoke();
        }
    }
}
