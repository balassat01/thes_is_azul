using NUnit.Framework;
using Core.Domain;
using Core.Domain.Rules;

namespace AzulTests
{
    [TestFixture]
    public class GrayBoxRuleTests
    {
        GrayBoxRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = TestHelpers.GrayRule();
        }

        // -----------------------------------------------------------------------
        // Helper — builds a PlayerState with an empty box
        // -----------------------------------------------------------------------

        static PlayerState EmptyBoard()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            return ps;
        }

        // -----------------------------------------------------------------------
        // CanPlace
        // -----------------------------------------------------------------------

        [Test]
        public void CanPlace_EmptyBoard_ReturnsTrueAtColumn0()
        {
            var ps = EmptyBoard();

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Blue, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, col);
        }

        [Test]
        public void CanPlace_Column0Occupied_SkipsToFirstEmptyColumn()
        {
            var ps = EmptyBoard();
            ps.Box[0, 0] = TestHelpers.Brown; // column 0 occupied in target row

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Blue, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, col);
        }

        [Test]
        public void CanPlace_ColorConflictInColumn_SkipsToFirstConflictFreeColumn()
        {
            // Place blue in column 0 of a different row — column 0 now conflicts for blue
            var ps = EmptyBoard();
            ps.Box[1, 0] = TestHelpers.Blue;

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Blue, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, col);
        }

        [Test]
        public void CanPlace_AllColumnsConflicted_ReturnsFalse()
        {
            // Place the same color in each column (in different rows) so no column is valid
            var ps = EmptyBoard();
            for (var c = 0; c < 5; c++)
            {
                // Use a row different from row 0 for each column
                ps.Box[c + 0, c] = TestHelpers.Red;
            }
            // Now row 0 cells are empty, but every column already has Red somewhere

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Red, ps, out int col);

            Assert.IsFalse(ok);
            Assert.AreEqual(-1, col);
        }

        [Test]
        public void CanPlace_CellOccupiedByDifferentColor_StillSkipped()
        {
            var ps = EmptyBoard();
            // Row 2, col 0: occupied by brown; col 1: occupied by white
            ps.Box[2, 0] = TestHelpers.Brown;
            ps.Box[2, 1] = TestHelpers.White;

            bool ok = _rule.CanPlace(row: 2, color: TestHelpers.Blue, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(2, col); // first empty, non-conflicting column
        }

        [Test]
        public void CanPlace_SelectsFirstValidColumnNotFirstEmpty()
        {
            // Col 0 empty but has same color elsewhere in the column; col 1 is clear
            var ps = EmptyBoard();
            ps.Box[3, 0] = TestHelpers.Black; // Black already in col 0

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Black, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, col);
        }

        // -----------------------------------------------------------------------
        // RowHasColor
        // -----------------------------------------------------------------------

        [Test]
        public void RowHasColor_WhenColorPresentInRow_ReturnsTrue()
        {
            var ps = EmptyBoard();
            ps.Box[1, 3] = TestHelpers.White;

            bool result = _rule.RowHasColor(row: 1, color: TestHelpers.White, ps);

            Assert.IsTrue(result);
        }

        [Test]
        public void RowHasColor_WhenColorAbsentFromRow_ReturnsFalse()
        {
            var ps = EmptyBoard();
            ps.Box[1, 3] = TestHelpers.Brown; // different color in the row

            bool result = _rule.RowHasColor(row: 1, color: TestHelpers.White, ps);

            Assert.IsFalse(result);
        }

        [Test]
        public void RowHasColor_EmptyRow_ReturnsFalse()
        {
            var ps = EmptyBoard();

            bool result = _rule.RowHasColor(row: 0, color: TestHelpers.Red, ps);

            Assert.IsFalse(result);
        }

        [Test]
        public void RowHasColor_ColorInDifferentRow_ReturnsFalse()
        {
            var ps = EmptyBoard();
            ps.Box[0, 2] = TestHelpers.Blue; // row 0, not row 1

            bool result = _rule.RowHasColor(row: 1, color: TestHelpers.Blue, ps);

            Assert.IsFalse(result);
        }

        // -----------------------------------------------------------------------
        // GetValidColumns (static utility)
        // -----------------------------------------------------------------------

        [Test]
        public void GetValidColumns_EmptyBoard_AllColumnsValid()
        {
            byte[] boxColors = TestHelpers.EmptyBoxColors();

            bool[] valid = GrayBoxRule.GetValidColumns(row: 0, color: TestHelpers.Blue, boxColors);

            Assert.That(valid, Is.EqualTo(new[] { true, true, true, true, true }));
        }

        [Test]
        public void GetValidColumns_OccupiedCellInRow_ThatColumnFalse()
        {
            byte[] boxColors = TestHelpers.EmptyBoxColors();
            boxColors[0 * 5 + 2] = TestHelpers.Brown; // row 0, col 2 occupied

            bool[] valid = GrayBoxRule.GetValidColumns(row: 0, color: TestHelpers.Blue, boxColors);

            Assert.IsFalse(valid[2], "Column 2 is occupied and must not be valid.");
            Assert.IsTrue(valid[0]);
            Assert.IsTrue(valid[1]);
            Assert.IsTrue(valid[3]);
            Assert.IsTrue(valid[4]);
        }

        [Test]
        public void GetValidColumns_ColorConflictInColumn_ThatColumnFalse()
        {
            // Place Blue in row 3, col 1 → col 1 conflicts for Blue anywhere else
            byte[] boxColors = TestHelpers.EmptyBoxColors();
            boxColors[3 * 5 + 1] = TestHelpers.Blue;

            bool[] valid = GrayBoxRule.GetValidColumns(row: 0, color: TestHelpers.Blue, boxColors);

            Assert.IsFalse(valid[1], "Column 1 already has Blue in another row — must not be valid.");
            Assert.IsTrue(valid[0]);
            Assert.IsTrue(valid[2]);
        }

        [Test]
        public void GetValidColumns_AllColumnsConflicted_AllFalse()
        {
            byte[] boxColors = TestHelpers.EmptyBoxColors();
            // Place the target color in each column (at different rows)
            for (var c = 0; c < 5; c++)
                boxColors[c * 5 + c] = TestHelpers.Red; // row c, col c

            bool[] valid = GrayBoxRule.GetValidColumns(row: 0, color: TestHelpers.Red, boxColors);

            // Row 0: col 0 is occupied by Red (row 0, col 0), others conflict due to color presence
            // Actually row 0 col 0 is occupied (same cell), rest have Red in their column
            Assert.That(valid, Is.EqualTo(new[] { false, false, false, false, false }));
        }

        [Test]
        public void GetValidColumns_KnownBoardState_MatchesExpectedResult()
        {
            // Board: row 1 col 0 = Blue, row 2 col 2 = Red
            // Checking row 0, color White:
            //   col 0: empty, no White conflict → valid
            //   col 1: empty, no conflict → valid
            //   col 2: empty, no conflict → valid
            //   col 3: empty, no conflict → valid
            //   col 4: empty, no conflict → valid
            byte[] boxColors = TestHelpers.EmptyBoxColors();
            boxColors[1 * 5 + 0] = TestHelpers.Blue;
            boxColors[2 * 5 + 2] = TestHelpers.Red;

            bool[] valid = GrayBoxRule.GetValidColumns(row: 0, color: TestHelpers.White, boxColors);

            Assert.That(valid, Is.EqualTo(new[] { true, true, true, true, true }));
        }
    }
}
