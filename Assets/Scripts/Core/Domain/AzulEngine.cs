using System;
using System.Collections.Generic;
using System.Linq;
using Core.Domain.Rules;

namespace Core.Domain
{
    public static class AzulEngine
    {
        const int ColorCount = 5;
        const int TilesPerColor = 20;
        const int FloorMax = 7;

        public static void Initialize(GameState state, (int actor, string nick, byte playerColor)[] seats, bool useGrayBox, int seed)
        {
            state.config.playerCount = seats.Length;
            state.config.useGrayBox = useGrayBox;
            state.config.seed = seed;

            state.players = new PlayerState[seats.Length];

            for (var i = 0; i < seats.Length; i++)
            {
                state.players[i] = new PlayerState
                {
                    actorNumber = seats[i].actor,
                    nick = seats[i].nick,
                    visualIdColor = seats[i].playerColor
                };
                for (var r = 0; r < 5; r++) state.players[i].patternColors[r] = -1;

                for (var r = 0; r < 5; r++)
                    for (var c = 0; c < 5; c++)
                        state.players[i].Box[r, c] = -1;
            }

            for (var color = 0; color < ColorCount; color++) state.bag[color] = TilesPerColor;

            int factoryCount = state.config.playerCount switch
            {
                2 => 5,
                3 => 7,
                _ => 9
            };
            state.Factories = new List<byte>[factoryCount];
            for (var i = 0; i < factoryCount; i++) state.Factories[i] = new List<byte>(4);

            state.round = 1;
            state.phase = Phase.Refill;
            state.firstPlayerTokenInCenter = false;
            state.startPlayerActor = seats[0].actor;
            state.activeActor = state.startPlayerActor;
        }

        public static void RefillAndBeginOffer(GameState state, RNG rng)
        {
            if (state.phase != Phase.Refill && state.phase != Phase.GameOver) return;

            foreach (List<byte> factory in state.Factories)
            {
                factory.Clear();
                for (var i = 0; i < 4; i++)
                {
                    int col = DrawFromContainers(state.bag, state.lid, rng);
                    if (col >= 0) factory.Add((byte)col);
                }
            }

            state.center.Clear();
            state.firstPlayerTokenInCenter = true;
            state.activeActor = state.startPlayerActor;
            state.phase = Phase.FactoryOffer;
        }

        static int DrawFromContainers(int[] bag, int[] lid, RNG rng)
        {
            int color = rng.DrawWeighted(bag);
            if (color >= 0) { bag[color]--; return color; }

            var moved = 0;
            for (var i = 0; i < ColorCount; i++) { bag[i] += lid[i]; moved += lid[i]; lid[i] = 0; }
            if (moved == 0) return -1;

            color = rng.DrawWeighted(bag);
            if (color < 0) return -1;
            bag[color]--; return color;
        }

        public static bool ApplyTakeTiles(GameState state, TakeTilesCmd cmd, IBoxRule boxRule, out string error)
        {
            error = null;
            if (state.phase != Phase.FactoryOffer) { error = "Not in draft phase."; return false; }
            if (cmd.Actor != state.activeActor) { error = "Not your turn."; return false; }
            if (cmd.PatternRow is < 0 or > 4) { error = "Invalid row."; return false; }
            PlayerState player = FindPlayer(state, cmd.Actor);
            if (player == null) { error = "No such player."; return false; }

            if (boxRule.RowHasColor(cmd.PatternRow, cmd.Color, player))
            { error = "Color already exists in this Box row."; return false; }

            var take = new List<byte>(8);
            if (cmd.Source == DraftSource.Factory)
            {
                if (cmd.SourceIndex < 0 || cmd.SourceIndex >= state.Factories.Length) { error = "Bad factory."; return false; }
                List<byte> factory = state.Factories[cmd.SourceIndex];
                if (!factory.Contains(cmd.Color)) { error = "Color not present."; return false; }
                for (int i = factory.Count - 1; i >= 0; i--) if (factory[i] == cmd.Color) { take.Add(factory[i]); factory.RemoveAt(i); }
                foreach (byte tile in factory)
                    state.center.Add(tile);
                factory.Clear();
            }
            else
            {
                bool has = state.center.Contains(cmd.Color);
                if (!has) { error = "Color not in center."; return false; }
                if (state.firstPlayerTokenInCenter)
                {
                    state.firstPlayerTokenInCenter = false;
                    state.startPlayerActor = player.actorNumber;
                    player.floor.Add(5);
                }
                for (int i = state.center.Count - 1; i >= 0; i--) if (state.center[i] == cmd.Color) { take.Add(state.center[i]); state.center.RemoveAt(i); }
            }

            if (player.patternColors[cmd.PatternRow] >= 0 && player.patternColors[cmd.PatternRow] != cmd.Color)
            { error = "Pattern line has different color."; return false; }

            int capacity = cmd.PatternRow + 1;
            int free = capacity - player.patternCounts[cmd.PatternRow];
            int place = Math.Min(free, take.Count);
            int overflow = take.Count - place;

            if (player.patternColors[cmd.PatternRow] < 0) player.patternColors[cmd.PatternRow] = (sbyte)cmd.Color;
            player.patternCounts[cmd.PatternRow] += (byte)place;

            int floorFree = FloorMax - player.floor.Count;
            int toFloor = Math.Min(floorFree, overflow);
            for (var i = 0; i < toFloor; i++) player.floor.Add(cmd.Color);
            int toLid = overflow - toFloor;
            state.lid[cmd.Color] += toLid;

            AdvanceActiveToNextSeat(state);
            if (IsMarketEmpty(state))
            {
                state.phase = Phase.BoxTiling;
            }
            return true;
        }

        static bool IsMarketEmpty(GameState state)
        {
            return state.center.Count <= 0 && state.Factories.All(factory => factory.Count <= 0);
        }

