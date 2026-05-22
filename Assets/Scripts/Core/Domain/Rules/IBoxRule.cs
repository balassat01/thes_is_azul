namespace Core.Domain.Rules
{
    public interface IBoxRule
    {
        bool CanPlace(int row, int color, PlayerState ps, out int column);
        bool RowHasColor(int row, int color, PlayerState ps);
    }
}
