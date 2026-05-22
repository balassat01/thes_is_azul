using NUnit.Framework;
using Core.Domain;
using Core.Domain.Rules;

namespace AzulTests
{
    [TestFixture]
    public class StandardBoxRuleTests
    {
        // Default base order: [Blue=0, Brown=1, White=2, Black=3, Red=4]
        static readonly int[] DefaultOrder = { 0, 1, 2, 3, 4 };
        StandardBoxRule _rule;

        [SetUp]
        public void SetUp()
        {
            _rule = new StandardBoxRule(DefaultOrder);
        }

        static PlayerState EmptyBoard()
        {
            var ps = new PlayerState();
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    ps.Box[r, c] = -1;
            return ps;
        }

        // -----------------------------------------------------------------------
        // CanPlace — column calculation
        // For base order [0,1,2,3,4]:
        //   column = (baseIndex + row) % 5 where baseIndex = color
        // -----------------------------------------------------------------------

        [Test]
        [TestCase(0, 0, 0)] // Blue, row 0 → col (0+0)%5=0
        [TestCase(0, 1, 1)] // Blue, row 1 → col 1
        [TestCase(0, 4, 4)] // Blue, row 4 → col 4
        [TestCase(1, 0, 1)] // Brown, row 0 → col 1
        [TestCase(1, 4, 0)] // Brown, row 4 → col (1+4)%5=0
        [TestCase(4, 0, 4)] // Red, row 0 → col 4
        [TestCase(4, 1, 0)] // Red, row 1 → col (4+1)%5=0
        [TestCase(3, 3, 1)] // Black, row 3 → col (3+3)%5=1
        [TestCase(2, 2, 4)] // White, row 2 → col (2+2)%5=4
        public void CanPlace_EmptyCell_ReturnsCorrectColumn(int color, int row, int expectedCol)
        {
            var ps = EmptyBoard();

            bool ok = _rule.CanPlace(row, color, ps, out int col);

            Assert.IsTrue(ok);
            Assert.AreEqual(expectedCol, col);
        }

        [Test]
        public void CanPlace_OccupiedCell_ReturnsFalse()
        {
            var ps = EmptyBoard();
            // Blue in row 0 goes to col 0; pre-occupy it
            ps.Box[0, 0] = TestHelpers.Blue;

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Blue, ps, out int col);

            Assert.IsFalse(ok);
            Assert.AreEqual(0, col); // column is still computed even when false
        }

        [Test]
        public void CanPlace_DifferentColorOccupiesTargetCell_ReturnsFalse()
        {
            var ps = EmptyBoard();
            // Brown's cell for row 0 is col 1; occupy it with a different color
            ps.Box[0, 1] = TestHelpers.White;

            bool ok = _rule.CanPlace(row: 0, color: TestHelpers.Brown, ps, out int col);

            Assert.IsFalse(ok);
        }

        [Test]
        public void CanPlace_AllFiveColorsRow0_MapsToDistinctColumns()
        {
            // Default order ensures each color maps to a unique column in any given row
            var ps = EmptyBoard();
            var columns = new int[5];
            for (var color = 0; color < 5; color++)
            {
                _rule.CanPlace(row: 0, color, ps, out int col);
                columns[color] = col;
            }

            Assert.That(columns, Is.Unique, "All 5 colors must map to distinct columns in row 0.");
        }

        [Test]
        public void CanPlace_AllFiveColorsRow2_MapsToDistinctColumns()
        {
            var ps = EmptyBoard();
            var columns = new int[5];
            for (var color = 0; color < 5; color++)
            {
                _rule.CanPlace(row: 2, color, ps, out int col);
                columns[color] = col;
            }

            Assert.That(columns, Is.Unique);
        }

        // -----------------------------------------------------------------------
        // RowHasColor
        // -----------------------------------------------------------------------

        [Test]
        public void RowHasColor_WhenColorNotPlacedInRow_ReturnsFalse()
        {
            var ps = EmptyBoard();

            bool result = _rule.RowHasColor(row: 0, color: TestHelpers.Blue, ps);

            Assert.IsFalse(result);
        }

        [Test]
        public void RowHasColor_WhenTargetCellOccupied_ReturnsTrue()
        {
            var ps = EmptyBoard();
            // Blue row 0 → col 0
            ps.Box[0, 0] = TestHelpers.Blue;

            bool result = _rule.RowHasColor(row: 0, color: TestHelpers.Blue, ps);

            Assert.IsTrue(result);
        }

        [Test]
        public void RowHasColor_DifferentColorAtTargetCell_StillReturnsTrue()
        {
            // StandardBoxRule checks if the cell is occupied (regardless of stored color),
            // because the column is deterministic and any tile there blocks the color.
            var ps = EmptyBoard();
            // Blue row 0 → col 0; put White there instead
            ps.Box[0, 0] = TestHelpers.White;

            bool result = _rule.RowHasColor(row: 0, color: TestHelpers.Blue, ps);

            // The cell is occupied — RowHasColor returns true
            Assert.IsTrue(result);
        }

