using UnityEngine;

namespace UI.Animation
{
    public class Hover : MonoBehaviour
    {
        public float amplitude = 0.25f;
        public float frequency = 1f;

        Vector3 _startPos;

        void Start()
        {
            _startPos = transform.localPosition;
        }

        void Update()
        {
            float offset = Mathf.Sin(Time.time * frequency) * amplitude;
            transform.localPosition = _startPos + Vector3.up * offset;
        }
    }
}
