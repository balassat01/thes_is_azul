using NUnit.Framework;
using Core.Domain;

namespace AzulTests
{
    [TestFixture]
    public class ScoringTests
    {
        // -----------------------------------------------------------------------
        // ScorePlacement
        // -----------------------------------------------------------------------

        [Test]
        public void ScorePlacement_IsolatedTile_Returns1()
        {
            // Arrange: empty board, place at (2,2)
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            ps.Box[2, 2] = 0;

            // Act
            int score = Scoring.ScorePlacement(ps, 2, 2);

            // Assert
            Assert.AreEqual(1, score);
        }

        [Test]
        public void ScorePlacement_HorizontalRunOf3_Returns3()
        {
            // Arrange: row 0 has tiles at columns 0,1; placing at column 2 → run of 3
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            ps.Box[0, 0] = 0;
            ps.Box[0, 1] = 1;
            ps.Box[0, 2] = 2; // the newly placed tile

            // Act
            int score = Scoring.ScorePlacement(ps, 0, 2);

            // Assert: horizontal run = 3, vertical = 1 (only itself) → h>1 only → score = h = 3
            Assert.AreEqual(3, score);
        }

        [Test]
        public void ScorePlacement_VerticalRunOf3_Returns3()
        {
            // Arrange: column 1 has tiles at rows 0,1; placing at row 2 → run of 3
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            ps.Box[0, 1] = 0;
            ps.Box[1, 1] = 1;
            ps.Box[2, 1] = 2; // newly placed

            // Act
            int score = Scoring.ScorePlacement(ps, 2, 1);

            // Assert: v=3, h=1 → score = v = 3
            Assert.AreEqual(3, score);
        }

        [Test]
        public void ScorePlacement_LShapeBothHorizontalAndVertical_ReturnsSumOfBothRuns()
        {
            // Arrange: existing h-run of 2 to the left AND v-run of 2 above.
            // Place tile at (2,2): left neighbor (2,1), top neighbor (1,2) already present.
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            ps.Box[2, 1] = 0;  // left neighbor
            ps.Box[1, 2] = 0;  // top neighbor
            ps.Box[2, 2] = 0;  // newly placed

            // Act
            int score = Scoring.ScorePlacement(ps, 2, 2);

            // Assert: h=2 (cols 1,2), v=2 (rows 1,2) → both >1 → sum = 4
            Assert.AreEqual(4, score);
        }

        [Test]
        public void ScorePlacement_FullRow5_Returns5()
        {
            // Arrange: row 3 has tiles 0-3 already; place at col 4
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var c = 0; c < 4; c++) ps.Box[3, c] = (sbyte)c;
            ps.Box[3, 4] = 0; // place

            int score = Scoring.ScorePlacement(ps, 3, 4);

            // h=5 (full row), v=1 → h>1 only → 5
            Assert.AreEqual(5, score);
        }

