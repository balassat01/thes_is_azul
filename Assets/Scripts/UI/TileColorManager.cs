using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public sealed class TileColorManager : MonoBehaviour
    {
        public static TileColorManager Instance { get; private set; }

        [Header("Tile Sprites (Master Chocolatier)")]
        [Tooltip("Index 0: Blue chocolate tile")]
        [SerializeField] Sprite blueChocolateTile;
        [Tooltip("Index 1: Brown chocolate tile")]
        [SerializeField] Sprite brownChocolateTile;
        [Tooltip("Index 2: White chocolate tile")]
        [SerializeField] Sprite whiteChocolateTile;
        [Tooltip("Index 3: Black chocolate tile")]
        [SerializeField] Sprite blackChocolateTile;
        [Tooltip("Index 4: Red chocolate tile")]
        [SerializeField] Sprite redChocolateTile;

        [Header("Special Tokens")]
        [Tooltip("First player token sprite")]
        [SerializeField] Sprite firstPlayerTokenSprite;

        [Header("Fallback Colors (if sprites not assigned)")]
        [Tooltip("Only used if blueChocolateTile sprite is not assigned")]
        [SerializeField] Color blueColor = new Color(0.26f, 0.56f, 0.61f);
        [Tooltip("Only used if brownChocolateTile sprite is not assigned")]
        [SerializeField] Color brownColor = new Color(0.51f, 0.36f, 0.2f);
        [Tooltip("Only used if whiteChocolateTile sprite is not assigned")]
        [SerializeField] Color whiteColor = new Color(0.98f, 0.9f, 0.76f);
        [Tooltip("Only used if blackChocolateTile sprite is not assigned")]
        [SerializeField] Color blackColor = new Color(0.26f, 0.13f, 0.04f);
        [Tooltip("Only used if redChocolateTile sprite is not assigned")]
        [SerializeField] Color redColor = new Color(0.55f, 0.09f, 0.23f);
        [Tooltip("Only used if firstPlayerTokenSprite is not assigned")]
        [SerializeField] Color tokenColor = new Color(0.15f, 0.94f, 0.75f);

        [Header("UI Settings")]
        [SerializeField] float patternLineTileSize = 50f;
        [SerializeField] float floorLineTileSize = 50f;

        [Header("Box Grid Placeholders")]
        [Tooltip("Alpha value for empty box cells showing where tiles should go (0.0-1.0)")]
        [SerializeField] float emptyBoxCellAlpha = 0.25f;
        [Tooltip("Whether to show colored placeholders for empty box cells")]
        [SerializeField] bool showBoxPlaceholders = true;

        Sprite[] _tileSprites;
        Color[] _tileColors;

        void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _tileSprites = new[]
            {
                blueChocolateTile,
                brownChocolateTile,
                whiteChocolateTile,
                blackChocolateTile,
                redChocolateTile
            };

            _tileColors = new[]
            {
                blueColor,
                brownColor,
                whiteColor,
                blackColor,
                redColor
            };
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public Sprite GetTileSprite(byte colorIndex)
        {
            if (colorIndex < 5)
            {
                return _tileSprites[colorIndex];
            }

            Debug.LogWarning($"[TileColorManager] Invalid color index: {colorIndex}");
            return null;
        }

        public Color GetTileColor(byte colorIndex)
        {
            if (colorIndex < 5)
            {
                return _tileColors[colorIndex];
            }

            Debug.LogWarning($"[TileColorManager] Invalid color index: {colorIndex}");
            return Color.magenta;
        }

        public Sprite GetFirstPlayerTokenSprite()
        {
            return firstPlayerTokenSprite;
        }

        Color GetFirstPlayerTokenColor()
        {
            return tokenColor;
        }

        public string GetColorName(byte colorIndex)
        {
            return colorIndex switch
            {
                0 => "Blue",
                1 => "Brown",
                2 => "White",
                3 => "Black",
                4 => "Red",
                _ => "Unknown"
            };
        }

        public void ApplyTileVisuals(Image image, byte colorIndex)
        {
            if (!image) return;

            Sprite sprite = GetTileSprite(colorIndex);
            if (sprite)
            {
                image.sprite = sprite;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = GetTileColor(colorIndex);
            }
        }

        public void ApplyTokenVisuals(Image image)
        {
            if (!image) return;

            if (firstPlayerTokenSprite)
            {
                image.sprite = firstPlayerTokenSprite;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = GetFirstPlayerTokenColor();
            }
        }

        public float GetPatternLineTileSize()
        {
            return patternLineTileSize;
        }

        public float GetFloorLineTileSize()
        {
            return floorLineTileSize;
        }

        public bool ShowBoxPlaceholders => showBoxPlaceholders;

        public void ApplyBoxPlaceholderVisuals(Image image, byte colorIndex)
        {
            if (!image) return;

            if (!showBoxPlaceholders)
            {
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0.1f);
                return;
            }

            Sprite sprite = GetTileSprite(colorIndex);
            if (sprite)
            {
                image.sprite = sprite;
                image.color = new Color(1f, 1f, 1f, emptyBoxCellAlpha);
            }
            else
            {
                Color placeholderColor = GetTileColor(colorIndex);
                placeholderColor.a = emptyBoxCellAlpha;
                image.sprite = null;
                image.color = placeholderColor;
            }
        }
    }
}
