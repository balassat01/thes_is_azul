using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ExitGames.Client.Photon;
using Core.Domain;
using Netcode;

namespace AzulTests
{
    /// <summary>
    /// Tests for RoomPropsCodec encode/decode round-trips.
    /// Only the serialization logic is exercised — no Photon networking, no MonoBehaviours.
    /// </summary>
    [TestFixture]
    public class RoomPropsCodecTests
    {
        static readonly byte[] DefaultBaseOrder = { 0, 1, 2, 3, 4 };
        const int Version = 1;

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Encodes and immediately decodes a GameState, returning the Snapshot view.
        /// </summary>
        static RoomPropsCodec.Snapshot RoundTrip(GameState state, long rngState = 0)
        {
            Hashtable encoded = RoomPropsCodec.Encode(state, Version, DefaultBaseOrder, rngState);
            return RoomPropsCodec.Decode(encoded);
        }

        /// <summary>Builds a minimal 2-player GameState at FactoryOffer phase.</summary>
        static GameState MakeReadyState()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            return state;
        }

        // -----------------------------------------------------------------------
        // Phase round-trip
        // -----------------------------------------------------------------------

        [Test]
        [TestCase(Phase.FactoryOffer)]
        [TestCase(Phase.BoxTiling)]
        [TestCase(Phase.Refill)]
        [TestCase(Phase.GameOver)]
        public void Encode_Decode_PhasePreserved(Phase phase)
        {
            var state = MakeReadyState();
            state.phase = phase;

            var snap = RoundTrip(state);

            Assert.AreEqual(phase, snap.Phase);
        }

        // -----------------------------------------------------------------------
        // Scalar field round-trips
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_RoundPreserved()
        {
            var state = MakeReadyState();
            state.round = 5;

            var snap = RoundTrip(state);

            Assert.AreEqual(5, snap.Round);
        }

        [Test]
        public void Encode_Decode_ActiveActorPreserved()
        {
            var state = MakeReadyState();
            state.activeActor = 2;

            var snap = RoundTrip(state);

            Assert.AreEqual(2, snap.ActiveActor);
        }

        [Test]
        public void Encode_Decode_StartActorPreserved()
        {
            var state = MakeReadyState();
            state.startPlayerActor = 2;

            var snap = RoundTrip(state);

            Assert.AreEqual(2, snap.StartActor);
        }

        [Test]
        public void Encode_Decode_FirstInCenterTrue_Preserved()
        {
            var state = MakeReadyState();
            state.firstPlayerTokenInCenter = true;

            var snap = RoundTrip(state);

            Assert.IsTrue(snap.FirstInCenter);
        }

        [Test]
        public void Encode_Decode_FirstInCenterFalse_Preserved()
        {
            var state = MakeReadyState();
            state.firstPlayerTokenInCenter = false;

            var snap = RoundTrip(state);

            Assert.IsFalse(snap.FirstInCenter);
        }

        [Test]
        public void Encode_Decode_IsGrayBoxFalse_Preserved()
        {
            var state = MakeReadyState(); // grayBox = false
            var snap = RoundTrip(state);
            Assert.IsFalse(snap.IsGrayBox);
        }

