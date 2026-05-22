using System.Linq;

namespace Core.Domain
{

    public sealed class RNG
    {
        ulong _state;

        public RNG(int seed)
        {

            _state = (ulong)((long)seed ^ unchecked((long)0x9E3779B97F4A7C15UL));
            if (_state == 0) _state = 0x9E3779B97F4A7C15UL;
        }

        RNG(ulong state) { _state = state == 0 ? 0x9E3779B97F4A7C15UL : state; }

        public long SerializeState() => (long)_state;

        public static RNG FromSerializedState(long state) => new RNG((ulong)state);

        ulong Next()
        {

            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return _state;
        }

        public int NextInt(int exclusiveMax) => (int)(Next() % (ulong)exclusiveMax);

        public int DrawWeighted(int[] counts)
        {
            int sum = counts.Sum();
            if (sum <= 0) return -1;

            int roll = (int)(Next() % (ulong)sum);
            for (var i = 0; i < counts.Length; i++)
            {
                if (roll < counts[i]) return i;
                roll -= counts[i];
            }
            return -1;
        }
    }
}
