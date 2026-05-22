namespace Core.Domain
{
    public enum DraftSource : byte { Factory, Center }

    public readonly struct TakeTilesCmd
    {
        public readonly int Actor;
        public readonly DraftSource Source;
        public readonly int SourceIndex;
        public readonly byte Color;
        public readonly int PatternRow;

        public TakeTilesCmd(int actor, DraftSource src, int srcIndex, byte color, int row)
        { Actor = actor; Source = src; SourceIndex = srcIndex; Color = color; PatternRow = row; }
    }

    public readonly struct PlaceInBoxCmd
    {
        public readonly int Actor;
        public readonly int PatternRow;
        public readonly int Column;

        public PlaceInBoxCmd(int actor, int row, int col)
        { Actor = actor; PatternRow = row; Column = col; }
    }
}
