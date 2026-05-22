using System;
using System.Linq;
using Core.Domain;
using Core.Domain.Rules;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Random = UnityEngine.Random;

namespace Netcode
{
    public sealed class AzulNet : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static AzulNet Instance { get; private set; }

        GameState State { get; set; }
        public event Action<RoomPropsCodec.Snapshot> SnapshotUpdated;

        IBoxRule _boxRule;
        RNG _rng;
        int _version;
        int _cachedActiveActor = -1;

        void Awake()
        {
            if (Instance) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public override void OnEnable()
        {
            base.OnEnable();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        void Start()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "GameScene") return;

            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogError("[AzulNet] GameScene loaded but not in a room!");
                return;
            }

            PhotonNetwork.AddCallbackTarget(this);

            switch (PhotonNetwork.IsMasterClient)
            {
                case true when ShouldInitializeGame():
                    InitializeStateOnMaster();
                    PublishSnapshot();
                    TriggerLocalSnapshotUpdate();
                    break;
                case true when State != null:
                    TriggerLocalSnapshotUpdate();
                    break;
                case true:
                    Debug.LogWarning("[AzulNet] Master client: State is null, waiting for GameStarted property...");
                    break;
                case false:
                    OnRoomPropertiesUpdate(PhotonNetwork.CurrentRoom.CustomProperties);
                    break;
            }
        }

        bool ShouldInitializeGame()
        {
            if (PhotonNetwork.CurrentRoom?.CustomProperties != null
                && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("GameStarted", out object started)
                && started is true)
            {

                return State == null || State.phase == Phase.GameOver;
            }
            return false;
        }

        public override void OnJoinedRoom()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnLeftRoom()
        {
            Debug.LogWarning("[AzulNet] OnLeftRoom callback fired! Player left the room.");
            PhotonNetwork.RemoveCallbackTarget(this);
            Debug.LogWarning($"[AzulNet] OnLeftRoom: scene='{SceneManager.GetActiveScene().name}', IsMasterClient={PhotonNetwork.IsMasterClient}");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogError($"[AzulNet] OnDisconnected from Photon. Cause={cause}, scene='{SceneManager.GetActiveScene().name}', IsMasterClient={PhotonNetwork.IsMasterClient}, InRoom={PhotonNetwork.InRoom}");
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
            {
                Screen.fullScreen = !Screen.fullScreen;
            }

            if (!PhotonNetwork.IsMasterClient || State == null) return;
            if (State.config.turnSeconds <= 0 || !(State.turnDeadline > 0) || !(PhotonNetwork.Time >= State.turnDeadline)) return;

            if (State.phase == Phase.FactoryOffer)
            {
                Debug.LogWarning($"[AzulNet] Turn timer expired for Actor {State.activeActor}. Auto-skipping turn.");
                State.turnDeadline = 0;
                AdvanceToNextActivePlayer();
                _version++;
                PublishSnapshot();
                TriggerLocalSnapshotUpdate();
            }
            else if (State.phase == Phase.BoxTiling && State.config.useGrayBox)
            {
                Debug.LogWarning($"[AzulNet] Gray box placement timer expired for Actor {State.activeActor}. Auto-placing.");
                State.turnDeadline = 0;
                AutoCompleteBoxTiling();
                _version++;
                PublishSnapshot();
                TriggerLocalSnapshotUpdate();
            }
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (PhotonNetwork.CurrentRoom == null) return;

            Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
            if (roomProps == null || roomProps.Count == 0) return;

            if (!roomProps.ContainsKey("GameStarted") || !(roomProps["GameStarted"] is bool started) || !started)
                return;

            if (PhotonNetwork.IsMasterClient && State == null)
            {
                InitializeStateOnMaster();
                PublishSnapshot();
                TriggerLocalSnapshotUpdate();
                return;
            }

            if (!roomProps.ContainsKey(R.Version))
            {
                Debug.LogWarning("[AzulNet] GameStarted=true but snapshot not yet published by master.");
                return;
            }

            try
            {
                RoomPropsCodec.Snapshot snap = RoomPropsCodec.Decode(roomProps);
                _cachedActiveActor = snap.ActiveActor;
                SnapshotUpdated?.Invoke(snap);
                NetEvents.RaiseSnapshotUpdate(snap);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AzulNet] Snapshot decode failed: {e}");
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber != newMasterClient.ActorNumber) return;
            Debug.LogWarning("[AzulNet] Became new master client. Reconstructing GameState.");
            ReconstructStateFromRoomProperties();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.IsMasterClient || State == null) return;

            Debug.LogWarning($"[AzulNet] Player {otherPlayer.NickName} (Actor {otherPlayer.ActorNumber}) disconnected");

            if (PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[AzulNet] CurrentRoom is null in OnPlayerLeftRoom");
                return;
            }

            int remainingPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
            if (remainingPlayers < 2)
            {
                Debug.LogWarning("[AzulNet] Too few players remaining. Ending game gracefully.");
                EndGameGracefully("Too few players");
                return;
            }

            if (State.activeActor != otherPlayer.ActorNumber)
            {
                return;
            }

            Debug.LogWarning("[AzulNet] Active player disconnected. Skipping their turn.");
            AdvanceToNextActivePlayer();
            PublishSnapshot();
            TriggerLocalSnapshotUpdate();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient || State == null) return;

            PlayerState ps = State.players.FirstOrDefault(p => p.actorNumber == newPlayer.ActorNumber);
            if (ps != null)
            {

                ps.nick = newPlayer.NickName ?? $"P{newPlayer.ActorNumber}";
                PublishSnapshot();
            }
        }

        void AdvanceToNextActivePlayer()
        {
            if (State?.players == null) return;

            int currentIndex = -1;
            for (var i = 0; i < State.players.Length; i++)
            {
                if (State.players[i].actorNumber != State.activeActor)
                {
                    continue;
                }

                currentIndex = i;
                break;
            }

            if (currentIndex < 0) return;

            if (PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[AzulNet] CurrentRoom is null in DetermineNextPlayer");
                return;
            }

            foreach (PlayerState unused in State.players)
            {
                currentIndex = (currentIndex + 1) % State.players.Length;
                int candidateActor = State.players[currentIndex].actorNumber;

                if (!PhotonNetwork.CurrentRoom.Players.ContainsKey(candidateActor))
                {
                    continue;
                }
                State.activeActor = candidateActor;
                SetTurnDeadline();
                return;
            }

            Debug.LogError("[AzulNet] Could not find valid active player!");
            EndGameGracefully("No valid players");
        }

        void EndGameGracefully(string reason)
        {
            if (State == null) return;

            Debug.LogWarning($"[AzulNet] Ending game gracefully: {reason}");
            State.phase = Phase.GameOver;
            State.endTriggered = true;
            PublishSnapshot();
        }

        void ReconstructStateFromRoomProperties()
        {
            if (PhotonNetwork.CurrentRoom == null) { Debug.LogError("[AzulNet] Cannot reconstruct: CurrentRoom is null"); return; }

            Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
            if (roomProps == null || roomProps.Count == 0) { Debug.LogError("[AzulNet] Cannot reconstruct: room properties empty"); return; }

            if (!roomProps.ContainsKey("GameStarted") || !(roomProps["GameStarted"] is bool started) || !started)
            {
                Debug.LogWarning("[AzulNet] Cannot reconstruct: game not started");
                return;
            }
            if (!roomProps.ContainsKey(R.Version))
            {
                Debug.LogWarning("[AzulNet] Cannot reconstruct: snapshot not yet published");
                return;
            }

            try
            {
                RoomPropsCodec.Snapshot snap = RoomPropsCodec.Decode(roomProps);

                State = new GameState
                {
                    config =
                    {
                        playerCount = snap.Players.Length,
                        seed = (int)roomProps[R.Seed],
                        useGrayBox = ((byte)roomProps[R.Mode] & 1) != 0
                    },
                    phase = snap.Phase,
                    round = snap.Round,
                    activeActor = snap.ActiveActor,
                    startPlayerActor = snap.StartActor,
                    firstPlayerTokenInCenter = snap.FirstInCenter,
                    Factories = snap.Factories,
                    center = snap.Center,
                    bag = snap.Bag.Select(b => (int)b).ToArray(),
                    lid = snap.Lid.Select(b => (int)b).ToArray(),
                    players = new PlayerState[snap.Players.Length]
                };

                int[] baseOrderInt = snap.BaseOrder != null
                    ? Array.ConvertAll(snap.BaseOrder, b => (int)b)
                    : new[] { 0, 1, 2, 3, 4 };

                for (var i = 0; i < snap.Players.Length; i++)
                {
                    var ps = new PlayerState
                    {
                        actorNumber   = snap.Actors[i],
                        nick          = PhotonNetwork.CurrentRoom.Players.TryGetValue(snap.Actors[i], out Player p) ? p.NickName : $"P{snap.Actors[i]}",
                        visualIdColor = snap.IdColors[i],
                        score         = snap.Players[i].Score
                    };

                    for (var r = 0; r < 5; r++)
                    {
                        byte col = snap.Players[i].PatternColors[r];
                        ps.patternColors[r] = col == 255 ? (sbyte)-1 : (sbyte)col;
                        ps.patternCounts[r] = snap.Players[i].PatternCounts[r];
                    }

                    for (var r = 0; r < 5; r++)
                    {
                        for (var c = 0; c < 5; c++)
                        {
                            byte storedColor = snap.Players[i].BoxColors[r * 5 + c];
                            if (storedColor != 255)
                            {
                                ps.Box[r, c] = (sbyte)storedColor;
                            }
                            else if ((snap.Players[i].BoxBytes[r] & (1 << c)) != 0)
                            {

                                ps.Box[r, c] = (sbyte)StandardBoxMap.ColorAtCell(baseOrderInt, r, c);
                            }
                            else
                            {
                                ps.Box[r, c] = -1;
                            }
                        }
                    }

                    ps.floor.Clear();
                    for (var j = 0; j < 7; j++)
                    {
                        byte tile = snap.Players[i].Floor[j];
                        if (tile != 255) ps.floor.Add(tile);
                    }

                    State.players[i] = ps;
                }

                _boxRule = State.config.useGrayBox
                    ? (IBoxRule)new GrayBoxRule()
                    : new StandardBoxRule(new[] { 0, 1, 2, 3, 4 });

                _rng = roomProps.ContainsKey(R.RngState)
                    ? RNG.FromSerializedState((long)roomProps[R.RngState])
                    : new RNG(State.config.seed);

                _version = (int)roomProps[R.Version];

                Debug.LogWarning($"[AzulNet] GameState reconstructed. Phase={State.phase}, Round={State.round}, ActiveActor={State.activeActor}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AzulNet] Failed to reconstruct GameState: {e}");
            }
        }

        void InitializeStateOnMaster()
        {
            State = new GameState();

            (int ActorNumber, string Nick, byte PlayerColor)[] seats = PhotonNetwork.PlayerList
                .OrderBy(p => p.ActorNumber)
                .Select(p =>
                {
                    byte color = 0;
                    if (p.CustomProperties.TryGetValue("PlayerColor", out object colorObj) && colorObj is byte b)
                        color = b;
                    else
                        Debug.LogWarning($"[AzulNet] Player {p.NickName} (Actor {p.ActorNumber}) has no PlayerColor, defaulting to blue");
                    return (p.ActorNumber, p.NickName ?? $"P{p.ActorNumber}", color);
                })
                .ToArray();

            int seed = seats.Aggregate(0, (acc, s) => acc ^ s.ActorNumber) ^ Random.Range(int.MinValue, int.MaxValue);

            var useGrayBox = false;
            var turnTimer  = 0;
            if (PhotonNetwork.CurrentRoom?.CustomProperties != null)
            {
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("UseGrayBox", out object gray))
                    useGrayBox = (bool)gray;
                if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("TurnTimer", out object timer))
                    turnTimer = (int)timer;
            }

            AzulEngine.Initialize(State, seats, useGrayBox, seed);
            State.config.turnSeconds = turnTimer;

            _boxRule = State.config.useGrayBox
                ? (IBoxRule)new GrayBoxRule()
                : new StandardBoxRule(new[] { 0, 1, 2, 3, 4 });
            _rng = new RNG(seed);

            AzulEngine.RefillAndBeginOffer(State, _rng);
            _version = 1;
            SetTurnDeadline();

            Debug.LogWarning($"[AzulNet] Game initialized: Players={seats.Length}, GrayBox={useGrayBox}, Timer={turnTimer}s");
        }

        void SetTurnDeadline()
        {
            if (State == null || State.config.turnSeconds <= 0)
            {
                if (State != null)
                    State.turnDeadline = 0;
                return;
            }

            State.turnDeadline = PhotonNetwork.Time + State.config.turnSeconds;
        }

        public void SendPlaceInBox(int patternRow, int column)
        {
            if (PhotonNetwork.CurrentRoom == null) { Debug.LogError("[AzulNet] Cannot send CmdPlaceInBox: not in room"); return; }

            if (PhotonNetwork.IsMasterClient)
            {
                var cmd = new PlaceInBoxCmd(PhotonNetwork.LocalPlayer.ActorNumber, patternRow, column);
                if (TryApplyPlaceInBox(cmd, out string err))
                {
                    PublishSnapshot();
                    TriggerLocalSnapshotUpdate();
                }
                else
                    Debug.LogWarning($"[AzulNet] CmdPlaceInBox rejected (master local): {err}");
                return;
            }

            object[] payload = { PhotonNetwork.LocalPlayer.ActorNumber, patternRow, column, NextCmdSeq() };
            PhotonNetwork.RaiseEvent(Ev.CmdPlaceInBox, payload,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
        }

        public void SendTakeTiles(int sourceIndex, byte color, int patternRow, bool fromCenter)
        {
            if (PhotonNetwork.CurrentRoom == null) { Debug.LogError("[AzulNet] Cannot send command: not in room"); return; }

            if (PhotonNetwork.LocalPlayer.ActorNumber != _cachedActiveActor)
            {
                Debug.LogWarning($"[AzulNet] SendTakeTiles BLOCKED by guard: local={PhotonNetwork.LocalPlayer.ActorNumber}, cachedActive={_cachedActiveActor}");
                return;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                var cmd = new TakeTilesCmd(PhotonNetwork.LocalPlayer.ActorNumber,
                    fromCenter ? DraftSource.Center : DraftSource.Factory,
                    sourceIndex, color, patternRow);
                if (TryApply(cmd, out string err))
                {
                    PublishSnapshot();
                    TriggerLocalSnapshotUpdate();
                }
                else
                    Debug.LogWarning($"[AzulNet] CmdTakeTiles rejected (master local): {err}");
                return;
            }

            object[] payload = { PhotonNetwork.LocalPlayer.ActorNumber, (byte)(fromCenter ? 1 : 0), sourceIndex, color, patternRow, NextCmdSeq() };
            PhotonNetwork.RaiseEvent(Ev.CmdTakeTiles, payload, new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            switch (photonEvent.Code)
            {
                case Ev.CmdTakeTiles:
                {
                    var data = (object[])photonEvent.CustomData;
                    var actor      = (int)data[0];
                    bool center    = ((byte)data[1]) == 1;
                    var srcIndex   = (int)data[2];
                    var color      = (byte)data[3];
                    var patternRow = (int)data[4];

                    var cmd = new TakeTilesCmd(actor, center ? DraftSource.Center : DraftSource.Factory, srcIndex, color, patternRow);
                    if (TryApply(cmd, out string err))
                    {
                        PublishSnapshot();

                        TriggerLocalSnapshotUpdate();
                    }
                    else
                        Debug.LogWarning($"[AzulNet] CmdTakeTiles rejected: {err}");
                    break;
                }

                case Ev.CmdPlaceInBox:
                {
                    var data = (object[])photonEvent.CustomData;
                    var actor      = (int)data[0];
                    var patternRow = (int)data[1];
                    var column     = (int)data[2];

                    var cmd = new PlaceInBoxCmd(actor, patternRow, column);
                    if (TryApplyPlaceInBox(cmd, out string err))
                    {
                        PublishSnapshot();
                        TriggerLocalSnapshotUpdate();
                    }
                    else
                        Debug.LogWarning($"[AzulNet] CmdPlaceInBox rejected: {err}");
                    break;
                }
            }
        }

        static readonly int[] DefaultBaseOrder = { 0, 1, 2, 3, 4 };
        static int StandardColor(int r, int c) => StandardBoxMap.ColorAtCell(DefaultBaseOrder, r, c);

        bool TryApply(TakeTilesCmd cmd, out string error)
        {
            bool ok = AzulEngine.ApplyTakeTiles(State, cmd, _boxRule, out error);
            if (!ok) return false;

            if (State.phase == Phase.BoxTiling)
            {
                if (State.config.useGrayBox)
                {

                    AzulEngine.BeginGrayBoxTiling(State, StandardColor);
                }
                else
                {
                    AzulEngine.ExecuteBoxTiling(State, _boxRule, StandardColor);
                }
                if (State.phase == Phase.Refill)
                    AzulEngine.RefillAndBeginOffer(State, _rng);
            }

            SetTurnDeadline();
            _version++;
            return true;
        }

        bool TryApplyPlaceInBox(PlaceInBoxCmd cmd, out string error)
        {
            bool ok = AzulEngine.ApplyPlaceInBox(State, cmd, StandardColor, out error);
            if (!ok) return false;
            if (State.phase == Phase.Refill)
                AzulEngine.RefillAndBeginOffer(State, _rng);
            SetTurnDeadline();
            _version++;
            return true;
        }

        void AutoCompleteBoxTiling()
        {
            const int safetyLimit = 25;
            for (var attempt = 0; attempt < safetyLimit && State.phase == Phase.BoxTiling; attempt++)
            {
                PlayerState player = State.players.FirstOrDefault(p => p.actorNumber == State.activeActor);
                if (player == null) break;

                var placed = false;
                for (var row = 0; row < 5 && !placed; row++)
                {
                    if (player.patternCounts[row] != row + 1) continue;
                    int color = (int)player.patternColors[row];

                    for (var col = 0; col < 5; col++)
                    {
                        if (player.Box[row, col] >= 0) continue;
                        bool conflict = false;
                        for (var r = 0; r < 5; r++)
                            if (player.Box[r, col] == (sbyte)color) { conflict = true; break; }
                        if (conflict) continue;

                        AzulEngine.ApplyPlaceInBox(State, new PlaceInBoxCmd(State.activeActor, row, col), StandardColor, out _);
                        placed = true;
                        break;
                    }
                }
                if (!placed) break;
            }
            if (State.phase == Phase.Refill)
                AzulEngine.RefillAndBeginOffer(State, _rng);
        }

        void PublishSnapshot()
        {
            if (PhotonNetwork.CurrentRoom == null)
            {
                Debug.LogError("[AzulNet] Cannot publish snapshot: CurrentRoom is null");
                return;
            }
            if (State == null)
            {
                Debug.LogError("[AzulNet] Cannot publish snapshot: State is null");
                return;
            }

            Hashtable gameStateProps = RoomPropsCodec.Encode(State, _version,
                baseOrderForStandard: new byte[] { 0, 1, 2, 3, 4 },
                rngState: _rng?.SerializeState() ?? 0);

            var mergedProps = new Hashtable();
            foreach (var key in PhotonNetwork.CurrentRoom.CustomProperties.Keys)
                mergedProps[key] = PhotonNetwork.CurrentRoom.CustomProperties[key];
            foreach (var key in gameStateProps.Keys)
                mergedProps[key] = gameStateProps[key];

            if (!mergedProps.ContainsKey(R.Version))
                Debug.LogError("[AzulNet] R.Version missing from merged properties!");

            PhotonNetwork.CurrentRoom.SetCustomProperties(mergedProps);
        }

        void TriggerLocalSnapshotUpdate()
        {
            if (State == null) return;
            try
            {
                Hashtable encoded = RoomPropsCodec.Encode(State, _version,
                    baseOrderForStandard: new byte[] { 0, 1, 2, 3, 4 },
                    rngState: _rng?.SerializeState() ?? 0);
                RoomPropsCodec.Snapshot snap = RoomPropsCodec.Decode(encoded);
                _cachedActiveActor = snap.ActiveActor;
                SnapshotUpdated?.Invoke(snap);
                NetEvents.RaiseSnapshotUpdate(snap);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AzulNet] Failed to trigger local snapshot update: {e}");
            }
        }

        public void RequestSnapshotRefresh()
        {
            if (State != null)
                TriggerLocalSnapshotUpdate();
            else if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties != null)
                OnRoomPropertiesUpdate(PhotonNetwork.CurrentRoom.CustomProperties);
        }

        static int NextCmdSeq() => Random.Range(0, int.MaxValue);
    }
}
