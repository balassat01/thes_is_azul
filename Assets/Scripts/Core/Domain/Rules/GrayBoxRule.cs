namespace Core.Domain.Rules
{
    public sealed class GrayBoxRule : IBoxRule
    {

        public static bool[] GetValidColumns(int row, int color, byte[] boxColors)
        {
            var result = new bool[5];
            for (var col = 0; col < 5; col++)
            {
                if (boxColors[row * 5 + col] != 255) continue;
                bool conflict = false;
                for (var r = 0; r < 5; r++)
                    if (boxColors[r * 5 + col] == (byte)color) { conflict = true; break; }
                result[col] = !conflict;
            }
            return result;
        }

        public bool CanPlace(int row, int color, PlayerState ps, out int column)
        {
            for (column = 0; column < 5; column++)
            {
                if (ps.Box[row, column] >= 0) continue;

                bool conflict = false;
                for (var r = 0; r < 5; r++)
                    if (ps.Box[r, column] == (sbyte)color) { conflict = true; break; }

                if (!conflict) return true;
            }
            column = -1;
            return false;
        }

        public bool RowHasColor(int row, int color, PlayerState ps)
        {
            for (var c = 0; c < 5; c++)
                if (ps.Box[row, c] >= 0 && ps.Box[row, c] == (sbyte)color)
                    return true;
            return false;
        }
    }
}
