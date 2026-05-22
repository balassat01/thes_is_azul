using System.Linq;
using NUnit.Framework;
using Core.Domain;
using Core.Domain.Rules;

namespace AzulTests
{
    /// <summary>
    /// Unit tests for AzulEngine state transitions.
    /// Each test is self-contained: state is constructed fresh per test.
    /// </summary>
    [TestFixture]
    public class AzulEngineInitializeTests
    {
        // -----------------------------------------------------------------------
        // Initialize
        // -----------------------------------------------------------------------

        [Test]
        [TestCase(2, 5)]
        [TestCase(3, 7)]
        [TestCase(4, 9)]
        public void Initialize_FactoryCount_MatchesPlayerCount(int playerCount, int expectedFactories)
        {
            var state = TestHelpers.MakeInitializedState(playerCount);

            Assert.AreEqual(expectedFactories, state.Factories.Length);
        }

        [Test]
        public void Initialize_BagContains20OfEachColor()
        {
            var state = TestHelpers.MakeInitializedState(2);

            for (var color = 0; color < 5; color++)
                Assert.AreEqual(20, state.bag[color], $"Bag color {color} should start with 20 tiles.");
        }

        [Test]
        public void Initialize_LidIsEmpty()
        {
            var state = TestHelpers.MakeInitializedState(2);

            for (var color = 0; color < 5; color++)
                Assert.AreEqual(0, state.lid[color], $"Lid color {color} must start empty.");
        }

        [Test]
        public void Initialize_PhaseIsRefill()
        {
            var state = TestHelpers.MakeInitializedState(2);

            Assert.AreEqual(Phase.Refill, state.phase);
        }

        [Test]
        public void Initialize_AllPatternColorsAreMinusOne()
        {
            var state = TestHelpers.MakeInitializedState(4);

            foreach (var player in state.players)
                for (var r = 0; r < 5; r++)
                    Assert.AreEqual(-1, player.patternColors[r],
                        $"Player {player.actorNumber} pattern row {r} must start empty.");
        }

        [Test]
        public void Initialize_AllPatternCountsAreZero()
        {
            var state = TestHelpers.MakeInitializedState(3);

            foreach (var player in state.players)
                for (var r = 0; r < 5; r++)
                    Assert.AreEqual(0, player.patternCounts[r]);
        }

        [Test]
        public void Initialize_AllBoxCellsAreMinusOne()
        {
            var state = TestHelpers.MakeInitializedState(3);

            foreach (var player in state.players)
                for (var r = 0; r < 5; r++)
                    for (var c = 0; c < 5; c++)
                        Assert.AreEqual(-1, player.Box[r, c],
                            $"Box[{r},{c}] for player {player.actorNumber} must be -1.");
        }

        [Test]
        public void Initialize_RoundStartsAt1()
        {
            var state = TestHelpers.MakeInitializedState(2);

            Assert.AreEqual(1, state.round);
        }

        [Test]
        public void Initialize_PlayerActorNumbers_MatchSeats()
        {
            var state = TestHelpers.MakeInitializedState(3);

            Assert.AreEqual(1, state.players[0].actorNumber);
            Assert.AreEqual(2, state.players[1].actorNumber);
            Assert.AreEqual(3, state.players[2].actorNumber);
        }

        [Test]
        public void Initialize_FirstPlayerTokenNotInCenter()
        {
            var state = TestHelpers.MakeInitializedState(2);

            Assert.IsFalse(state.firstPlayerTokenInCenter);
        }

        [Test]
        public void Initialize_AllFactoriesStartEmpty()
        {
            var state = TestHelpers.MakeInitializedState(2);

            foreach (var factory in state.Factories)
                Assert.AreEqual(0, factory.Count, "All factories must be empty after Initialize.");
        }
    }

    [TestFixture]
    public class AzulEngineRefillTests
    {
        [Test]
        public void RefillAndBeginOffer_PhaseBecomesFactoryOffer()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(42);

            AzulEngine.RefillAndBeginOffer(state, rng);

            Assert.AreEqual(Phase.FactoryOffer, state.phase);
        }

        [Test]
        public void RefillAndBeginOffer_FirstPlayerTokenPlacedInCenter()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(42);

