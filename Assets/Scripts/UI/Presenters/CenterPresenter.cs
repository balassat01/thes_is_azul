using Gameplay;
using Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class CenterPresenter : MonoBehaviour
    {
        [SerializeField] Transform centerRoot;
        [SerializeField] GameObject tilePrefab;
        [SerializeField] GameObject tokenIcon;

        DraftSelectionModel _sel;

        public void Init(DraftSelectionModel sel) { _sel = sel; }

        public void Render(RoomPropsCodec.Snapshot s, bool myTurn)
        {
            bool centerSelected = _sel.HasSelection && _sel.FromCenter;

            tokenIcon.SetActive(s.FirstInCenter);
            if (s.FirstInCenter && TileColorManager.Instance)
            {
                var tokenImage = tokenIcon.GetComponent<Image>();
                if (tokenImage)
                {
                    TileColorManager.Instance.ApplyTokenVisuals(tokenImage);

                    if (centerSelected)
                    {
                        tokenImage.color = new Color(1.5f, 1.5f, 1.5f, 1f);
                    }
                }
            }

            foreach (Transform child in centerRoot) Destroy(child.gameObject);

            foreach (byte col in s.Center)
            {
                GameObject go = Instantiate(tilePrefab, centerRoot);
                go.name = $"Tile_{TileColorManager.Instance?.GetColorName(col) ?? col.ToString()}";

                var img = go.GetComponent<Image>();
                if (!img)
                {
                    img = go.AddComponent<Image>();
                }

                if (img && TileColorManager.Instance)
                {
                    TileColorManager.Instance.ApplyTileVisuals(img, col);

                    if (centerSelected && col == _sel.Color)
                    {
                        img.color = new Color(1.5f, 1.5f, 1.5f, 1f);
                    }
                }

                var btn = go.GetComponent<Button>();
                if (!btn)
                {
                    continue;
                }
                byte capture = col;
                btn.interactable = myTurn;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => _sel.SelectFromCenter(capture));
            }
        }
    }
}
