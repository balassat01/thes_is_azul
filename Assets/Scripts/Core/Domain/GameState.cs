using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Core.Domain
{
    public enum Phase : byte { FactoryOffer, BoxTiling, Refill, GameOver }

    [Serializable]
    public sealed class GameConfig
    {
        public int playerCount;
        public bool useGrayBox;
        public int turnSeconds;
        public int seed;
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int actorNumber;
        public string nick;
        public byte visualIdColor;

        public sbyte[] patternColors = new sbyte[5];
        public byte[]  patternCounts = new byte[5];

        public sbyte[,] Box = new sbyte[5,5];

        public List<byte> floor = new List<byte>(7);
        public int score;
    }

    [Serializable]
    public sealed class GameState
    {
        public GameConfig config = new GameConfig();
        public Phase phase;
        public int round;
        public int activeActor;
        public int startPlayerActor;
        public bool firstPlayerTokenInCenter;
        public double turnDeadline;

        public List<byte>[] Factories;
        public List<byte> center = new List<byte>(32);

        public int[] bag = new int[5];
        public int[] lid = new int[5];

        public PlayerState[] players;
        public bool endTriggered;
    }
}
