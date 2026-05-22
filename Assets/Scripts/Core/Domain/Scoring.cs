using System;
using System.Collections.Generic;

namespace Core.Domain
{
    public static class Scoring
    {
        public static int ScorePlacement(PlayerState ps, int row, int col)
        {
            var h = 1;
            for (int c = col - 1; c >= 0 && ps.Box[row, c] >= 0; c--) h++;
            for (int c = col + 1; c < 5 && ps.Box[row, c] >= 0; c++) h++;

            var v = 1;
            for (int r = row - 1; r >= 0 && ps.Box[r, col] >= 0; r--) v++;
            for (int r = row + 1; r < 5 && ps.Box[r, col] >= 0; r++) v++;

            if (h > 1 || v > 1) return (h > 1 ? h : 0) + (v > 1 ? v : 0);
            return 1;
        }

        static readonly int[] FloorPenalty = { -1, -1, -2, -2, -2, -3, -3 };

        public static int ScoreFloor(PlayerState ps)
        {
            var sum = 0;
            for (var i = 0; i < ps.floor.Count && i < 7; i++) sum += FloorPenalty[i];
            return sum;
        }

        public static int EndGameBonusRows(PlayerState ps)
        {
            var bonus = 0;
            for (var r = 0; r < 5; r++)
            {
                var full = true;
                for (var c = 0; c < 5; c++) if (ps.Box[r, c] < 0) { full = false; break; }
                if (full) bonus += 2;
            }
            return bonus;
        }

        public static int CountCompletedRows(PlayerState ps)
        {
            var count = 0;
            for (var r = 0; r < 5; r++)
            {
                var full = true;
                for (var c = 0; c < 5; c++) if (ps.Box[r, c] < 0) { full = false; break; }
                if (full) count++;
            }
            return count;
        }

        public static int EndGameBonusCols(PlayerState ps)
        {
            var bonus = 0;
            for (var c = 0; c < 5; c++)
            {
                var full = true;
                for (var r = 0; r < 5; r++) if (ps.Box[r, c] < 0) { full = false; break; }
                if (full) bonus += 7;
            }
            return bonus;
        }

        public static int EndGameBonusColors(PlayerState ps, Func<int, int, int> colorAtCell)
        {
            var bonus = 0;
            for (var color = 0; color < 5; color++)
            {
                var count = 0;
                for (var r = 0; r < 5; r++)
                    for (var c = 0; c < 5; c++)
                        if (ps.Box[r, c] >= 0 && colorAtCell(r, c) == color) count++;
                if (count == 5) bonus += 10;
            }
            return bonus;
        }

        public static List<int> DetermineWinners(PlayerState[] players)
        {
            var winners = new List<int>();
            if (players == null || players.Length == 0) return winners;

            int maxScore = int.MinValue;
            foreach (var player in players)
            {
                if (player.score > maxScore)
                    maxScore = player.score;
            }

            var tiedPlayers = new List<PlayerState>();
            foreach (var player in players)
            {
                if (player.score == maxScore)
                    tiedPlayers.Add(player);
            }

            if (tiedPlayers.Count == 1)
            {
                winners.Add(tiedPlayers[0].actorNumber);
                return winners;
            }

            int maxRows = int.MinValue;
            foreach (var player in tiedPlayers)
            {
                int completedRows = CountCompletedRows(player);
                if (completedRows > maxRows)
                    maxRows = completedRows;
            }

            foreach (var player in tiedPlayers)
            {
                int completedRows = CountCompletedRows(player);
                if (completedRows == maxRows)
                    winners.Add(player.actorNumber);
            }

            return winners;
        }
    }
}
