using System;
using System.Collections.Generic;
using Core.Domain;
using ExitGames.Client.Photon;

namespace Netcode
{
    public static class RoomPropsCodec
    {
        public static Hashtable Encode(GameState state, int version, byte[] baseOrderForStandard, long rngState = 0)
        {
            var roomProperties = new Hashtable
            {
                [R.Version]         = version,
                [R.Phase]           = (byte)state.phase,
                [R.Round]           = state.round,
                [R.ActiveActor]     = state.activeActor,
                [R.StartActor]      = state.startPlayerActor,
                [R.FirstInCenter]   = state.firstPlayerTokenInCenter,
                [R.FactoryCount]    = (byte)state.Factories.Length,
                [R.Mode]            = (byte)(state.config.useGrayBox ? 1 : 0),
                [R.Seed]            = state.config.seed,
                [R.BaseOrder]       = baseOrderForStandard,
                [R.TurnDeadline]    = state.turnDeadline
            };

            if (rngState != 0) roomProperties[R.RngState] = rngState;

            int factoryCount = state.Factories.Length;
            var factories = new byte[factoryCount * 4];
            for (var i = 0; i < factories.Length; i++) factories[i] = 255;
            for (var i = 0; i < factoryCount; i++)
                for (var k = 0; k < state.Factories[i].Count && k < 4; k++)
                    factories[i * 4 + k] = state.Factories[i][k];
            roomProperties[R.Factories] = factories;

            roomProperties[R.Center] = state.center.ToArray();

            var bag = new byte[5];
            var lid = new byte[5];
            for (var i = 0; i < 5; i++) { bag[i] = (byte)Math.Clamp(state.bag[i], 0, 255); lid[i] = (byte)Math.Clamp(state.lid[i], 0, 255); }
            roomProperties[R.Bag] = bag; roomProperties[R.Lid] = lid;

            int playerCount = state.players.Length;
            var plColors  = new byte[playerCount * 5];
            var plCounts  = new byte[playerCount * 5];
            var floor     = new byte[playerCount * 7];
            var boxBits   = new byte[playerCount * 5];
            var boxColors = new byte[playerCount * 25];
            var scores    = new short[playerCount];
            var actors    = new int[playerCount];
            var idColors  = new byte[playerCount];

            for (var i = 0; i < playerCount; i++)
            {
                PlayerState player = state.players[i];
                actors[i]   = player.actorNumber;
                idColors[i] = player.visualIdColor;
                scores[i]   = (short)Math.Clamp(player.score, short.MinValue, short.MaxValue);

                for (var r = 0; r < 5; r++)
                {
                    plColors[i * 5 + r] = player.patternColors[r] >= 0 ? (byte)player.patternColors[r] : (byte)255;
                    plCounts[i * 5 + r] = player.patternCounts[r];

                    byte bits = 0;
                    for (var c = 0; c < 5; c++)
                    {
                        byte colorByte = player.Box[r, c] >= 0 ? (byte)player.Box[r, c] : (byte)255;
                        boxColors[i * 25 + r * 5 + c] = colorByte;
                        if (colorByte != 255) bits |= (byte)(1 << c);
                    }
                    boxBits[i * 5 + r] = bits;
                }

                for (var j = 0; j < 7; j++)
                    floor[i * 7 + j] = j < player.floor.Count ? player.floor[j] : (byte)255;
            }

            roomProperties[R.PLColors]  = plColors;
            roomProperties[R.PLCounts]  = plCounts;
            roomProperties[R.Floor]     = floor;
            roomProperties[R.BoxBits]   = boxBits;
            roomProperties[R.BoxColors] = boxColors;
            roomProperties[R.Scores]    = scores;
            roomProperties[R.Actors]    = actors;
            roomProperties[R.IdColors]  = idColors;

            return roomProperties;
        }

        public sealed class Snapshot
        {
            public Phase Phase;
            public int Round;
            public int ActiveActor;
            public int StartActor;
            public bool FirstInCenter;
            public int FactoryCount;
            public List<byte>[] Factories;
            public List<byte> Center;
            public byte[] Bag, Lid;
            public PlayerSnap[] Players;
            public int[] Actors;
            public byte[] IdColors;
            public byte[] BaseOrder;
            public double TurnDeadline;
            public bool IsGrayBox;
        }

        public sealed class PlayerSnap
        {
            public readonly byte[] PatternColors = new byte[5];
            public readonly byte[] PatternCounts = new byte[5];
            public readonly byte[] Floor         = new byte[7];
            public readonly byte[] BoxBytes  = new byte[5];
            public readonly byte[] BoxColors = new byte[25];
            public short Score;
        }

        public static Snapshot Decode(Hashtable roomState)
        {
            var snapshot = new Snapshot
            {
                Phase         = (Phase)(byte)roomState[R.Phase],
                Round         = (int)roomState[R.Round],
                ActiveActor   = (int)roomState[R.ActiveActor],
                StartActor    = (int)roomState[R.StartActor],
                FirstInCenter = (bool)roomState[R.FirstInCenter],
                FactoryCount  = (byte)roomState[R.FactoryCount],
                Center        = new List<byte>((byte[])roomState[R.Center]),
                Bag           = (byte[])roomState[R.Bag],
                Lid           = (byte[])roomState[R.Lid],
                Actors        = (int[])roomState[R.Actors],
                IdColors      = (byte[])roomState[R.IdColors],
                BaseOrder     = (byte[])roomState[R.BaseOrder],
                TurnDeadline  = roomState.ContainsKey(R.TurnDeadline) ? (double)roomState[R.TurnDeadline] : 0.0,
                IsGrayBox     = roomState.ContainsKey(R.Mode) && ((byte)roomState[R.Mode] & 1) != 0
            };

            var factories = (byte[])roomState[R.Factories];
            snapshot.Factories = new List<byte>[snapshot.FactoryCount];
            for (var i = 0; i < snapshot.FactoryCount; i++)
            {
                snapshot.Factories[i] = new List<byte>(4);
                for (var k = 0; k < 4; k++)
                {
                    byte tile = factories[i * 4 + k];
                    if (tile != 255) snapshot.Factories[i].Add(tile);
                }
            }

            var plc    = (byte[])roomState[R.PLColors];
            var plcN   = (byte[])roomState[R.PLCounts];
            var flr    = (byte[])roomState[R.Floor];
            var wbits  = (byte[])roomState[R.BoxBits];
            var scores = (short[])roomState[R.Scores];

            byte[] wcolors = roomState.ContainsKey(R.BoxColors) ? (byte[])roomState[R.BoxColors] : null;

            int playerCount = scores.Length;
            snapshot.Players = new PlayerSnap[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                var ps = new PlayerSnap { Score = scores[i] };
                for (var r = 0; r < 5; r++)
                {
                    ps.PatternColors[r] = plc[i * 5 + r];
                    ps.PatternCounts[r] = plcN[i * 5 + r];
                }
                for (var j = 0; j < 7; j++) ps.Floor[j] = flr[i * 7 + j];

                if (wcolors != null)
                {
                    for (var r = 0; r < 5; r++)
                    {
                        byte bits = 0;
                        for (var c = 0; c < 5; c++)
                        {
                            byte col = wcolors[i * 25 + r * 5 + c];
                            ps.BoxColors[r * 5 + c] = col;
                            if (col != 255) bits |= (byte)(1 << c);
                        }
                        ps.BoxBytes[r] = bits;
                    }
                }
                else
                {
                    for (var r = 0; r < 5; r++) ps.BoxBytes[r] = wbits[i * 5 + r];
                }

                snapshot.Players[i] = ps;
            }

            return snapshot;
        }
    }
}
