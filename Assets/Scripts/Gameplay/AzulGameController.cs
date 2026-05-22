using System.Collections;
using Core.Domain;
using Netcode;
using Photon.Pun;
using UI.Presenters;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    public sealed class AzulGameController : MonoBehaviour
    {
        [SerializeField] FactoryPresenter factoryPresenter;
        [SerializeField] CenterPresenter centerPresenter;
        [SerializeField] BoardPresenter boardPresenter;
        [SerializeField] PlayerCardsPresenter playerCardsPresenter;

        DraftSelectionModel _selection;
        RoomPropsCodec.Snapshot _currentSnapshot;
        int? _hoveredPlayerActor;
        byte[] _baseOrder;

        void Awake()
        {
            _selection = new DraftSelectionModel();
            _selection.SelectionChanged += OnSelectionChanged;
            factoryPresenter.Init(_selection);
            centerPresenter.Init(_selection);
            boardPresenter.Init(_selection);
        }

        void OnDestroy()
        {
            if (_selection != null)
            {
                _selection.SelectionChanged -= OnSelectionChanged;
            }
        }

        void OnSelectionChanged()
        {

            EventSystem.current?.SetSelectedGameObject(null);
            RefreshDisplay();
        }

        void Start()
        {
            StartCoroutine(WaitForAzulNetAndInitialize());
        }

        IEnumerator WaitForAzulNetAndInitialize()
        {
            const float timeout = 30f;
            var elapsed = 0f;

            while (elapsed < timeout)
            {
                if (AzulNet.Instance)
                {
                    AzulNet.Instance.SnapshotUpdated += OnSnapshot;
                    AzulNet.Instance.RequestSnapshotRefresh();
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Debug.LogWarning($"[AzulGameController] AzulNet not found after {timeout}s. InRoom={PhotonNetwork.InRoom}");

            if (!PhotonNetwork.InRoom)
            {
                StartCoroutine(ReturnToLobbyAfterDelay());
                yield break;
            }

            var go = new GameObject("AzulNet");
            go.AddComponent<AzulNet>();
            yield return null;

            if (AzulNet.Instance)
            {
                AzulNet.Instance.SnapshotUpdated += OnSnapshot;
                AzulNet.Instance.RequestSnapshotRefresh();
            }
            else
            {
                Debug.LogError("[AzulGameController] Failed to create AzulNet. Returning to lobby.");
                StartCoroutine(ReturnToLobbyAfterDelay());
            }
        }

        IEnumerator ReturnToLobbyAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            SceneManager.LoadScene("LobbyScene");
        }

        void OnEnable()
        {
            if (playerCardsPresenter)
            {
                playerCardsPresenter.OnPlayerCardHoverEnter += OnPlayerCardHovered;
                playerCardsPresenter.OnPlayerCardHoverExit += OnPlayerCardHoverExit;
            }
        }

        void OnDisable()
        {
            if (AzulNet.Instance)
                AzulNet.Instance.SnapshotUpdated -= OnSnapshot;

            if (playerCardsPresenter)
            {
                playerCardsPresenter.OnPlayerCardHoverEnter -= OnPlayerCardHovered;
                playerCardsPresenter.OnPlayerCardHoverExit -= OnPlayerCardHoverExit;
            }
        }

        void OnSnapshot(RoomPropsCodec.Snapshot s)
        {
            _currentSnapshot = s;
            if (_baseOrder == null && s.BaseOrder != null) _baseOrder = s.BaseOrder;

            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
            bool isMyGrayBoxTurn = s.IsGrayBox && s.Phase == Phase.BoxTiling && s.ActiveActor == myActor;
            if (!isMyGrayBoxTurn && _selection.GrayBoxSelectedRow >= 0)
                _selection.ClearGrayBoxRow();

            RefreshDisplay();
        }

        void OnPlayerCardHovered(int actorNumber)
        {
            _hoveredPlayerActor = actorNumber;
            RefreshDisplay();
        }

        void OnPlayerCardHoverExit()
        {
            _hoveredPlayerActor = null;
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            if (_currentSnapshot == null) return;

            int myActor = PhotonNetwork.LocalPlayer.ActorNumber;
            bool myTurn = _currentSnapshot.ActiveActor == myActor && _currentSnapshot.Phase == Phase.FactoryOffer;
            bool grayBoxPlacementTurn = _currentSnapshot.IsGrayBox
                && _currentSnapshot.Phase == Phase.BoxTiling
                && _currentSnapshot.ActiveActor == myActor;

            int displayActor = _hoveredPlayerActor ?? myActor;

            factoryPresenter.Render(_currentSnapshot, myTurn);
            centerPresenter.Render(_currentSnapshot, myTurn);
            boardPresenter.RenderPlayerBoard(_currentSnapshot, myTurn, myActor, displayActor, _baseOrder, grayBoxPlacementTurn);
            playerCardsPresenter.Render(_currentSnapshot);
        }
    }

    public sealed class DraftSelectionModel
    {
        public event System.Action SelectionChanged;

        public bool HasSelection => Source.HasValue;
        public bool FromCenter => Source == 1;
        public int SourceIndex = -1;
        public byte Color;
        public byte? Source;

        public int GrayBoxSelectedRow = -1;

        public void SelectFromFactory(int factoryIndex, byte color)
        {
            Source = 0; SourceIndex = factoryIndex; Color = color;
            SelectionChanged?.Invoke();
        }
        public void SelectFromCenter(byte color)
        {
            Source = 1; SourceIndex = -1; Color = color;
            SelectionChanged?.Invoke();
        }

        public void Clear()
        {
            Source = null; SourceIndex = -1;
            SelectionChanged?.Invoke();
        }

        public void SelectGrayBoxRow(int row)
        {
            GrayBoxSelectedRow = row;
            SelectionChanged?.Invoke();
        }

        public void ClearGrayBoxRow()
        {
            GrayBoxSelectedRow = -1;
            SelectionChanged?.Invoke();
        }
    }
}
