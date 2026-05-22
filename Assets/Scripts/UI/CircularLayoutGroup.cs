using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [AddComponentMenu("Layout/Circular Layout Group")]
    public class CircularLayoutGroup : LayoutGroup
    {
        [Header("Circle Settings")]
        [SerializeField, Tooltip("Use dynamic radius based on parent size")]
        bool useDynamicRadius = false;

        [SerializeField, Tooltip("Radius of the circle in pixels (used when useDynamicRadius is false)")]
        float radius = 100f;

        [SerializeField, Tooltip("Radius scale multiplier when using dynamic radius (e.g., 0.8 means 80% of available space)")]
        float radiusScale = 0.8f;

        [SerializeField, Tooltip("Starting angle in degrees (0 = right, 90 = up, 180 = left, 270 = down)")]
        float startAngle = 0f;

        [SerializeField, Tooltip("Whether to distribute children clockwise or counter-clockwise")]
        bool clockwise = true;

        [SerializeField, Tooltip("Total arc angle to distribute children across (360 = full circle)")]
        float arcAngle = 360f;

        public bool UseDynamicRadius
        {
            get => useDynamicRadius;
            set { useDynamicRadius = value; SetDirty(); }
        }

        public float Radius
        {
            get => radius;
            set { radius = value; SetDirty(); }
        }

        public float RadiusScale
        {
            get => radiusScale;
            set { radiusScale = Mathf.Clamp01(value); SetDirty(); }
        }

        public float StartAngle
        {
            get => startAngle;
            set { startAngle = value; SetDirty(); }
        }

        public bool Clockwise
        {
            get => clockwise;
            set { clockwise = value; SetDirty(); }
        }

        public float ArcAngle
        {
            get => arcAngle;
            set { arcAngle = Mathf.Clamp(value, 0f, 360f); SetDirty(); }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalculateCircularLayout();
        }

        public override void CalculateLayoutInputVertical()
        {

        }

        public override void SetLayoutHorizontal()
        {
            SetCircularPositions();
        }

        public override void SetLayoutVertical()
        {

        }

        static void CalculateCircularLayout()
        {

        }

        float CalculateDynamicRadius()
        {
            Rect rect = rectTransform.rect;
            float minHalfDimension = Mathf.Min(rect.width / 2f, rect.height / 2f);
            return radiusScale * minHalfDimension;
        }

        void SetCircularPositions()
        {
            var childCount = 0;

            for (var i = 0; i < rectTransform.childCount; i++)
            {
                var child = rectTransform.GetChild(i) as RectTransform;
                if (child && child.gameObject.activeInHierarchy)
                    childCount++;
            }

            if (childCount == 0) return;

            float effectiveRadius = useDynamicRadius ? CalculateDynamicRadius() : radius;

            Rect rect = rectTransform.rect;
            Vector2 pivot = rectTransform.pivot;

            float centerX = rect.width * (0.5f - pivot.x);
            float centerY = rect.height * (0.5f - pivot.y);

            float angleBetween = childCount > 1 ? arcAngle / (childCount - 1) : 0f;

            if (Mathf.Approximately(arcAngle, 360f))
            {
                angleBetween = arcAngle / childCount;
            }

            var childIndex = 0;

            for (var i = 0; i < rectTransform.childCount; i++)
            {
                var child = rectTransform.GetChild(i) as RectTransform;

                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                child.anchorMin = new Vector2(0.5f, 0.5f);
                child.anchorMax = new Vector2(0.5f, 0.5f);
                child.pivot = new Vector2(0.5f, 0.5f);

                float angle = startAngle + (angleBetween * childIndex);

                if (!clockwise)
                    angle = startAngle - (angleBetween * childIndex);

                float angleRad = angle * Mathf.Deg2Rad;

                float x = centerX + (Mathf.Cos(angleRad) * effectiveRadius);
                float y = centerY + (Mathf.Sin(angleRad) * effectiveRadius);

                child.anchoredPosition = new Vector2(x, y);

                childIndex++;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            arcAngle = Mathf.Clamp(arcAngle, 0f, 360f);
            radiusScale = Mathf.Clamp01(radiusScale);
            SetDirty();
        }
#endif
    }
}
