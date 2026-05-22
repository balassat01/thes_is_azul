using System;
using System.Collections.Generic;
using System.Linq;
using Core.Domain.Rules;
using Gameplay;
using Localization;
using Netcode;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        [Header("Pattern Lines")]
        [SerializeField] Button[] patternRowButtons = new Button[5];
        [SerializeField] Transform[] patternLineTileContainers = new Transform[5];

        [Header("Box Grid (5x5)")]
        [SerializeField] Image[] boxCells = new Image[25];

        [Header("Floor")]
        [SerializeField] Transform floorTileContainer;

        [Header("Pattern Line Visuals")]
        [SerializeField] GameObject patternLineTileIconPrefab;

        [Header("Board Info Display")]
        [Tooltip("Used to display the owner name label when viewing another player's board")]
        [SerializeField] GameObject boardOwnerContainer;
        [SerializeField] TextMeshProUGUI boardOwnerLabel;

        [Header("Help Overlay")]
        [Tooltip("Button with '?' icon that toggles the help overlay")]
        [SerializeField] Button helpButton;
        [Tooltip("Semi-transparent overlay panel showing bonus information")]
        [SerializeField] GameObject helpOverlayPanel;

        DraftSelectionModel _sel;
        bool _helpOverlayVisible;
        readonly List<List<GameObject>> _patternLineTiles = new List<List<GameObject>>();
        readonly List<GameObject> _floorLineTiles = new List<GameObject>();
        System.Collections.Generic.Dictionary<int, string> _playerNames;

        bool _cachedMyTurn;
        bool _cachedViewingOwnBoard;
        bool _cachedGrayBoxPlacementTurn;
        readonly int[] _cachedPatternCounts = new int[5];

        public void Init(DraftSelectionModel sel) { _sel = sel; }

        void Awake()
        {
            FixButtonRaycasting();

            if (helpOverlayPanel)
            {
                helpOverlayPanel.SetActive(false);
                _helpOverlayVisible = false;
            }

            if (helpButton)
            {
                helpButton.onClick.AddListener(ToggleHelpOverlay);
            }
        }

        void FixButtonRaycasting()
        {
            foreach (string bgName in new[] { "PlayerBoard", "GameBoard" })
            {
                var go = GameObject.Find(bgName);
                if (!go) continue;
                var img = go.GetComponent<Image>();
                if (img && img.raycastTarget && !go.GetComponent<Button>() && !go.GetComponent<EventTrigger>())
                    img.raycastTarget = false;
            }

            if (patternRowButtons == null) return;
            foreach (var btn in patternRowButtons)
            {
                if (!btn) continue;

                var img = btn.GetComponent<Image>();
                if (!img)
                {
                    img = btn.gameObject.AddComponent<Image>();
                    img.color = new Color(0, 0, 0, 0);
                }
                img.raycastTarget = true;
                if (!btn.targetGraphic) btn.targetGraphic = img;

                var t = btn.transform.parent;
                while (t != null)
                {
                    var parentImg = t.GetComponent<Image>();
                    if (parentImg && parentImg.raycastTarget
                        && !t.GetComponent<Button>() && !t.GetComponent<EventTrigger>())
                        parentImg.raycastTarget = false;
                    t = t.parent;
                }
            }

            if (patternLineTileContainers != null)
            {
                foreach (var container in patternLineTileContainers)
                {
                    if (!container) continue;
                    var containerImg = container.GetComponent<Image>();
                    if (containerImg) containerImg.raycastTarget = false;
                    foreach (var childImg in container.GetComponentsInChildren<Image>(includeInactive: true))
                        childImg.raycastTarget = false;
                }
            }
        }

        void Start()
        {
            _playerNames = new System.Collections.Generic.Dictionary<int, string>();
            if (PhotonNetwork.CurrentRoom?.Players != null)
            {
                foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
                {
                    _playerNames[kvp.Key] = kvp.Value.NickName ?? $"Player {kvp.Key}";
                }
            }
        }

        void ToggleHelpOverlay()
        {
            if (!helpOverlayPanel) return;

            _helpOverlayVisible = !_helpOverlayVisible;
            helpOverlayPanel.SetActive(_helpOverlayVisible);
        }

        public void HideHelpOverlay()
        {
            if (!helpOverlayPanel) return;

            _helpOverlayVisible = false;
            helpOverlayPanel.SetActive(false);
        }

        public void Render(RoomPropsCodec.Snapshot s, bool myTurn, int myActor, byte[] baseOrder)
        {
            RenderPlayerBoard(s, myTurn, myActor, myActor, baseOrder, grayBoxPlacementTurn: false);
        }

        public void RenderPlayerBoard(RoomPropsCodec.Snapshot snapshot, bool myTurn, int myActor, int displayActor, byte[] baseOrder, bool grayBoxPlacementTurn = false)
        {
            int displaySeat = Array.IndexOf(snapshot.Actors, displayActor);
            if (displaySeat < 0) return;
            RoomPropsCodec.PlayerSnap playerSnap = snapshot.Players[displaySeat];

            bool viewingOwnBoard = (displayActor == myActor);

            _cachedMyTurn              = myTurn;
            _cachedViewingOwnBoard     = viewingOwnBoard;
            _cachedGrayBoxPlacementTurn = grayBoxPlacementTurn;
            for (var i = 0; i < 5; i++)
                _cachedPatternCounts[i] = playerSnap.PatternCounts[i];

            if (patternRowButtons == null || patternRowButtons.Length < 5)
            {
                Debug.LogWarning("[BoardPresenter] patternRowButtons array not properly assigned in Inspector! Expected 5 Button elements.");
            }
            if (boxCells == null || boxCells.Length < 25)
            {
                Debug.LogWarning("[BoardPresenter] boxCells array not properly assigned in Inspector! Expected 25 Image elements.");
            }

            if (boardOwnerLabel)
            {
                if (viewingOwnBoard)
                {
                    boardOwnerContainer.SetActive(false);
                }
                else
                {
                    boardOwnerContainer.SetActive(true);
                    string playerName = _playerNames != null && _playerNames.TryGetValue(displayActor, out string name)
                        ? name
                        : $"Player {displayActor}";
                    boardOwnerLabel.text = LocalizationManager.Instance.GetText("playerBoard.boardOwner", playerName);
                }
            }

            while (_patternLineTiles.Count < 5)
            {
                _patternLineTiles.Add(new List<GameObject>());
            }

            for (var r = 0; r < 5; r++)
            {
                int cap = r + 1;
                int cnt = playerSnap.PatternCounts[r];
                byte color = playerSnap.PatternColors[r];

                RenderPatternLineTiles(r, color, cnt, cap);

                if (patternRowButtons == null || r >= patternRowButtons.Length || !patternRowButtons[r])
                {
                    Debug.LogWarning($"[BoardPresenter] Row {r}: patternRowButtons[{r}] is null or missing!");
                    continue;
                }

                Button btn = patternRowButtons[r];
                btn.onClick.RemoveAllListeners();

                int captureRow = r;
                if (grayBoxPlacementTurn && viewingOwnBoard)
                {
                    bool rowIsFull    = playerSnap.PatternCounts[r] == r + 1;
                    bool noRowSelected = _sel.GrayBoxSelectedRow < 0;
                    btn.interactable  = rowIsFull && noRowSelected;
                    if (btn.interactable)
                        btn.onClick.AddListener(() => OnPatternRowClicked(captureRow));
                }
                else
                {
                    bool shouldBeInteractable = viewingOwnBoard && myTurn && _sel.HasSelection;
                    btn.interactable = shouldBeInteractable;
                    if (btn.interactable)
                        btn.onClick.AddListener(() => OnPatternRowClicked(captureRow));
                }
            }

            bool[] grayBoxValidCols = null;
            int grayBoxSelectedColor = -1;
            if (grayBoxPlacementTurn && viewingOwnBoard && _sel.GrayBoxSelectedRow >= 0)
            {
                int selRow = _sel.GrayBoxSelectedRow;
                byte colorByte = playerSnap.PatternColors[selRow];
                if (colorByte != 255)
                {
                    grayBoxSelectedColor = colorByte;
                    grayBoxValidCols = GrayBoxRule.GetValidColumns(selRow, grayBoxSelectedColor, playerSnap.BoxColors);
                }
            }

            for (var r = 0; r < 5; r++)
            for (var c = 0; c < 5; c++)
            {
                bool occ = (playerSnap.BoxBytes[r] & (1 << c)) != 0;
                int idx = r * 5 + c;

                if (boxCells == null || idx >= boxCells.Length || !boxCells[idx]) continue;

                int colorAtCell = -1;
                if (TileColorManager.Instance && baseOrder != null)
                    colorAtCell = StandardBoxMap.ColorAtCell(Array.ConvertAll(baseOrder, b => (int)b), r, c);

                boxCells[idx].enabled = true;

                switch (occ)
                {
                    case true when colorAtCell >= 0:
                        TileColorManager.Instance.ApplyTileVisuals(boxCells[idx], (byte)colorAtCell);
                        break;
                    case true:
                        boxCells[idx].sprite = null;
                        boxCells[idx].color = Color.white;
                        break;
                    default:
                        if (colorAtCell >= 0 && TileColorManager.Instance)
                            TileColorManager.Instance.ApplyBoxPlaceholderVisuals(boxCells[idx], (byte)colorAtCell);
                        else
                        { boxCells[idx].sprite = null; boxCells[idx].color = new Color(1f, 1f, 1f, 0.1f); }
                        break;
                }

                var trigger = boxCells[idx].GetComponent<EventTrigger>();
                if (grayBoxPlacementTurn && viewingOwnBoard && _sel.GrayBoxSelectedRow == r && !occ && grayBoxValidCols != null)
                {
                    bool isValid = grayBoxValidCols[c];

                    boxCells[idx].color = isValid
                        ? new Color(0.2f, 1f, 0.2f, 0.9f)
                        : new Color(1f, 0.2f, 0.2f, 0.5f);

                    if (isValid)
                    {
                        if (!trigger) trigger = boxCells[idx].gameObject.AddComponent<EventTrigger>();
                        trigger.triggers.Clear();
                        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                        int captureRow = r, captureCol = c;
                        entry.callback.AddListener(_ =>
                        {
                            if (AzulNet.Instance) AzulNet.Instance.SendPlaceInBox(captureRow, captureCol);
                            _sel.ClearGrayBoxRow();
                        });
                        trigger.triggers.Add(entry);
                    }
                    else if (trigger)
                    {
                        trigger.triggers.Clear();
                    }
                }
                else
                {
                    if (trigger) trigger.triggers.Clear();
                }
            }

            RenderFloorLine(playerSnap.Floor);
        }

        public void OnPatternRowClicked(int row)
        {
            if (_sel == null || row < 0 || row > 4) return;

            if (_cachedGrayBoxPlacementTurn && _cachedViewingOwnBoard)
            {
                bool rowIsFull    = row < _cachedPatternCounts.Length && _cachedPatternCounts[row] == row + 1;
                bool noRowSelected = _sel.GrayBoxSelectedRow < 0;
                if (rowIsFull && noRowSelected)
                    _sel.SelectGrayBoxRow(row);
                return;
            }

            if (_cachedViewingOwnBoard && _cachedMyTurn && _sel.HasSelection)
            {
                AzulNet.Instance.SendTakeTiles(
                    sourceIndex: _sel.SourceIndex,
                    color:       _sel.Color,
                    patternRow:  row,
                    fromCenter:  _sel.FromCenter);
                _sel.Clear();
            }
        }

        void RenderPatternLineTiles(int row, byte color, int count, int capacity)
        {
            foreach (GameObject tile in _patternLineTiles[row].Where(tile => tile))
            {
                Destroy(tile);
            }
            _patternLineTiles[row].Clear();

            if (count == 0 || color == 255 || patternLineTileContainers == null ||
                row >= patternLineTileContainers.Length || patternLineTileContainers[row] == null)
            {
                return;
            }

            Transform container = patternLineTileContainers[row];

            for (var i = 0; i < count; i++)
            {
                GameObject tileIcon = CreatePatternLineTileIcon(color, container);
                if (tileIcon)
                {
                    _patternLineTiles[row].Add(tileIcon);
                }
            }
        }

        GameObject CreatePatternLineTileIcon(byte color, Transform parent)
        {
            GameObject icon;

            if (patternLineTileIconPrefab)
            {
                icon = Instantiate(patternLineTileIconPrefab, parent);

                if (!icon.GetComponent<Image>())
                {
                    icon.AddComponent<Image>();
                }
            }
            else
            {
                icon = new GameObject($"PatternTile_{color}", typeof(RectTransform));
                icon.transform.SetParent(parent, false);
                icon.AddComponent<Image>();

                var rt = icon.GetComponent<RectTransform>();
                float size = TileColorManager.Instance?.GetPatternLineTileSize() ?? 30f;
                rt.sizeDelta = new Vector2(size, size);
            }

            var image = icon.GetComponent<Image>();
            if (image && TileColorManager.Instance)
            {
                TileColorManager.Instance.ApplyTileVisuals(image, color);

                image.raycastTarget = false;
            }

            foreach (var childImg in icon.GetComponentsInChildren<Image>(includeInactive: true))
                childImg.raycastTarget = false;

            return icon;
        }

        void RenderFloorLine(byte[] floor)
        {
            foreach (GameObject tile in _floorLineTiles.Where(tile => tile))
            {
                Destroy(tile);
            }
            _floorLineTiles.Clear();

            var floorCount = 0;
            for (var i = 0; i < 7; i++)
            {
                byte tileValue = floor[i];
                if (tileValue != 255)
                {
                    floorCount++;

                    if (floorTileContainer && TileColorManager.Instance)
                    {
                        GameObject tileIcon = CreateFloorLineTileIcon(tileValue, floorTileContainer);
                        if (tileIcon)
                        {
                            _floorLineTiles.Add(tileIcon);
                        }
                    }
                }
            }
        }

        GameObject CreateFloorLineTileIcon(byte tileValue, Transform parent)
        {
            var icon = new GameObject("FloorTile", typeof(RectTransform));
            icon.transform.SetParent(parent, false);

            var img = icon.AddComponent<Image>();

            img.raycastTarget = false;

            var rt = icon.GetComponent<RectTransform>();

            float size = TileColorManager.Instance?.GetFloorLineTileSize() ?? 40f;
            rt.sizeDelta = new Vector2(size, size);

            switch (tileValue)
            {
                case 5:
                {
                    if (TileColorManager.Instance)
                        TileColorManager.Instance.ApplyTokenVisuals(img);
                    break;
                }
                case < 5:
                {
                    if (TileColorManager.Instance)
                        TileColorManager.Instance.ApplyTileVisuals(img, tileValue);
                    break;
                }
            }

            return icon;
        }
    }
}