        [Test]
        public void Encode_Decode_IsGrayBoxTrue_Preserved()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            var snap = RoundTrip(state);
            Assert.IsTrue(snap.IsGrayBox);
        }

        [Test]
        public void Encode_Decode_BaseOrderPreserved()
        {
            var state = MakeReadyState();
            var snap = RoundTrip(state);
            Assert.AreEqual(DefaultBaseOrder, snap.BaseOrder);
        }

        // -----------------------------------------------------------------------
        // Factories round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_FactoryCountPreserved()
        {
            var state = MakeReadyState(); // 2 players → 5 factories
            var snap = RoundTrip(state);
            Assert.AreEqual(5, snap.FactoryCount);
        }

        [Test]
        [TestCase(2, 5)]
        [TestCase(3, 7)]
        [TestCase(4, 9)]
        public void Encode_Decode_FactoryCountForPlayerCount(int playerCount, int expectedFactories)
        {
            var (state, _) = TestHelpers.MakeGame(playerCount);
            var snap = RoundTrip(state);
            Assert.AreEqual(expectedFactories, snap.FactoryCount);
        }

        [Test]
        public void Encode_Decode_FactoryTilesPreserved()
        {
            var state = MakeReadyState();
            // Record factory 0 content before encode
            var expected = state.Factories[0].ToList();

            var snap = RoundTrip(state);

            Assert.AreEqual(expected.Count, snap.Factories[0].Count);
            for (var i = 0; i < expected.Count; i++)
                Assert.AreEqual(expected[i], snap.Factories[0][i]);
        }

        [Test]
        public void Encode_Decode_EmptyFactory_DecodesEmpty()
        {
            var state = MakeReadyState();
            state.Factories[0].Clear(); // empty factory 0

            var snap = RoundTrip(state);

            Assert.AreEqual(0, snap.Factories[0].Count,
                "An empty factory must decode as an empty list, not as sentinel values.");
        }

        [Test]
        public void Encode_Decode_EmptySlotSentinel255_NotIncludedInFactoryList()
        {
            var state = MakeReadyState();
            // Factory with 2 tiles (not 4) — the remaining 2 slots are 255=empty
            state.Factories[1].Clear();
            state.Factories[1].Add(0);
            state.Factories[1].Add(1);

            var snap = RoundTrip(state);

            Assert.AreEqual(2, snap.Factories[1].Count,
                "Sentinel 255 slots must not appear as tiles in the decoded factory list.");
        }

        // -----------------------------------------------------------------------
        // Center round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_CenterPreserved()
        {
            var state = MakeReadyState();
            state.center.Clear();
            state.center.Add(2);
            state.center.Add(3);

            var snap = RoundTrip(state);

            Assert.AreEqual(2, snap.Center.Count);
            Assert.AreEqual(2, snap.Center[0]);
            Assert.AreEqual(3, snap.Center[1]);
        }

        // -----------------------------------------------------------------------
        // Bag / Lid round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_BagPreserved()
        {
            var state = MakeReadyState();
            // After RefillAndBeginOffer, bag has been drawn from
            var bagCopy = (int[])state.bag.Clone();

            var snap = RoundTrip(state);

            for (var c = 0; c < 5; c++)
                Assert.AreEqual(bagCopy[c], snap.Bag[c], $"Bag color {c} mismatch.");
        }

        [Test]
        public void Encode_Decode_LidPreserved()
        {
            var state = MakeReadyState();
            state.lid[2] = 5;
            state.lid[4] = 3;

            var snap = RoundTrip(state);

            Assert.AreEqual(5, snap.Lid[2]);
            Assert.AreEqual(3, snap.Lid[4]);
        }

        // -----------------------------------------------------------------------
        // Player data round-trips
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_ScoresPreserved()
        {
            var state = MakeReadyState();
            state.players[0].score = 17;
            state.players[1].score = 42;

            var snap = RoundTrip(state);

            Assert.AreEqual(17, snap.Players[0].Score);
            Assert.AreEqual(42, snap.Players[1].Score);
        }

        [Test]
        public void Encode_Decode_PatternColorsPreserved()
        {
            var state = MakeReadyState();
            // Set pattern row 2 of player 0 to White
            state.players[0].patternColors[2] = TestHelpers.White;
            state.players[0].patternCounts[2] = 2;

            var snap = RoundTrip(state);

            Assert.AreEqual(TestHelpers.White, snap.Players[0].PatternColors[2]);
            Assert.AreEqual(2, snap.Players[0].PatternCounts[2]);
        }

        [Test]
        public void Encode_Decode_EmptyPatternSlot_EncodesAs255()
        {
            var state = MakeReadyState();
            // Row 0 empty
            state.players[0].patternColors[0] = -1;
            state.players[0].patternCounts[0] = 0;

            Hashtable encoded = RoomPropsCodec.Encode(state, Version, DefaultBaseOrder);
            byte[] plColors = (byte[])encoded[R.PLColors];

            Assert.AreEqual(255, plColors[0], "Empty pattern slot must encode as 255.");
        }

        [Test]
        public void Encode_Decode_EmptyPatternSlot_Preserved()
        {
            var state = MakeReadyState();
            state.players[0].patternColors[0] = -1;

            var snap = RoundTrip(state);

            Assert.AreEqual(255, snap.Players[0].PatternColors[0],
                "Empty pattern slot must decode as 255.");
        }

        // -----------------------------------------------------------------------
        // Floor round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_FloorTilesPreserved()
        {
            var state = MakeReadyState();
            state.players[0].floor.Add(TestHelpers.Blue);
            state.players[0].floor.Add(TestHelpers.Token); // first-player token

            var snap = RoundTrip(state);

            Assert.AreEqual(TestHelpers.Blue, snap.Players[0].Floor[0]);
            Assert.AreEqual(TestHelpers.Token, snap.Players[0].Floor[1]);
        }

        [Test]
        public void Encode_Decode_EmptyFloorSlots_EncodedAs255()
        {
            var state = MakeReadyState();
            state.players[0].floor.Clear();
            state.players[0].floor.Add(TestHelpers.Red); // only 1 tile

            Hashtable encoded = RoomPropsCodec.Encode(state, Version, DefaultBaseOrder);
            byte[] flr = (byte[])encoded[R.Floor];

            // Slots 1-6 must be 255
            for (var j = 1; j < 7; j++)
                Assert.AreEqual(255, flr[0 * 7 + j], $"Floor slot {j} must be 255 when empty.");
        }

        // -----------------------------------------------------------------------
        // BoxColors round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_BoxColorsPreserved()
        {
            var state = MakeReadyState();
            // Place Blue at (0,0) and Red at (2,3) for player 0
            state.players[0].Box[0, 0] = TestHelpers.Blue;
            state.players[0].Box[2, 3] = TestHelpers.Red;

            var snap = RoundTrip(state);

            Assert.AreEqual(TestHelpers.Blue, snap.Players[0].BoxColors[0 * 5 + 0]);
            Assert.AreEqual(TestHelpers.Red,  snap.Players[0].BoxColors[2 * 5 + 3]);
        }

        [Test]
        public void Encode_Decode_EmptyBoxCell_EncodesAs255()
        {
            var state = MakeReadyState();
            // All cells are -1 (empty)

            var snap = RoundTrip(state);

            for (var idx = 0; idx < 25; idx++)
                Assert.AreEqual(255, snap.Players[0].BoxColors[idx],
                    $"Empty box cell at flat index {idx} must decode as 255.");
        }

        [Test]
        public void Encode_Decode_BoxBits_DerivedFromBoxColors()
        {
            var state = MakeReadyState();
            // Occupy row 0 cols 0 and 2 for player 0
            state.players[0].Box[0, 0] = TestHelpers.Blue;
            state.players[0].Box[0, 2] = TestHelpers.White;

            var snap = RoundTrip(state);

            // Expected bitmask for row 0: bit 0 set (col 0) + bit 2 set (col 2) = 0b00101 = 5
            byte expectedBits = (1 << 0) | (1 << 2);
            Assert.AreEqual(expectedBits, snap.Players[0].BoxBytes[0]);
        }

        [Test]
        public void Encode_Decode_FullBoard_AllCellsPreserved()
        {
            var state = MakeReadyState();
            // Fill entire box with known pattern
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    state.players[0].Box[r, c] = (sbyte)((r + c) % 5);

            var snap = RoundTrip(state);

            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    Assert.AreEqual((r + c) % 5, snap.Players[0].BoxColors[r * 5 + c],
                        $"Box[{r},{c}] mismatch.");
        }

        // -----------------------------------------------------------------------
        // RNG state round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_RngStateNonzero_Preserved()
        {
            var state = MakeReadyState();
            var rng = new RNG(42);
            long serialized = rng.SerializeState();

            Hashtable encoded = RoomPropsCodec.Encode(state, Version, DefaultBaseOrder, rngState: serialized);

            Assert.IsTrue(encoded.ContainsKey(R.RngState), "Non-zero RNG state must be present in encoded props.");
            Assert.AreEqual(serialized, (long)encoded[R.RngState]);
        }

        [Test]
        public void Encode_RngStateZero_NotIncludedInProps()
        {
            var state = MakeReadyState();

            Hashtable encoded = RoomPropsCodec.Encode(state, Version, DefaultBaseOrder, rngState: 0);

            Assert.IsFalse(encoded.ContainsKey(R.RngState),
                "RNG state of 0 must be omitted from encoded props.");
        }

        // -----------------------------------------------------------------------
        // Multi-player round-trip
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_FourPlayers_AllScoresPreserved()
        {
            var (state, _) = TestHelpers.MakeGame(4);
            state.players[0].score = 10;
            state.players[1].score = 20;
            state.players[2].score = 30;
            state.players[3].score = 40;

            var snap = RoundTrip(state);

            Assert.AreEqual(10, snap.Players[0].Score);
            Assert.AreEqual(20, snap.Players[1].Score);
            Assert.AreEqual(30, snap.Players[2].Score);
            Assert.AreEqual(40, snap.Players[3].Score);
        }

        [Test]
        public void Encode_Decode_ActorNumbersPreserved()
        {
            var (state, _) = TestHelpers.MakeGame(3);

            var snap = RoundTrip(state);

            Assert.AreEqual(1, snap.Actors[0]);
            Assert.AreEqual(2, snap.Actors[1]);
            Assert.AreEqual(3, snap.Actors[2]);
        }

        [Test]
        public void Encode_Decode_IdColorsPreserved()
        {
            var (state, _) = TestHelpers.MakeGame(4);
            // MakeGame assigns player color = i-1, so players get colors 0,1,2,3
            var snap = RoundTrip(state);

            for (var i = 0; i < 4; i++)
                Assert.AreEqual(i, snap.IdColors[i], $"Player {i} id color mismatch.");
        }

        // -----------------------------------------------------------------------
        // Sentinel-heavy state (all empty)
        // -----------------------------------------------------------------------

        [Test]
        public void Encode_Decode_EmptyFactories_AllSentinelState()
        {
            var state = MakeReadyState();
            foreach (var f in state.Factories) f.Clear();
            state.center.Clear();

            var snap = RoundTrip(state);

            foreach (var f in snap.Factories)
                Assert.AreEqual(0, f.Count, "All-empty factories must decode as empty lists.");
            Assert.AreEqual(0, snap.Center.Count);
        }
    }
}
