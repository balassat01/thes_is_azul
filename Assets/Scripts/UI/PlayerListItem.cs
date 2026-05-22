using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class PlayerListItem : MonoBehaviour
    {
        [SerializeField] Image colorTileImage;
        [SerializeField] TextMeshProUGUI playerNameText;
        [SerializeField] Image backgroundImage;
        [SerializeField] Image hostCrownImage;

        [SerializeField] Color normalBackgroundColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] Color hostBackgroundColor = new Color(0.3f, 0.25f, 0.2f);

        public void SetPlayerInfo(string playerName, bool isHost, byte colorIndex)
        {
            if (playerNameText)
            {
                playerNameText.text = playerName;
            }

            if (backgroundImage)
            {
                backgroundImage.color = isHost ? hostBackgroundColor : normalBackgroundColor;
            }

            if (hostCrownImage)
            {
                hostCrownImage.gameObject.SetActive(isHost);
            }

            if (colorTileImage && TileColorManager.Instance)
            {
                TileColorManager.Instance.ApplyTileVisuals(colorTileImage, colorIndex);
            }
        }
    }
}
