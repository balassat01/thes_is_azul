using System;

namespace Core.Domain.Rules
{
    public static class StandardBoxMap
    {
        public static int ColorAtCell(int[] baseOrder, int row, int col)
        {

            int idx = (col - row) % 5; if (idx < 0) idx += 5;
            return baseOrder[idx];
        }

        public static int ColumnFor(int[] baseOrder, int row, int color)
        {
            int idx = Array.IndexOf(baseOrder, color);
            return (idx + row) % 5;
        }
    }
}
