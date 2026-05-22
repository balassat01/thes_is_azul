using System;

namespace Core.Domain.Rules
{
    public sealed class StandardBoxRule : IBoxRule
    {
        readonly int[] _baseOrder;

        public StandardBoxRule(int[] baseOrder)
        {
            if (baseOrder is not { Length: 5 }) throw new ArgumentException();
            _baseOrder = baseOrder;
        }

        public bool CanPlace(int row, int color, PlayerState ps, out int column)
        {
            int idx = Array.IndexOf(_baseOrder, color);
            column = (idx + row) % 5;
            return ps.Box[row, column] < 0;
        }

        public bool RowHasColor(int row, int color, PlayerState ps)
        {
            int idx = Array.IndexOf(_baseOrder, color);
            int col = (idx + row) % 5;
            return ps.Box[row, col] >= 0;
        }
    }
}
