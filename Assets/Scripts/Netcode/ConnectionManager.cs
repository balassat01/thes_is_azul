using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Netcode
{
    public sealed class ConnectionManager : MonoBehaviourPunCallbacks
    {
        [Header("Photon Settings")]
        [SerializeField] string gameVersion = "1.0";
        [SerializeField] bool autoConnect;

        [Header("Scene Management")]
        [SerializeField] bool autoSyncScene;

        void Start()
        {
            PhotonNetwork.GameVersion = gameVersion;
            PhotonNetwork.AutomaticallySyncScene = autoSyncScene;

            if (AzulNet.Instance == null)
            {
                var go = new GameObject("AzulNet");
                go.AddComponent<AzulNet>();
            }

            if (!autoConnect || PhotonNetwork.IsConnected) return;
            Connect();
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnected) return;
            PhotonNetwork.ConnectUsingSettings();
        }

        public void Disconnect()
        {
            if (!PhotonNetwork.IsConnected) return;
            PhotonNetwork.Disconnect();
        }

        #region Photon Callbacks

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogWarning($"[ConnectionManager] Disconnected: {cause}");
        }

        #endregion

        #region Public Utilities

        public static string GetConnectionStatus()
        {
            if (!PhotonNetwork.IsConnected)
                return "Disconnected";
            if (PhotonNetwork.InRoom)
                return $"In Room: {PhotonNetwork.CurrentRoom.Name}";
            if (PhotonNetwork.InLobby)
                return "In Lobby";
            return "Connected";
        }

        public static bool IsReadyForGame()
        {
            return PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom;
        }

        #endregion
    }
}