        static void AdvanceActiveToNextSeat(GameState state)
        {
            int idx = SeatIndexOf(state, state.activeActor);
            int next = (idx + 1) % state.players.Length;
            state.activeActor = state.players[next].actorNumber;
        }

        static int SeatIndexOf(GameState state, int actor)
        {
            for (var i = 0; i < state.players.Length; i++) if (state.players[i].actorNumber == actor) return i;
            return 0;
        }

        static PlayerState FindPlayer(GameState state, int actor)
        {
            return state.players.FirstOrDefault(player => player.actorNumber == actor);
        }

        public static void ExecuteBoxTiling(GameState state, IBoxRule boxRule, Func<int,int,int> standardMapColorAtCell)
        {
            if (state.phase != Phase.BoxTiling) return;
            state.endTriggered = false;

            foreach (PlayerState player in state.players)
            {
                for (var row = 0; row < 5; row++)
                {
                    int needed = row + 1;
                    if (player.patternCounts[row] != needed) continue;
                    int color = player.patternColors[row];
                    if (!boxRule.CanPlace(row, color, player, out int col)) continue;

                    player.Box[row, col] = (sbyte)color;
                    player.score += Scoring.ScorePlacement(player, row, col);
                    state.lid[color] += needed - 1;
                    player.patternCounts[row] = 0; player.patternColors[row] = -1;

                    var rowFull = true;
                    for (var c = 0; c < 5; c++) if (player.Box[row, c] < 0) { rowFull = false; break; }
                    if (rowFull) state.endTriggered = true;
                }
            }

            FinalizeBoxTilingScoring(state, isGrayBox: false, standardMapColorAtCell);
        }

        public static void BeginGrayBoxTiling(GameState state, Func<int,int,int> standardMapColorAtCell)
        {
            state.endTriggered = false;
            int startSeat = SeatIndexOf(state, state.startPlayerActor);
            for (var i = 0; i < state.players.Length; i++)
            {
                int seat = (startSeat + i) % state.players.Length;
                if (HasFullPatternRows(state.players[seat]))
                {
                    state.activeActor = state.players[seat].actorNumber;
                    return;
                }
            }
            FinalizeBoxTilingScoring(state, isGrayBox: true, standardMapColorAtCell);
        }

        public static bool ApplyPlaceInBox(GameState state, PlaceInBoxCmd cmd,
            Func<int,int,int> standardMapColorAtCell, out string error)
        {
            error = null;
            if (state.phase != Phase.BoxTiling)  { error = "Not in box-tiling phase."; return false; }
            if (cmd.Actor != state.activeActor)   { error = "Not your turn."; return false; }

            PlayerState player = FindPlayer(state, cmd.Actor);
            if (player == null) { error = "No such player."; return false; }

            int row = cmd.PatternRow;
            int col = cmd.Column;
            if (row is < 0 or > 4) { error = "Invalid row."; return false; }
            if (col is < 0 or > 4) { error = "Invalid column."; return false; }

            int needed = row + 1;
            if (player.patternCounts[row] != needed) { error = "Pattern line not full."; return false; }
            if (player.Box[row, col] >= 0)           { error = "Cell already occupied."; return false; }

            int color = player.patternColors[row];
            for (var r = 0; r < 5; r++)
                if (player.Box[r, col] == (sbyte)color) { error = "Color already in this column."; return false; }

            player.Box[row, col] = (sbyte)color;
            player.score += Scoring.ScorePlacement(player, row, col);
            state.lid[color] += needed - 1;
            player.patternCounts[row] = 0; player.patternColors[row] = -1;

            var rowFull = true;
            for (var c = 0; c < 5; c++) if (player.Box[row, c] < 0) { rowFull = false; break; }
            if (rowFull) state.endTriggered = true;

            if (HasFullPatternRows(player)) return true;

            int currentSeat = SeatIndexOf(state, cmd.Actor);
            for (var i = 1; i < state.players.Length; i++)
            {
                int seat = (currentSeat + i) % state.players.Length;
                if (HasFullPatternRows(state.players[seat]))
                {
                    state.activeActor = state.players[seat].actorNumber;
                    return true;
                }
            }

            FinalizeBoxTilingScoring(state, isGrayBox: true, standardMapColorAtCell);
            return true;
        }

        public static bool HasFullPatternRows(PlayerState ps)
        {
            for (var r = 0; r < 5; r++)
                if (ps.patternCounts[r] == r + 1) return true;
            return false;
        }

        static void FinalizeBoxTilingScoring(GameState state, bool isGrayBox, Func<int,int,int> standardMapColorAtCell)
        {
            foreach (PlayerState player in state.players)
            {
                player.score += Scoring.ScoreFloor(player);
                if (player.score < 0) player.score = 0;
                foreach (byte tile in player.floor.Where(t => t <= 4)) state.lid[tile]++;
                player.floor.Clear();
            }

            if (state.endTriggered)
            {
                foreach (PlayerState player in state.players)
                {
                    player.score += Scoring.EndGameBonusRows(player);
                    player.score += Scoring.EndGameBonusCols(player);
                    for (var color = 0; color < 5; color++)
                    {
                        var count = 0;
                        for (var r = 0; r < 5; r++)
                        for (var c = 0; c < 5; c++)
                        {
                            if (player.Box[r, c] < 0) continue;
                            int cellColor = isGrayBox
                                ? (int)player.Box[r, c]
                                : standardMapColorAtCell(r, c);
                            if (cellColor == color) count++;
                        }
                        if (count == 5) player.score += 10;
                    }
                }
                state.phase = Phase.GameOver;
            }
            else
            {
                state.round++;
                state.phase = Phase.Refill;
            }
        }
    }
}
