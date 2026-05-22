using NUnit.Framework;
using Core.Domain;

namespace AzulTests
{
    [TestFixture]
    public class RNGTests
    {
        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void SeededRNG_ProducesIdenticalSequence_WhenCreatedWithSameSeed()
        {
            const int seed = 12345;
            var rng1 = new RNG(seed);
            var rng2 = new RNG(seed);

            var draws1 = new int[10];
            var draws2 = new int[10];
            int[] counts = { 10, 10, 10, 10, 10 };

            for (var i = 0; i < 10; i++)
            {
                draws1[i] = rng1.DrawWeighted(new[] { 10, 10, 10, 10, 10 });
                draws2[i] = rng2.DrawWeighted(new[] { 10, 10, 10, 10, 10 });
            }

            Assert.That(draws1, Is.EqualTo(draws2));
        }

        [Test]
        public void SeededRNG_DifferentSeeds_ProduceDifferentSequences()
        {
            var rng1 = new RNG(1);
            var rng2 = new RNG(2);

            // Draw 20 values; statistically should differ
            bool anyDifference = false;
            for (var i = 0; i < 20; i++)
            {
                int a = rng1.DrawWeighted(new[] { 10, 10, 10, 10, 10 });
                int b = rng2.DrawWeighted(new[] { 10, 10, 10, 10, 10 });
                if (a != b) { anyDifference = true; break; }
            }

            Assert.IsTrue(anyDifference, "Two different seeds must produce at least one different draw in 20 attempts.");
        }

        // -----------------------------------------------------------------------
        // DrawWeighted — correctness
        // -----------------------------------------------------------------------

        [Test]
        public void DrawWeighted_AllZeroCounts_ReturnsMinus1()
        {
            var rng = new RNG(99);
            int result = rng.DrawWeighted(new[] { 0, 0, 0, 0, 0 });

            Assert.AreEqual(-1, result);
        }

        [Test]
        public void DrawWeighted_SingleNonzeroBucket_AlwaysReturnsThatBucket()
        {
            var rng = new RNG(7);
            int[] counts = { 0, 0, 42, 0, 0 };

            for (var i = 0; i < 50; i++)
            {
                int result = rng.DrawWeighted(counts);
                Assert.AreEqual(2, result, "With only bucket 2 non-zero, must always return 2.");
            }
        }

        [Test]
        public void DrawWeighted_ReturnedIndex_IsWithinValidRange()
        {
            var rng = new RNG(55);
            int[] counts = { 5, 5, 5, 5, 5 };

            for (var i = 0; i < 100; i++)
            {
                int result = rng.DrawWeighted(counts);
                Assert.That(result, Is.InRange(0, 4), "DrawWeighted must return a valid color index 0-4.");
            }
        }

        [Test]
        public void DrawWeighted_EmptiesBucket_WhenCalledWithCount1()
        {
            // Verify the return value is exactly the bucket that had count=1,
            // and that repeated calls with that same array (if caller decrements) drain it.
            var rng = new RNG(1);
            int[] counts = { 0, 1, 0, 0, 0 };
            int result = rng.DrawWeighted(counts);

            Assert.AreEqual(1, result);
        }

        [Test]
        public void DrawWeighted_OnlyLastBucketNonzero_ReturnsIndex4()
        {
            var rng = new RNG(42);
            int[] counts = { 0, 0, 0, 0, 20 };

            for (var i = 0; i < 30; i++)
            {
                int result = rng.DrawWeighted(counts);
                Assert.AreEqual(4, result);
            }
        }

        [Test]
        public void DrawWeighted_OnlyFirstBucketNonzero_ReturnsIndex0()
        {
            var rng = new RNG(42);
            int[] counts = { 15, 0, 0, 0, 0 };

            for (var i = 0; i < 30; i++)
            {
                int result = rng.DrawWeighted(counts);
                Assert.AreEqual(0, result);
            }
        }

        [Test]
        public void DrawWeighted_AllColorsDrawnOverManyTrials()
        {
            // With equal weights, all 5 colors should appear in 200 draws
            var rng = new RNG(9999);
            var seen = new bool[5];
            int[] counts = { 20, 20, 20, 20, 20 };

            for (var i = 0; i < 200; i++)
            {
                // Rebuild counts each draw since DrawWeighted doesn't consume
                int result = rng.DrawWeighted(counts);
                if (result >= 0 && result < 5) seen[result] = true;
            }

            for (var c = 0; c < 5; c++)
                Assert.IsTrue(seen[c], $"Color {c} was never drawn in 200 trials with equal weights.");
        }