        [Test]
        public void RowHasColor_ColorInDifferentRow_ReturnsFalse()
        {
            var ps = EmptyBoard();
            // Blue in row 1, col 1
            ps.Box[1, 1] = TestHelpers.Blue;

            bool result = _rule.RowHasColor(row: 0, color: TestHelpers.Blue, ps);

            // Row 0 col 0 is still empty
            Assert.IsFalse(result);
        }

        // -----------------------------------------------------------------------
        // Constructor validation
        // -----------------------------------------------------------------------

        [Test]
        public void Constructor_NullBaseOrder_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _ = new StandardBoxRule(null));
        }

        [Test]
        public void Constructor_WrongLengthBaseOrder_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _ = new StandardBoxRule(new[] { 0, 1, 2 }));
        }
    }

    [TestFixture]
    public class StandardBoxMapTests
    {
        static readonly int[] DefaultOrder = { 0, 1, 2, 3, 4 };

        // -----------------------------------------------------------------------
        // ColorAtCell: index = (col - row) mod 5, then baseOrder[index]
        // -----------------------------------------------------------------------

        [Test]
        [TestCase(0, 0, 0)] // (0-0)%5=0 → Blue
        [TestCase(0, 1, 1)] // (1-0)%5=1 → Brown
        [TestCase(0, 4, 4)] // (4-0)%5=4 → Red
        [TestCase(1, 0, 4)] // (0-1)%5=4 → Red
        [TestCase(1, 1, 0)] // (1-1)%5=0 → Blue
        [TestCase(2, 2, 0)] // (2-2)%5=0 → Blue
        [TestCase(3, 1, 3)] // (1-3)%5 = -2+5=3 → Black
        [TestCase(4, 0, 1)] // (0-4)%5 = -4+5=1 → Brown
        public void ColorAtCell_KnownRowCol_ReturnsCorrectColor(int row, int col, int expectedColor)
        {
            int color = StandardBoxMap.ColorAtCell(DefaultOrder, row, col);

            Assert.AreEqual(expectedColor, color);
        }

        [Test]
        public void ColorAtCell_AllCellsInRow_HasEachColorExactlyOnce()
        {
            for (var row = 0; row < 5; row++)
            {
                var seen = new bool[5];
                for (var col = 0; col < 5; col++)
                {
                    int color = StandardBoxMap.ColorAtCell(DefaultOrder, row, col);
                    Assert.That(color, Is.InRange(0, 4));
                    Assert.IsFalse(seen[color], $"Row {row} col {col}: color {color} appeared twice.");
                    seen[color] = true;
                }
            }
        }

        [Test]
        public void ColorAtCell_AllCellsInColumn_HasEachColorExactlyOnce()
        {
            for (var col = 0; col < 5; col++)
            {
                var seen = new bool[5];
                for (var row = 0; row < 5; row++)
                {
                    int color = StandardBoxMap.ColorAtCell(DefaultOrder, row, col);
                    Assert.IsFalse(seen[color], $"Col {col} row {row}: color {color} appeared twice.");
                    seen[color] = true;
                }
            }
        }

        // -----------------------------------------------------------------------
        // ColumnFor: (baseIndex + row) % 5
        // -----------------------------------------------------------------------

        [Test]
        [TestCase(0, 0, 0)] // Blue row 0 → col 0
        [TestCase(0, 1, 1)] // Blue row 1 → col 1
        [TestCase(4, 1, 0)] // Red row 1 → (4+1)%5=0
        [TestCase(3, 3, 1)] // Black row 3 → (3+3)%5=1
        public void ColumnFor_KnownRowColor_ReturnsCorrectColumn(int color, int row, int expectedCol)
        {
            int col = StandardBoxMap.ColumnFor(DefaultOrder, row, color);

            Assert.AreEqual(expectedCol, col);
        }

        [Test]
        public void ColumnFor_AndColorAtCell_AreConsistent()
        {
            // ColorAtCell and ColumnFor must be inverses of each other
            for (var row = 0; row < 5; row++)
            {
                for (var color = 0; color < 5; color++)
                {
                    int col = StandardBoxMap.ColumnFor(DefaultOrder, row, color);
                    int roundTrip = StandardBoxMap.ColorAtCell(DefaultOrder, row, col);
                    Assert.AreEqual(color, roundTrip,
                        $"Round-trip failed for row={row} color={color}: ColumnFor={col} but ColorAtCell={roundTrip}.");
                }
            }
        }

        [Test]
        public void ColumnFor_NonDefaultBaseOrder_UsesCorrectIndex()
        {
            // Reversed order: [4,3,2,1,0]
            int[] reversed = { 4, 3, 2, 1, 0 };
            // Blue (0) is at index 4 in reversed → col = (4 + row) % 5
            int col = StandardBoxMap.ColumnFor(reversed, row: 0, color: 0);
            Assert.AreEqual(4, col);
        }
    }
}
