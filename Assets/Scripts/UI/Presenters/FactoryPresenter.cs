using System.Collections.Generic;
using Gameplay;
using Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class FactoryPresenter : MonoBehaviour
    {
        [SerializeField] Transform factoriesRoot;
        [SerializeField] GameObject factoryDiscPrefab;
        [SerializeField] GameObject tilePrefab;

        DraftSelectionModel _sel;
        readonly List<List<GameObject>> _spawned = new List<List<GameObject>>();

        public void Init(DraftSelectionModel sel) { _sel = sel; }

        public void Render(RoomPropsCodec.Snapshot s, bool myTurn)
        {
            float factorySize = CalculateFactorySize();

            while (factoriesRoot.childCount < s.FactoryCount)
            {
                GameObject go = Instantiate(factoryDiscPrefab, factoriesRoot);
                go.name = $"Factory{factoriesRoot.childCount - 1}";

                var rt = go.GetComponent<RectTransform>();
                if (rt)
                {
                    rt.sizeDelta = new Vector2(factorySize, factorySize);
                }
            }

            UpdateExistingFactorySizes(factorySize);

            while (_spawned.Count < s.FactoryCount) _spawned.Add(new List<GameObject>());

            for (var i = 0; i < s.FactoryCount; i++)
            {
                Transform disc = factoriesRoot.GetChild(i);
                foreach (GameObject go in _spawned[i]) Destroy(go);
                _spawned[i].Clear();

                bool isSelected = _sel.HasSelection && !_sel.FromCenter && _sel.SourceIndex == i;

                var discImage = disc.GetComponent<Image>();
                if (discImage)
                {
                    discImage.color = isSelected ? new Color(1f, 1f, 0.5f, 1f) : Color.white;
                }

                foreach (byte col in s.Factories[i])
                {
                    GameObject tile = Instantiate(tilePrefab, disc);
                    tile.name = $"Tile_{TileColorManager.Instance?.GetColorName(col) ?? col.ToString()}";
                    _spawned[i].Add(tile);

                    var img = tile.GetComponent<Image>();
                    if (!img)
                    {
                        img = tile.AddComponent<Image>();
                    }

                    if (img && TileColorManager.Instance)
                    {
                        TileColorManager.Instance.ApplyTileVisuals(img, col);

                        if (isSelected && col == _sel.Color)
                        {
                            img.color = new Color(1.5f, 1.5f, 1.5f, 1f);
                        }
                    }

                    int captureIdx = i;
                    byte captureColor = col;
                    var btn = tile.GetComponent<Button>();
                    if (!btn)
                    {
                        continue;
                    }
                    btn.interactable = myTurn;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => _sel.SelectFromFactory(captureIdx, captureColor));
                }
            }
        }

        float CalculateFactorySize()
        {
            if (!factoriesRoot) return 120f;

            var parent = factoriesRoot.parent as RectTransform;
            if (!parent) return 120f;

            Rect parentRect = parent.rect;
            float minDimension = Mathf.Min(parentRect.width, parentRect.height);
            return 0.2f * minDimension;
        }

        void UpdateExistingFactorySizes(float factorySize)
        {
            float tileSize = factorySize * 0.35f;
            float spacing = factorySize * 0.05f;
            int padding = Mathf.RoundToInt(factorySize * 0.08f);

            for (var i = 0; i < factoriesRoot.childCount; i++)
            {
                Transform child = factoriesRoot.GetChild(i);
                var rt = child.GetComponent<RectTransform>();
                if (!rt)
                {
                    continue;
                }
                rt.sizeDelta = new Vector2(factorySize, factorySize);

                var gridLayout = child.GetComponent<GridLayoutGroup>();
                if (!gridLayout)
                {
                    continue;
                }
                gridLayout.cellSize = new Vector2(tileSize, tileSize);
                gridLayout.spacing = new Vector2(spacing, spacing);
                gridLayout.padding = new RectOffset(padding, padding, padding, padding);
            }
        }
    }
}
