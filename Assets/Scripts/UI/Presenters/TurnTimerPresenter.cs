using Localization;
using Netcode;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Presenters
{
    public sealed class TurnTimerPresenter : MonoBehaviour
    {
        [Header("Timer Display")]
        [Tooltip("Text component showing remaining time")]
        [SerializeField] TextMeshProUGUI timerText;
        [Tooltip("Optional: Image component for timer background/bar")]
        [SerializeField] Image timerBackground;
        [Tooltip("Optional: Radial fill image for circular timer")]
        [SerializeField] Image radialTimer;

        [Header("Color Thresholds")]
        [Tooltip("Color when plenty of time remains")]
        [SerializeField] Color safeColor = new Color(0.2f, 0.8f, 0.2f);
        [Tooltip("Color when time is getting low")]
        [SerializeField] Color warningColor = new Color(1f, 0.8f, 0f);
        [Tooltip("Color when time is critically low")]
        [SerializeField] Color dangerColor = new Color(1f, 0.2f, 0.2f);

        [Header("Warning Thresholds (seconds)")]
        [Tooltip("Show warning color below this threshold")]
        [SerializeField] float warningThreshold = 10f;
        [Tooltip("Show danger color below this threshold")]
        [SerializeField] float dangerThreshold = 5f;

        [Header("Visual Effects")]
        [Tooltip("Enable pulsing effect when time is low")]
        [SerializeField] bool enablePulsing = true;
        [Tooltip("Enable sound alert when entering danger zone (requires AudioSource)")]
        [SerializeField] bool enableSoundAlert;
        [SerializeField] AudioSource audioSource;

        double _currentDeadline;
        int _turnSeconds;
        bool _dangerSoundPlayed;
        float _pulseTime;

        void Start()
        {
            NetEvents.OnSnapshot += Render;

            if (PhotonNetwork.CurrentRoom?.CustomProperties != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("TurnTimer", out object timer))
            {
                _turnSeconds = (int)timer;
            }

            HideTimer();
        }

        void OnDestroy()
        {
            NetEvents.OnSnapshot -= Render;
        }

        void Update()
        {
            if (_currentDeadline <= 0 || PhotonNetwork.Time >= _currentDeadline)
            {
                return;
            }

            UpdateTimerDisplay();
        }

        void Render(RoomPropsCodec.Snapshot snapshot)
        {
            _currentDeadline = snapshot.TurnDeadline;

            _dangerSoundPlayed = false;

            UpdateTimerDisplay();
        }

        void UpdateTimerDisplay()
        {
            if (_turnSeconds <= 0)
            {
                HideTimer();
                return;
            }

            if (_currentDeadline <= 0)
            {
                HideTimer();
                return;
            }

            double timeRemaining = _currentDeadline - PhotonNetwork.Time;

            if (timeRemaining <= 0)
            {
                ShowExpired();
                return;
            }

            ShowTimer(timeRemaining);
        }

        void ShowTimer(double timeRemaining)
        {
            string timeString;
            if (timeRemaining >= 60)
            {
                var minutes = (int)(timeRemaining / 60);
                var seconds = (int)(timeRemaining % 60);
                timeString = $"{minutes:D2}:{seconds:D2}";
            }
            else
            {
                timeString = $"{timeRemaining:F1}s";
            }

            if (timerText)
            {
                timerText.text = timeString;
                timerText.enabled = true;
            }

            Color currentColor = GetColorForTime((float)timeRemaining);

            if (enablePulsing && timeRemaining < dangerThreshold)
            {
                _pulseTime += Time.deltaTime * 3f;
                float pulse = (Mathf.Sin(_pulseTime) + 1f) / 2f;
                currentColor = Color.Lerp(dangerColor, Color.white, pulse * 0.3f);
            }

            if (timerText)
            {
                timerText.color = currentColor;
            }

            if (timerBackground)
            {
                timerBackground.color = currentColor;
                timerBackground.enabled = true;
            }

            if (radialTimer)
            {
                float turnDuration = _turnSeconds > 0 ? _turnSeconds : 30f;
                float fillAmount = Mathf.Clamp01((float)(timeRemaining / turnDuration));
                radialTimer.fillAmount = fillAmount;
                radialTimer.color = currentColor;
                radialTimer.enabled = true;
            }

            if (!enableSoundAlert || _dangerSoundPlayed || !(timeRemaining < dangerThreshold) || !audioSource || !audioSource.clip)
            {
                return;
            }
            audioSource.Play();
            _dangerSoundPlayed = true;
        }

        void ShowExpired()
        {
            if (timerText)
            {
                timerText.text = LocalizationManager.Instance.GetText("game.timeUp");
                timerText.color = dangerColor;
                timerText.enabled = true;
            }

            if (timerBackground)
            {
                timerBackground.color = dangerColor;
                timerBackground.enabled = true;
            }

            if (radialTimer)
            {
                radialTimer.fillAmount = 0f;
                radialTimer.color = dangerColor;
                radialTimer.enabled = true;
            }
        }

        void HideTimer()
        {
            if (timerText)
            {
                timerText.enabled = false;
            }

            if (timerBackground)
            {
                timerBackground.enabled = false;
            }

            if (radialTimer)
            {
                radialTimer.enabled = false;
            }
        }

        Color GetColorForTime(float seconds)
        {
            if (seconds < dangerThreshold)
            {
                return dangerColor;
            }
            if (!(seconds < warningThreshold))
            {
                return safeColor;
            }

            float range = warningThreshold - dangerThreshold;
            if (range <= 0.01f) return warningColor;

            float t = (seconds - dangerThreshold) / range;
            return Color.Lerp(dangerColor, warningColor, t);
        }
    }
}