        // -----------------------------------------------------------------------
        // SerializeState / FromSerializedState
        // -----------------------------------------------------------------------

        [Test]
        public void SerializeState_ThenFromSerializedState_ResumesIdenticalSequence()
        {
            var rng = new RNG(12);
            // Advance the RNG several steps first
            int[] counts = { 10, 10, 10, 10, 10 };
            for (var i = 0; i < 7; i++) rng.DrawWeighted(counts);

            // Serialize mid-sequence
            long serialized = rng.SerializeState();

            // Record 10 future draws from original
            var expected = new int[10];
            for (var i = 0; i < 10; i++) expected[i] = rng.DrawWeighted(counts);

            // Restore and reproduce
            var restored = RNG.FromSerializedState(serialized);
            var actual = new int[10];
            for (var i = 0; i < 10; i++) actual[i] = restored.DrawWeighted(counts);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void FromSerializedState_ZeroState_StillProducesValidSequence()
        {
            // Zero is an illegal xorshift64 state; the RNG must substitute a safe default
            var rng = RNG.FromSerializedState(0L);
            int[] counts = { 5, 5, 5, 5, 5 };

            // Should not throw and should return a valid index
            int result = rng.DrawWeighted(counts);
            Assert.That(result, Is.InRange(0, 4));
        }

        [Test]
        public void SerializeState_FreshRNG_CanBeRestoredBeforeAnyDraw()
        {
            var rng1 = new RNG(777);
            long state = rng1.SerializeState();

            var rng2 = RNG.FromSerializedState(state);

            int[] counts = { 10, 10, 10, 10, 10 };
            int a = rng1.DrawWeighted(counts);
            int b = rng2.DrawWeighted(counts);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void SerializeState_DifferentPointsInSequence_ProduceDifferentValues()
        {
            var rng1 = new RNG(5);
            var rng2 = new RNG(5);
            int[] counts = { 10, 10, 10, 10, 10 };

            // Advance rng2 by one step
            rng2.DrawWeighted(counts);

            long s1 = rng1.SerializeState();
            long s2 = rng2.SerializeState();

            Assert.AreNotEqual(s1, s2, "RNGs at different sequence positions must produce different serialized states.");
        }
    }

    // -----------------------------------------------------------------------
    // NextInt
    // -----------------------------------------------------------------------

    [TestFixture]
    public class RNGNextIntTests
    {
        [Test]
        public void NextInt_Max1_AlwaysReturns0()
        {
            var rng = new RNG(42);
            for (var i = 0; i < 50; i++)
                Assert.AreEqual(0, rng.NextInt(1), "NextInt(1) must always return 0.");
        }

        [Test]
        public void NextInt_ResultAlwaysInRange()
        {
            var rng = new RNG(7);
            for (var i = 0; i < 200; i++)
            {
                int result = rng.NextInt(10);
                Assert.That(result, Is.InRange(0, 9), "NextInt(10) must return values 0-9 inclusive.");
            }
        }

        [Test]
        public void NextInt_SameSeed_ProducesIdenticalSequence()
        {
            var rng1 = new RNG(99);
            var rng2 = new RNG(99);

            for (var i = 0; i < 20; i++)
                Assert.AreEqual(rng1.NextInt(100), rng2.NextInt(100),
                    "Same seed must produce identical NextInt sequence.");
        }

        [Test]
        public void NextInt_LargeMax_NeverReturnsNegative()
        {
            var rng = new RNG(12345);
            for (var i = 0; i < 100; i++)
                Assert.GreaterOrEqual(rng.NextInt(1_000_000), 0, "NextInt must never return a negative value.");
        }

        [Test]
        public void NextInt_AllValuesReachable_SmallRange()
        {
            // With max=5 and 500 draws, all values 0-4 should appear
            var rng = new RNG(55);
            var seen = new bool[5];
            for (var i = 0; i < 500; i++) seen[rng.NextInt(5)] = true;

            for (var v = 0; v < 5; v++)
                Assert.IsTrue(seen[v], $"Value {v} was never produced by NextInt(5) in 500 draws.");
        }
    }
}