        [Test]
        public void ScorePlacement_FullColumn5_Returns5()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var r = 0; r < 4; r++) ps.Box[r, 0] = (sbyte)r;
            ps.Box[4, 0] = 0;

            int score = Scoring.ScorePlacement(ps, 4, 0);

            // v=5, h=1 → 5
            Assert.AreEqual(5, score);
        }

        [Test]
        public void ScorePlacement_LargeL_Returns10()
        {
            // Full row of 5 + full column of 5 meeting at the corner tile
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            // Row 4 complete
            for (var c = 0; c < 5; c++) ps.Box[4, c] = (sbyte)c;
            // Column 4 complete
            for (var r = 0; r < 5; r++) ps.Box[r, 4] = (sbyte)r;
            // Place the corner tile that closes both (already placed above, rescore)
            // The tile at (4,4) exists; compute its placement score
            int score = Scoring.ScorePlacement(ps, 4, 4);

            // h=5, v=5 → 10
            Assert.AreEqual(10, score);
        }

        // -----------------------------------------------------------------------
        // ScoreFloor
        // -----------------------------------------------------------------------

        [Test]
        public void ScoreFloor_NoTiles_Returns0()
        {
            var ps = new PlayerState();

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(0, penalty);
        }

        [Test]
        public void ScoreFloor_OneTile_ReturnsMinus1()
        {
            var ps = new PlayerState();
            ps.floor.Add(TestHelpers.Blue);

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(-1, penalty);
        }

        [Test]
        public void ScoreFloor_ThreeTiles_ReturnsMinus4()
        {
            // Penalty slots: -1, -1, -2 → sum = -4
            var ps = new PlayerState();
            ps.floor.Add(TestHelpers.Blue);
            ps.floor.Add(TestHelpers.Brown);
            ps.floor.Add(TestHelpers.White);

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(-4, penalty);
        }

        [Test]
        public void ScoreFloor_SevenTiles_ReturnsMinus14()
        {
            // Full floor: -1,-1,-2,-2,-2,-3,-3 = -14
            var ps = new PlayerState();
            for (var i = 0; i < 7; i++) ps.floor.Add(TestHelpers.Blue);

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(-14, penalty);
        }

        [Test]
        public void ScoreFloor_FirstPlayerTokenCountsAsSlot()
        {
            // Token (value 5) counts as an occupied floor slot for penalty purposes
            var ps = new PlayerState();
            ps.floor.Add(TestHelpers.Token); // slot 0: -1

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(-1, penalty);
        }

        [Test]
        public void ScoreFloor_FiveTiles_ReturnsMinus8()
        {
            // -1,-1,-2,-2,-2 = -8
            var ps = new PlayerState();
            for (var i = 0; i < 5; i++) ps.floor.Add(TestHelpers.Red);

            int penalty = Scoring.ScoreFloor(ps);

            Assert.AreEqual(-8, penalty);
        }

        // -----------------------------------------------------------------------
        // EndGameBonusRows
        // -----------------------------------------------------------------------

        [Test]
        public void EndGameBonusRows_NoCompleteRows_Returns0()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;

            int bonus = Scoring.EndGameBonusRows(ps);

            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void EndGameBonusRows_OneCompleteRow_Returns2()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var c = 0; c < 5; c++) ps.Box[0, c] = 0;

            int bonus = Scoring.EndGameBonusRows(ps);

            Assert.AreEqual(2, bonus);
        }

        [Test]
        public void EndGameBonusRows_FiveCompleteRows_Returns10()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = (sbyte)c; // fill entire board

            int bonus = Scoring.EndGameBonusRows(ps);

            Assert.AreEqual(10, bonus);
        }

        [Test]
        public void EndGameBonusRows_PartialRowNotCounted()
        {
            // Row 1 has 4 out of 5 cells — must not count
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var c = 0; c < 4; c++) ps.Box[1, c] = 0;

            int bonus = Scoring.EndGameBonusRows(ps);

            Assert.AreEqual(0, bonus);
        }

        // -----------------------------------------------------------------------
        // EndGameBonusCols
        // -----------------------------------------------------------------------

        [Test]
        public void EndGameBonusCols_NoCompleteCols_Returns0()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;

            int bonus = Scoring.EndGameBonusCols(ps);

            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void EndGameBonusCols_OneCompleteCol_Returns7()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var r = 0; r < 5; r++) ps.Box[r, 2] = 0;

            int bonus = Scoring.EndGameBonusCols(ps);

            Assert.AreEqual(7, bonus);
        }

        [Test]
        public void EndGameBonusCols_FiveCompleteCols_Returns35()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = (sbyte)r;

            int bonus = Scoring.EndGameBonusCols(ps);

            Assert.AreEqual(35, bonus);
        }

        [Test]
        public void EndGameBonusCols_PartialColNotCounted()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var r = 0; r < 4; r++) ps.Box[r, 0] = 0;

            int bonus = Scoring.EndGameBonusCols(ps);

            Assert.AreEqual(0, bonus);
        }

        // -----------------------------------------------------------------------
        // EndGameBonusColors
        // -----------------------------------------------------------------------

        [Test]
        public void EndGameBonusColors_NoCompleteColorSet_Returns0()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            // Place only 4 blue tiles
            ps.Box[0, 0] = 0; ps.Box[1, 0] = 0; ps.Box[2, 0] = 0; ps.Box[3, 0] = 0;

            int bonus = Scoring.EndGameBonusColors(ps, (r, c) => (int)ps.Box[r, c]);

            Assert.AreEqual(0, bonus);
        }

        [Test]
        public void EndGameBonusColors_OneCompleteColorSet_Returns10()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            // 5 blue (color 0) tiles
            for (var r = 0; r < 5; r++) ps.Box[r, 0] = 0;

            int bonus = Scoring.EndGameBonusColors(ps, (r, c) => (int)ps.Box[r, c]);

            Assert.AreEqual(10, bonus);
        }

        // -----------------------------------------------------------------------
        // DetermineWinners
        // -----------------------------------------------------------------------

        [Test]
        public void DetermineWinners_SingleLeader_ReturnsThatActorOnly()
        {
            var p1 = new PlayerState { actorNumber = 1, score = 30 };
            var p2 = new PlayerState { actorNumber = 2, score = 20 };
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                { p1.Box[r, c] = -1; p2.Box[r, c] = -1; }

            var winners = Scoring.DetermineWinners(new[] { p1, p2 });

            Assert.AreEqual(1, winners.Count);
            Assert.AreEqual(1, winners[0]);
        }

        [Test]
        public void DetermineWinners_TiebreakByRows_PlayerWithMoreRowsWins()
        {
            var p1 = new PlayerState { actorNumber = 1, score = 25 };
            var p2 = new PlayerState { actorNumber = 2, score = 25 };
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                { p1.Box[r, c] = -1; p2.Box[r, c] = -1; }
            // p2 has one completed row
            for (var c = 0; c < 5; c++) p2.Box[0, c] = 0;

            var winners = Scoring.DetermineWinners(new[] { p1, p2 });

            Assert.AreEqual(1, winners.Count);
            Assert.AreEqual(2, winners[0]);
        }

        [Test]
        public void DetermineWinners_FullTie_ReturnsBothActors()
        {
            var p1 = new PlayerState { actorNumber = 1, score = 15 };
            var p2 = new PlayerState { actorNumber = 2, score = 15 };
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                { p1.Box[r, c] = -1; p2.Box[r, c] = -1; }
            // Both have one completed row
            for (var c = 0; c < 5; c++) { p1.Box[0, c] = 0; p2.Box[0, c] = 0; }

            var winners = Scoring.DetermineWinners(new[] { p1, p2 });

            Assert.AreEqual(2, winners.Count);
            Assert.That(winners, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void DetermineWinners_ThreePlayers_TiebreakPicksHigherRows()
        {
            var p1 = new PlayerState { actorNumber = 1, score = 20 };
            var p2 = new PlayerState { actorNumber = 2, score = 20 };
            var p3 = new PlayerState { actorNumber = 3, score = 20 };
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                { p1.Box[r, c] = -1; p2.Box[r, c] = -1; p3.Box[r, c] = -1; }
            // p3 has 2 completed rows, p1 and p2 have 0
            for (var c = 0; c < 5; c++) { p3.Box[0, c] = 0; p3.Box[1, c] = 0; }

            var winners = Scoring.DetermineWinners(new[] { p1, p2, p3 });

            Assert.AreEqual(1, winners.Count);
            Assert.AreEqual(3, winners[0]);
        }

        [Test]
        public void DetermineWinners_EmptyArray_ReturnsEmptyList()
        {
            var winners = Scoring.DetermineWinners(new PlayerState[0]);

            Assert.AreEqual(0, winners.Count);
        }

        [Test]
        public void DetermineWinners_NullArray_ReturnsEmptyList()
        {
            var winners = Scoring.DetermineWinners(null);

            Assert.AreEqual(0, winners.Count);
        }

        // -----------------------------------------------------------------------
        // EndGameBonusColors — additional cases
        // -----------------------------------------------------------------------

        [Test]
        public void EndGameBonusColors_TwoCompleteColorSets_Returns20()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            // Color 0 in column 0 (rows 0-4) — 5 tiles of color 0
            for (var r = 0; r < 5; r++) ps.Box[r, 0] = 0;
            // Color 1 in column 1 (rows 0-4) — 5 tiles of color 1
            for (var r = 0; r < 5; r++) ps.Box[r, 1] = 1;

            int bonus = Scoring.EndGameBonusColors(ps, (r, c) => (int)ps.Box[r, c]);

            Assert.AreEqual(20, bonus);
        }

        [Test]
        public void EndGameBonusColors_AllFiveColorSets_Returns50()
        {
            var ps = new PlayerState();
            // Fill board so each column holds all 5 tiles of one color
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = (sbyte)c; // col c → color c, 5 tiles of each color

            int bonus = Scoring.EndGameBonusColors(ps, (r, c) => (int)ps.Box[r, c]);

            Assert.AreEqual(50, bonus);
        }

        [Test]
        public void EndGameBonusColors_StandardBoxMap_DiagonalCompletesOneColor()
        {
            // With standard order [0,1,2,3,4], color 0 (Blue) appears at column r in row r.
            // Placing Blue on the main diagonal gives 5 Blue tiles → +10.
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            for (var r = 0; r < 5; r++) ps.Box[r, r] = 0; // Blue on diagonal

            int[] baseOrder = { 0, 1, 2, 3, 4 };
            int bonus = Scoring.EndGameBonusColors(ps, (r, c) => Core.Domain.Rules.StandardBoxMap.ColorAtCell(baseOrder, r, c));

            Assert.AreEqual(10, bonus, "Diagonal Blue tiles complete one color set under standard box map.");
        }
    }

    // -----------------------------------------------------------------------
    // CountCompletedRows
    // -----------------------------------------------------------------------

    [TestFixture]
    public class CountCompletedRowsTests
    {
        static PlayerState EmptyBoard()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            return ps;
        }

        [Test]
        public void CountCompletedRows_EmptyBoard_Returns0()
        {
            var ps = EmptyBoard();

            int count = Scoring.CountCompletedRows(ps);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void CountCompletedRows_OneCompleteRow_Returns1()
        {
            var ps = EmptyBoard();
            for (var c = 0; c < 5; c++) ps.Box[2, c] = (sbyte)c;

            int count = Scoring.CountCompletedRows(ps);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void CountCompletedRows_FiveCompleteRows_Returns5()
        {
            var ps = EmptyBoard();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = (sbyte)c;

            int count = Scoring.CountCompletedRows(ps);

            Assert.AreEqual(5, count);
        }

        [Test]
        public void CountCompletedRows_PartialRow4Of5_NotCounted()
        {
            var ps = EmptyBoard();
            // Row 0: 4 of 5 cells filled
            for (var c = 0; c < 4; c++) ps.Box[0, c] = (sbyte)c;

            int count = Scoring.CountCompletedRows(ps);

            Assert.AreEqual(0, count);
        }

        [Test]
        public void CountCompletedRows_TwoCompleteAndThreePartial_Returns2()
        {
            var ps = EmptyBoard();
            // Rows 1 and 3 complete
            for (var c = 0; c < 5; c++) ps.Box[1, c] = (sbyte)c;
            for (var c = 0; c < 5; c++) ps.Box[3, c] = (sbyte)c;
            // Rows 0, 2, 4 partially filled
            ps.Box[0, 0] = 0;
            ps.Box[2, 2] = 2;
            ps.Box[4, 4] = 4;

            int count = Scoring.CountCompletedRows(ps);

            Assert.AreEqual(2, count);
        }
    }
}
