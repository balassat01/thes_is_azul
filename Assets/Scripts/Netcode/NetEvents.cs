using System;

namespace Netcode
{
    public static class Ev
    {
        public const byte CmdTakeTiles  = 1;
        public const byte CmdPlaceInBox = 2;
    }

    public static class R
    {
        public const byte Version       = 1;
        public const byte Phase         = 2;
        public const byte Round         = 3;
        public const byte ActiveActor   = 4;
        public const byte Floor         = 5;
        public const byte Actors        = 7;
        public const byte IdColors      = 8;
        public const byte StartActor    = 10;
        public const byte Factories     = 11;
        public const byte Center        = 12;
        public const byte Bag           = 13;
        public const byte Lid           = 20;
        public const byte PLColors      = 21;
        public const byte PLCounts      = 22;
        public const byte BoxBits       = 23;
        public const byte Scores        = 24;
        public const byte FirstInCenter = 25;
        public const byte FactoryCount  = 26;
        public const byte TurnDeadline  = 27;
        public const byte Mode          = 28;
        public const byte Seed          = 29;
        public const byte BaseOrder     = 30;
        public const byte BoxColors     = 31;
        public const byte RngState      = 32;
    }

    public static class NetEvents
    {
        public static event Action<RoomPropsCodec.Snapshot> OnSnapshot;

        internal static void RaiseSnapshotUpdate(RoomPropsCodec.Snapshot snapshot)
        {
            OnSnapshot?.Invoke(snapshot);
        }
    }
}