            AzulEngine.RefillAndBeginOffer(state, rng);

            Assert.IsTrue(state.firstPlayerTokenInCenter);
        }

        [Test]
        public void RefillAndBeginOffer_CenterIsEmpty()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(42);
            state.center.Add(3); // artificially pollute

            AzulEngine.RefillAndBeginOffer(state, rng);

            Assert.AreEqual(0, state.center.Count);
        }

        [Test]
        [TestCase(2, 5)]
        [TestCase(3, 7)]
        [TestCase(4, 9)]
        public void RefillAndBeginOffer_AllFactoriesFilledWith4Tiles(int playerCount, int factoryCount)
        {
            var state = TestHelpers.MakeInitializedState(playerCount);
            var rng = new RNG(42);

            AzulEngine.RefillAndBeginOffer(state, rng);

            foreach (var factory in state.Factories)
                Assert.AreEqual(4, factory.Count, "Each factory should contain exactly 4 tiles.");
        }

        [Test]
        public void RefillAndBeginOffer_TilesAreValidColors()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(42);

            AzulEngine.RefillAndBeginOffer(state, rng);

            foreach (var factory in state.Factories)
                foreach (byte tile in factory)
                    Assert.That(tile, Is.InRange(0, 4), "Factory tiles must be valid colors 0-4.");
        }

        [Test]
        public void RefillAndBeginOffer_ActiveActorSetToStartPlayer()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(42);
            state.startPlayerActor = 2; // simulate round 2+

            AzulEngine.RefillAndBeginOffer(state, rng);

            Assert.AreEqual(2, state.activeActor);
        }

        [Test]
        public void RefillAndBeginOffer_IgnoredWhenPhaseIsNotRefillOrGameOver()
        {
            var (state, _) = TestHelpers.MakeGame(2); // phase = FactoryOffer
            int originalRound = state.round;

            var rng = new RNG(99);
            AzulEngine.RefillAndBeginOffer(state, rng); // should be a no-op

            // Phase and round must not change
            Assert.AreEqual(Phase.FactoryOffer, state.phase);
            Assert.AreEqual(originalRound, state.round);
        }

        [Test]
        public void RefillAndBeginOffer_DrainsBagTiles()
        {
            var state = TestHelpers.MakeInitializedState(2);
            var rng = new RNG(1);

            AzulEngine.RefillAndBeginOffer(state, rng);

            int totalInBag = state.bag.Sum();
            // 5 factories × 4 tiles = 20 tiles drawn; bag started at 100
            Assert.AreEqual(100 - 20, totalInBag);
        }

        [Test]
        public void RefillAndBeginOffer_BagEmpty_DrawsFromLid()
        {
            var state = TestHelpers.MakeInitializedState(2);
            // Empty the bag and put tiles in the lid instead
            for (var c = 0; c < 5; c++) state.bag[c] = 0;
            for (var c = 0; c < 5; c++) state.lid[c] = 20;
            var rng = new RNG(1);

            AzulEngine.RefillAndBeginOffer(state, rng);

            // Factories must still be filled — tiles came from lid
            foreach (var factory in state.Factories)
                Assert.AreEqual(4, factory.Count, "Factories must be filled from lid when bag is empty.");
        }

        [Test]
        public void RefillAndBeginOffer_BagAndLidBothEmpty_FactoriesGetFewerTiles()
        {
            var state = TestHelpers.MakeInitializedState(2);
            // No tiles anywhere — factories cannot be fully filled
            for (var c = 0; c < 5; c++) { state.bag[c] = 0; state.lid[c] = 0; }
            var rng = new RNG(1);

            AzulEngine.RefillAndBeginOffer(state, rng);

            int total = 0;
            foreach (var factory in state.Factories) total += factory.Count;
            Assert.AreEqual(0, total, "No tiles available — all factories must remain empty.");
        }
    }

    [TestFixture]
    public class AzulEngineApplyTakeTilesTests
    {
        // -----------------------------------------------------------------------
        // Rejection guards — invalid command fields
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_NegativeRow_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, row: -1);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_RowGreaterThan4_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, row: 5);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_FactoryIndexNegative_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, -1, TestHelpers.Blue, row: 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_FactoryIndexTooLarge_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2); // 5 factories, indices 0-4
            var rule = TestHelpers.StandardRule();
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 99, TestHelpers.Blue, row: 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_FromCenter_ColorNotPresent_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();
            // Center has no tiles at this point (none moved there yet)
            state.center.Clear();
            var cmd = new TakeTilesCmd(1, DraftSource.Center, 0, TestHelpers.Blue, row: 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        // -----------------------------------------------------------------------
        // Rejection guards — original guards
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_WrongPhase_ReturnsFalse()
        {
            var state = TestHelpers.MakeInitializedState(2); // Phase.Refill
            var rule = TestHelpers.StandardRule();
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_WrongActor_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2); // P1 is active
            var rule = TestHelpers.StandardRule();
            // Actor 2 tries to act when it is actor 1's turn
            var cmd = new TakeTilesCmd(2, DraftSource.Factory, 0, TestHelpers.Blue, 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyTakeTiles_ColorNotPresentInFactory_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Find a color not present in factory 0
            byte absentColor = 255;
            for (byte c = 0; c < 5; c++)
            {
                if (!state.Factories[0].Contains(c))
                {
                    absentColor = c;
                    break;
                }
            }

            // If all 5 colors present (unlikely with 4 tiles), skip this test
            if (absentColor == 255) Assert.Inconclusive("All colors happen to be in factory 0.");

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, absentColor, 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyTakeTiles_RowAlreadyHasThatColor_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Pre-occupy box[0,0] for player 1 (Blue row 0 → col 0)
            state.players[0].Box[0, 0] = TestHelpers.Blue;

            // Find factory that has Blue
            int factoryIdx = -1;
            for (var i = 0; i < state.Factories.Length; i++)
            {
                if (state.Factories[i].Contains(TestHelpers.Blue))
                { factoryIdx = i; break; }
            }
            if (factoryIdx < 0) Assert.Inconclusive("No factory contains Blue in this seeded game.");

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, factoryIdx, TestHelpers.Blue, row: 0);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyTakeTiles_PatternRowHasDifferentColor_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Pre-fill pattern row 1 of player 1 with Brown
            state.players[0].patternColors[1] = TestHelpers.Brown;
            state.players[0].patternCounts[1] = 1;

            // Find a factory with Blue (not Brown)
            int factoryIdx = -1;
            for (var i = 0; i < state.Factories.Length; i++)
                if (state.Factories[i].Contains(TestHelpers.Blue)) { factoryIdx = i; break; }

            if (factoryIdx < 0) Assert.Inconclusive("No factory has Blue.");

            // Try to put Blue into pattern row 1 which already has Brown
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, factoryIdx, TestHelpers.Blue, row: 1);

            bool ok = AzulEngine.ApplyTakeTiles(state, cmd, rule, out string error);

            Assert.IsFalse(ok);
        }

        // -----------------------------------------------------------------------
        // Take from factory — happy path
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_FromFactory_ChoseColorRemovedFromFactory()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Use first factory and first color present
            byte color = state.Factories[0][0];
            int countBefore = state.Factories[0].Count(t => t == color);
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, color, row: 0);

            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(0, state.Factories[0].Count(t => t == color),
                "All tiles of the chosen color must be removed from the factory.");
        }

        [Test]
        public void ApplyTakeTiles_FromFactory_LeftoversMovedToCenter()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Count leftover tiles (those with a different color)
            byte color = state.Factories[0][0];
            int leftovers = state.Factories[0].Count(t => t != color);
            int centerBefore = state.center.Count;

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, color, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(centerBefore + leftovers, state.center.Count,
                "Leftover factory tiles must go to center.");
        }

        [Test]
        public void ApplyTakeTiles_FromFactory_FactoryIsEmptyAfterward()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            byte color = state.Factories[0][0];
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, color, row: 0);

            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(0, state.Factories[0].Count);
        }

        [Test]
        public void ApplyTakeTiles_FromFactory_TilesPlacedInPatternLine()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            byte color = state.Factories[0][0];
            int taken = state.Factories[0].Count(t => t == color);
            int capacity = 1; // row 0 has capacity 1
            int placed = System.Math.Min(taken, capacity);

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, color, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(color, state.players[0].patternColors[0]);
            Assert.AreEqual(placed, state.players[0].patternCounts[0]);
        }

        [Test]
        public void ApplyTakeTiles_FromFactory_TurnAdvancesToNextPlayer()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            byte color = state.Factories[0][0];
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, color, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(2, state.activeActor);
        }

        // -----------------------------------------------------------------------
        // Take from center
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_FromCenter_FirstPickTakesFirstPlayerToken()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Manually add a color to center (simulating previous factory take)
            state.center.Add(TestHelpers.Blue);
            Assert.IsTrue(state.firstPlayerTokenInCenter);

            var cmd = new TakeTilesCmd(1, DraftSource.Center, 0, TestHelpers.Blue, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.IsFalse(state.firstPlayerTokenInCenter, "Token must be removed from center.");
            Assert.Contains((byte)TestHelpers.Token, state.players[0].floor,
                "First-player token (5) must be on the taker's floor.");
            Assert.AreEqual(1, state.startPlayerActor,
                "startPlayerActor must be updated to the first center taker.");
        }

        [Test]
        public void ApplyTakeTiles_FromCenter_TokenOnlyGrantedOnce()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            state.center.Add(TestHelpers.Blue);
            state.center.Add(TestHelpers.Blue);

            // P1 takes from center first
            var cmd1 = new TakeTilesCmd(1, DraftSource.Center, 0, TestHelpers.Blue, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd1, rule, out _);

            // Add more tiles so P2 can take from center
            state.center.Add(TestHelpers.Brown);
            var cmd2 = new TakeTilesCmd(2, DraftSource.Center, 0, TestHelpers.Brown, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd2, rule, out _);

            // P2 must NOT receive the token
            int tokenCount = state.players[1].floor.Count(t => t == TestHelpers.Token);
            Assert.AreEqual(0, tokenCount, "Second center pick must not grant the token again.");
        }

        // -----------------------------------------------------------------------
        // Overflow to floor and lid
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_Overflow_ExcessTilesGoToFloor()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Row 0 has capacity 1. Add 3 Blue to factory so 2 overflow to floor
            state.Factories[0].Clear();
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);
            // Remove any other tiles from factories to prevent market-empty transition
            // Keep factories 1+ untouched — they still have tiles

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, row: 0);
            int floorBefore = state.players[0].floor.Count;
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            // 3 taken, 1 fits in row 0, 2 overflow
            Assert.AreEqual(floorBefore + 2, state.players[0].floor.Count);
        }

        [Test]
        public void ApplyTakeTiles_FloorOverflow_ExcessGoToLidNotFloor()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Fill player 1's floor to capacity (6/7 to leave room) then overflow past 7
            for (var i = 0; i < 6; i++) state.players[0].floor.Add(TestHelpers.Brown);

            // Factory 0: 3 Blue tiles → 1 fits in row 0, 2 overflow;
            // floor has 1 free slot: 1 tile fits on floor, 1 goes to lid
            state.Factories[0].Clear();
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);

            int lidBlueBefore = state.lid[TestHelpers.Blue];
            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(7, state.players[0].floor.Count, "Floor must be capped at 7.");
            Assert.Greater(state.lid[TestHelpers.Blue], lidBlueBefore,
                "Excess overflow beyond floor cap must go to lid.");
        }

        // -----------------------------------------------------------------------
        // Market empty → phase transition
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_PatternRowAlreadyFull_SameColor_AllTilesOverflowToFloor()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Pre-fill row 0 (capacity 1) with Blue — it is now full
            state.players[0].patternColors[0] = TestHelpers.Blue;
            state.players[0].patternCounts[0] = 1;

            // Put 3 Blue tiles in factory 0 so the command is valid in all other respects
            state.Factories[0].Clear();
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);
            state.Factories[0].Add(TestHelpers.Blue);

            // boxRule.RowHasColor returns false (no Blue in the box yet), so the command proceeds
            int floorBefore = state.players[0].floor.Count;
            bool ok = AzulEngine.ApplyTakeTiles(state,
                new TakeTilesCmd(1, DraftSource.Factory, 0, TestHelpers.Blue, row: 0),
                rule, out _);

            Assert.IsTrue(ok, "Command with correct color for a full-but-not-yet-placed row must succeed.");
            // Row was already at capacity: free=0, all 3 overflow to floor
            Assert.AreEqual(floorBefore + 3, state.players[0].floor.Count,
                "All taken tiles must overflow to floor when the pattern row is already at capacity.");
        }

        [Test]
        public void ApplyTakeTiles_ThreePlayer_TurnOrder_CyclesP1P2P3()
        {
            var (state, _) = TestHelpers.MakeGame(3);
            var rule = TestHelpers.StandardRule();

            // P1 turn
            Assert.AreEqual(1, state.activeActor);
            byte color1 = state.Factories[0][0];
            AzulEngine.ApplyTakeTiles(state, new TakeTilesCmd(1, DraftSource.Factory, 0, color1, 0), rule, out _);

            // P2 turn
            Assert.AreEqual(2, state.activeActor);
            byte color2 = state.Factories[1][0];
            AzulEngine.ApplyTakeTiles(state, new TakeTilesCmd(2, DraftSource.Factory, 1, color2, 0), rule, out _);

            // P3 turn
            Assert.AreEqual(3, state.activeActor);
            byte color3 = state.Factories[2][0];
            AzulEngine.ApplyTakeTiles(state, new TakeTilesCmd(3, DraftSource.Factory, 2, color3, 0), rule, out _);

            // Wraps back to P1
            Assert.AreEqual(1, state.activeActor, "After P3 acts, turn must wrap back to P1.");
        }

        // -----------------------------------------------------------------------
        // Market empty → phase transition
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyTakeTiles_MarketBecomesEmpty_PhaseBecomesBoxTiling()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            var rule = TestHelpers.StandardRule();

            // Drain all factories except factory 0; leave exactly one color in factory 0
            for (var i = 1; i < state.Factories.Length; i++) state.Factories[i].Clear();
            state.center.Clear();
            state.firstPlayerTokenInCenter = false;
            byte lastColor = state.Factories[0][0];
            // Keep only one tile of lastColor in factory 0
            state.Factories[0].RemoveAll(t => t != lastColor);
            while (state.Factories[0].Count > 1) state.Factories[0].RemoveAt(1);

            var cmd = new TakeTilesCmd(1, DraftSource.Factory, 0, lastColor, row: 0);
            AzulEngine.ApplyTakeTiles(state, cmd, rule, out _);

            Assert.AreEqual(Phase.BoxTiling, state.phase);
        }
    }

    [TestFixture]
    public class AzulEngineExecuteBoxTilingTests
    {
        // -----------------------------------------------------------------------
        // Full pattern row placed in box
        // -----------------------------------------------------------------------

        [Test]
        public void ExecuteBoxTiling_FullPatternRow_PlacedInBoxAtCorrectColumn()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Player 1, row 0, color Blue (capacity 1) → standard rule col = 0
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(TestHelpers.Blue, state.players[0].Box[0, 0],
                "Blue placed in row 0 must go to column 0 per default base order.");
        }

        [Test]
        public void ExecuteBoxTiling_FullPatternRow_PatternLineClearedAfterPlacement()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(-1, state.players[0].patternColors[0]);
            Assert.AreEqual(0, state.players[0].patternCounts[0]);
        }

        [Test]
        public void ExecuteBoxTiling_PartialPatternRow_NotMoved()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            // Row 1 requires 2 tiles; fill only 1
            state.players[0].patternColors[1] = TestHelpers.Brown;
            state.players[0].patternCounts[1] = 1;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(-1, state.players[0].Box[1, 0] >= 0 ? state.players[0].Box[1, 0] : -1);
            Assert.AreEqual(1, state.players[0].patternCounts[1], "Partial rows must stay unchanged.");
        }

        [Test]
        public void ExecuteBoxTiling_FullRow_ScoreIncremented()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            int scoreBefore = state.players[0].score;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.Greater(state.players[0].score, scoreBefore, "Score must increase after a valid placement.");
        }

        [Test]
        public void ExecuteBoxTiling_FullRow_ExtrasTilesMovedToLid()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            // Row 2 capacity = 3; fill it with Brown; 2 extras go to lid
            TestHelpers.FillPatternRow(state.players[0], row: 2, TestHelpers.Brown);
            int lidBefore = state.lid[TestHelpers.Brown];

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            // needed - 1 = 2 discarded to lid
            Assert.AreEqual(lidBefore + 2, state.lid[TestHelpers.Brown]);
        }

        // -----------------------------------------------------------------------
        // Floor penalties
        // -----------------------------------------------------------------------

        [Test]
        public void ExecuteBoxTiling_FloorPenaltyApplied_ScoreReducedCorrectly()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // 3 floor tiles = -4 penalty
            state.players[0].floor.Add(TestHelpers.Blue);
            state.players[0].floor.Add(TestHelpers.Blue);
            state.players[0].floor.Add(TestHelpers.Blue);
            state.players[0].score = 10;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(6, state.players[0].score); // 10 - 4 = 6
        }

        [Test]
        public void ExecuteBoxTiling_FloorPenaltyNeverBelowZero_ClampedAt0()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Full floor (-14) with score 5 → clamped to 0
            for (var i = 0; i < 7; i++) state.players[0].floor.Add(TestHelpers.Blue);
            state.players[0].score = 5;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(0, state.players[0].score);
        }

        [Test]
        public void ExecuteBoxTiling_FloorClearedAfterPenalty()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            state.players[0].floor.Add(TestHelpers.Blue);

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(0, state.players[0].floor.Count);
        }

        [Test]
        public void ExecuteBoxTiling_FloorTilesReturnedToLid()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            state.players[0].floor.Add(TestHelpers.Red);
            int lidRedBefore = state.lid[TestHelpers.Red];

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(lidRedBefore + 1, state.lid[TestHelpers.Red]);
        }

        [Test]
        public void ExecuteBoxTiling_FirstPlayerTokenOnFloor_NotAddedToLid()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Token (value 5) on floor
            state.players[0].floor.Add(TestHelpers.Token);
            int lidSumBefore = state.lid.Sum();

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            // Token is not a real color tile — lid total should not increase from the token
            Assert.AreEqual(lidSumBefore, state.lid.Sum(),
                "The first-player token must not be added to any lid bucket.");
        }

        // -----------------------------------------------------------------------
        // End-game trigger
        // -----------------------------------------------------------------------

        [Test]
        public void ExecuteBoxTiling_CompletedBoxRow_SetsEndTriggeredAndGameOver()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Fill entire row 0 of box for player 1 except column 0; then fill pattern row 0 with Blue
            // After tiling, row 0 will be complete
            for (var c = 1; c < 5; c++)
                state.players[0].Box[0, c] = (sbyte)(c); // occupy cols 1-4 with valid colors
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue); // Blue → col 0

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.IsTrue(state.endTriggered, "endTriggered must be set when a box row is completed.");
            Assert.AreEqual(Phase.GameOver, state.phase);
        }

        [Test]
        public void ExecuteBoxTiling_NoCompletedRow_PhaseBecomesRefill()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();
            // No full pattern rows → no placements
            int roundBefore = state.round;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(Phase.Refill, state.phase);
            Assert.AreEqual(roundBefore + 1, state.round);
        }

        [Test]
        public void ExecuteBoxTiling_EndGame_AwardsRowColColorBonuses()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Build a board for player 1 where row 0 is complete after tiling
            for (var c = 1; c < 5; c++)
                state.players[0].Box[0, c] = (sbyte)StandardBoxMap.ColorAtCell(TestHelpers.DefaultBaseOrder, 0, c);
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            state.players[0].score = 0;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            // After game over, score must include placement + end-game row bonus
            Assert.GreaterOrEqual(state.players[0].score, 2,
                "Completed row must grant at least +2 end-game row bonus.");
        }

        // -----------------------------------------------------------------------
        // Multi-player coverage
        // -----------------------------------------------------------------------

        [Test]
        public void ExecuteBoxTiling_FourPlayers_AllPlayersGetFloorPenalty()
        {
            var (state, _) = TestHelpers.MakeGame(4);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            for (var p = 0; p < 4; p++)
            {
                state.players[p].score = 10;
                state.players[p].floor.Add(TestHelpers.Blue); // -1 each
            }

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            for (var p = 0; p < 4; p++)
                Assert.AreEqual(9, state.players[p].score, $"Player {p} should have 10 - 1 = 9.");
        }

        [Test]
        public void ExecuteBoxTiling_MultipleFullRows_AllPlacedForSamePlayer()
        {
            var (state, _) = TestHelpers.MakeGame(2);
            state.phase = Phase.BoxTiling;
            var rule = TestHelpers.StandardRule();

            // Give player 1 two full pattern rows
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);  // capacity 1
            TestHelpers.FillPatternRow(state.players[0], row: 1, TestHelpers.Brown); // capacity 2

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            // Both pattern rows must be cleared — that proves they were placed
            Assert.AreEqual(-1, state.players[0].patternColors[0], "Row 0 must be cleared after placement.");
            Assert.AreEqual(-1, state.players[0].patternColors[1], "Row 1 must be cleared after placement.");
            Assert.AreEqual(0, state.players[0].patternCounts[0], "Row 0 count must be 0 after placement.");
            Assert.AreEqual(0, state.players[0].patternCounts[1], "Row 1 count must be 0 after placement.");
            // Blue (0) at row 0 → col (0+0)%5=0; Brown (1) at row 1 → col (1+1)%5=2
            Assert.GreaterOrEqual(state.players[0].Box[0, 0], 0, "Blue must be placed in box at row 0 col 0.");
            Assert.GreaterOrEqual(state.players[0].Box[1, 2], 0, "Brown must be placed in box at row 1 col 2.");
        }

        [Test]
        public void ExecuteBoxTiling_IgnoredWhenNotInBoxTilingPhase()
        {
            var (state, _) = TestHelpers.MakeGame(2); // Phase.FactoryOffer
            var rule = TestHelpers.StandardRule();
            int scoreBefore = state.players[0].score;

            AzulEngine.ExecuteBoxTiling(state, rule, TestHelpers.StandardColorMap());

            Assert.AreEqual(scoreBefore, state.players[0].score, "ExecuteBoxTiling must be a no-op outside BoxTiling phase.");
            Assert.AreEqual(Phase.FactoryOffer, state.phase);
        }
    }

    [TestFixture]
    public class AzulEngineGrayBoxTests
    {
        // -----------------------------------------------------------------------
        // BeginGrayBoxTiling
        // -----------------------------------------------------------------------

        [Test]
        public void BeginGrayBoxTiling_PlayerWithFullRows_SetsActiveActorToThatPlayer()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.startPlayerActor = 1;

            // Player 1 (index 0) has a full row
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            AzulEngine.BeginGrayBoxTiling(state, TestHelpers.StandardColorMap());

            Assert.AreEqual(1, state.activeActor);
        }

        [Test]
        public void BeginGrayBoxTiling_OnlySecondPlayerHasFullRows_SetsActiveToSecondPlayer()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.startPlayerActor = 1;

            // Only player 2 (index 1) has a full row
            TestHelpers.FillPatternRow(state.players[1], row: 0, TestHelpers.Blue);

            AzulEngine.BeginGrayBoxTiling(state, TestHelpers.StandardColorMap());

            Assert.AreEqual(2, state.activeActor);
        }

        [Test]
        public void BeginGrayBoxTiling_NoPlayerHasFullRows_TransitionsImmediately()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            int roundBefore = state.round;

            AzulEngine.BeginGrayBoxTiling(state, TestHelpers.StandardColorMap());

            // With no full rows, phase must transition to Refill (or GameOver if endTriggered)
            Assert.That(state.phase, Is.EqualTo(Phase.Refill).Or.EqualTo(Phase.GameOver));
        }

        // -----------------------------------------------------------------------
        // ApplyPlaceInBox
        // -----------------------------------------------------------------------

        [Test]
        public void ApplyPlaceInBox_WrongPhase_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true); // FactoryOffer
            var cmd = new PlaceInBoxCmd(1, 0, 0);

            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out string error);

            Assert.IsFalse(ok);
            Assert.IsNotNull(error);
        }

        [Test]
        public void ApplyPlaceInBox_WrongActor_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            // Actor 2 tries to act when it's actor 1's turn
            var cmd = new PlaceInBoxCmd(2, 0, 0);

            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyPlaceInBox_PatternRowNotFull_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            // Row 1 needs 2 tiles; only put 1
            state.players[0].patternColors[1] = TestHelpers.Brown;
            state.players[0].patternCounts[1] = 1;

            var cmd = new PlaceInBoxCmd(1, row: 1, col: 0);

            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyPlaceInBox_CellAlreadyOccupied_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            state.players[0].Box[0, 0] = TestHelpers.Brown; // occupying target cell

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 0);

            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyPlaceInBox_ColorAlreadyInColumn_ReturnsFalse()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            // Blue already in column 0 (different row)
            state.players[0].Box[2, 0] = TestHelpers.Blue;

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 0);

            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out string error);

            Assert.IsFalse(ok);
        }

        [Test]
        public void ApplyPlaceInBox_ValidPlacement_TilePlacedAndScoreUpdated()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            int scoreBefore = state.players[0].score;

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 2);
            bool ok = AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(TestHelpers.Blue, state.players[0].Box[0, 2]);
            Assert.Greater(state.players[0].score, scoreBefore);
        }

        [Test]
        public void ApplyPlaceInBox_ValidPlacement_PatternLineCleared()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 2);
            AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            Assert.AreEqual(-1, state.players[0].patternColors[0]);
            Assert.AreEqual(0, state.players[0].patternCounts[0]);
        }

        [Test]
        public void ApplyPlaceInBox_LastRowForCurrentPlayer_AdvancesToNextPlayerWithRows()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            // Player 2 also has a full row
            TestHelpers.FillPatternRow(state.players[1], row: 0, TestHelpers.Brown);

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 2);
            AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            Assert.AreEqual(2, state.activeActor,
                "After current player's last row, active actor must advance to next player with rows.");
        }

        [Test]
        public void ApplyPlaceInBox_AllPlayersDone_FloorPenaltiesApplied()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            state.players[0].floor.Add(TestHelpers.Red); // 1 floor tile = -1
            state.players[0].score = 10;

            // Player 2 has no full rows
            var cmd = new PlaceInBoxCmd(1, row: 0, col: 2);
            AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            // Should have finalized: score = 10 (before) + placement score - 1 (floor)
            Assert.LessOrEqual(state.players[0].score, 10 + 5); // sanity upper bound
            Assert.AreEqual(0, state.players[0].floor.Count, "Floor must be cleared after finalization.");
        }

        [Test]
        public void ApplyPlaceInBox_AllPlayersDone_PhaseTransitionsToRefill()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            // No full rows for player 2

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 2);
            AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            Assert.That(state.phase, Is.EqualTo(Phase.Refill).Or.EqualTo(Phase.GameOver));
        }

        [Test]
        public void ApplyPlaceInBox_CompletedBoxRow_SetsEndTriggered()
        {
            var (state, _) = TestHelpers.MakeGame(2, grayBox: true);
            state.phase = Phase.BoxTiling;
            state.activeActor = 1;
            TestHelpers.FillPatternRow(state.players[0], row: 0, TestHelpers.Blue);
            // Pre-fill columns 0-3 of row 0 so placing col 4 completes the row
            for (var c = 0; c < 4; c++)
                state.players[0].Box[0, c] = (sbyte)c;

            var cmd = new PlaceInBoxCmd(1, row: 0, col: 4);
            AzulEngine.ApplyPlaceInBox(state, cmd, TestHelpers.StandardColorMap(), out _);

            Assert.IsTrue(state.endTriggered);
            Assert.AreEqual(Phase.GameOver, state.phase);
        }

        // -----------------------------------------------------------------------
        // HasFullPatternRows (public utility)
        // -----------------------------------------------------------------------

        [Test]
        public void HasFullPatternRows_PlayerWithNoRows_ReturnsFalse()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++) { ps.patternColors[r] = -1; ps.patternCounts[r] = 0; }

            bool result = AzulEngine.HasFullPatternRows(ps);

            Assert.IsFalse(result);
        }

        [Test]
        public void HasFullPatternRows_PlayerWithOneFullRow_ReturnsTrue()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++) { ps.patternColors[r] = -1; ps.patternCounts[r] = 0; }
            TestHelpers.FillPatternRow(ps, row: 2, TestHelpers.White);

            bool result = AzulEngine.HasFullPatternRows(ps);

            Assert.IsTrue(result);
        }
    }
}
