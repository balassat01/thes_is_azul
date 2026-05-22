using System.Linq;
using Core.Domain;
using Core.Domain.Rules;

namespace AzulTests
{
    /// <summary>
    /// Shared factory methods and utilities for Azul unit tests.
    /// All helpers return fresh instances — never share mutable state across tests.
    /// </summary>
    internal static class TestHelpers
    {
        // Color constants matching the domain convention
        // Declared as const int so they implicitly convert to byte, sbyte, or int without casts.
        internal const int Blue  = 0;
        internal const int Brown = 1;
        internal const int White = 2;
        internal const int Black = 3;
        internal const int Red   = 4;
        internal const int Token = 5; // first-player token

        internal static readonly int[] DefaultBaseOrder = { 0, 1, 2, 3, 4 };

        /// <summary>
        /// Creates a fully initialized, ready-to-play GameState at Phase.FactoryOffer.
        /// The RNG returned is a fresh instance seeded identically to the one used during
        /// initialization — callers that need deterministic draws should use this instance.
        /// </summary>
        internal static (GameState state, RNG rng) MakeGame(int playerCount = 2, bool grayBox = false, int seed = 42)
        {
            var state = new GameState();
            var seats = Enumerable.Range(1, playerCount)
                .Select(i => (actor: i, nick: $"P{i}", playerColor: (byte)(i - 1)))
                .ToArray();
            AzulEngine.Initialize(state, seats, grayBox, seed);
            var rng = new RNG(seed);
            AzulEngine.RefillAndBeginOffer(state, rng);
            return (state, rng);
        }

        /// <summary>
        /// Creates a GameState that has been initialized but NOT yet moved to FactoryOffer
        /// (still in Phase.Refill). Useful for testing RefillAndBeginOffer itself.
        /// </summary>
        internal static GameState MakeInitializedState(int playerCount = 2, bool grayBox = false, int seed = 42)
        {
            var state = new GameState();
            var seats = Enumerable.Range(1, playerCount)
                .Select(i => (actor: i, nick: $"P{i}", playerColor: (byte)(i - 1)))
                .ToArray();
            AzulEngine.Initialize(state, seats, grayBox, seed);
            return state;
        }

        /// <summary>Fills a pattern row so it is ready for tiling.</summary>
        internal static void FillPatternRow(PlayerState ps, int row, byte color)
        {
            ps.patternColors[row] = (sbyte)color;
            ps.patternCounts[row] = (byte)(row + 1);
        }

        /// <summary>
        /// Returns a fresh StandardBoxRule using the default base order [0,1,2,3,4].
        /// </summary>
        internal static StandardBoxRule StandardRule() => new StandardBoxRule(DefaultBaseOrder);

        /// <summary>Returns a fresh GrayBoxRule.</summary>
        internal static GrayBoxRule GrayRule() => new GrayBoxRule();

        /// <summary>
        /// Builds a flat byte[25] box-colors array (row-major) with all cells empty (255).
        /// </summary>
        internal static byte[] EmptyBoxColors() => new byte[25].Select(_ => (byte)255).ToArray();

        /// <summary>
        /// Builds a flat byte[25] box-colors array from a PlayerState's Box grid.
        /// Used to bridge the 2-D sbyte[,] domain type to the flat byte[] expected by
        /// GrayBoxRule.GetValidColumns.
        /// </summary>
        internal static byte[] BoxColorsFromPlayerState(PlayerState ps)
        {
            var arr = new byte[25];
            for (var r = 0; r < 5; r++)
                for (var c = 0; c < 5; c++)
                    arr[r * 5 + c] = ps.Box[r, c] >= 0 ? (byte)ps.Box[r, c] : (byte)255;
            return arr;
        }

        /// <summary>
        /// Lambda that maps (row, col) to color using StandardBoxMap with the default base order.
        /// Matches the signature required by AzulEngine.ExecuteBoxTiling and ApplyPlaceInBox.
        /// </summary>
        internal static System.Func<int, int, int> StandardColorMap()
            => (r, c) => StandardBoxMap.ColorAtCell(DefaultBaseOrder, r, c);
    }
}
