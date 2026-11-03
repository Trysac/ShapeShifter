using System;
using System.Collections.Generic;

namespace ShapeShifter.Core
{
    /// <summary>An obstacle on the track, from <see cref="Start"/> (inclusive) to <see cref="End"/> (exclusive).</summary>
    public sealed class Obstacle
    {
        public Obstacle(ObstacleKind kind, float start, float end)
        {
            if (end <= start) throw new ArgumentException("An obstacle must end after it starts.");
            Kind = kind;
            Start = start;
            End = end;
            TelegraphX = start - SpeedCurve.At(start) * Tuning.TelegraphSeconds;
        }

        public ObstacleKind Kind { get; }
        public float Start { get; }
        public float End { get; }
        /// <summary>Where the warning sign stands: <see cref="Tuning.TelegraphSeconds"/> ahead at the speed there.</summary>
        public float TelegraphX { get; }
        public float Length => End - Start;
        public Form? RequiredForm => FormRules.RequiredForm(Kind);

        /// <summary>The runner went through in the wrong form (and was hurt, unless invulnerable).</summary>
        public bool Failed { get; internal set; }
        /// <summary>The runner is past it.</summary>
        public bool Resolved { get; internal set; }
        /// <summary>A golem broke it (walls and boulders).</summary>
        public bool Smashed { get; internal set; }
    }

    public enum PickupKind
    {
        Coin,
        Heart,
    }

    public sealed class Pickup
    {
        public Pickup(PickupKind kind, float x, float y)
        {
            Kind = kind;
            X = x;
            Y = y;
        }

        public PickupKind Kind { get; }
        public float X { get; }
        /// <summary>Height of the pickup's centre above the ground.</summary>
        public float Y { get; }
        public bool Taken { get; internal set; }
    }

    /// <summary>
    /// The endless track, generated in chunks of <see cref="Tuning.ChunkLength"/> units. Each chunk
    /// draws from its own generator seeded by (seed, chunk index), and chunks are always built in
    /// order, so a seed always gives the same track however far ahead it is asked for. The first
    /// chunk is empty, the next four teach one form each, then obstacles mix and tighten. Every
    /// gap leaves time to react and shift (<see cref="GapSeconds"/>) at the speed reached there.
    /// </summary>
    public sealed class Track
    {
        static readonly ObstacleKind[] Lessons = { ObstacleKind.Wall, ObstacleKind.Chasm, ObstacleKind.Flood, ObstacleKind.Thicket };
        static readonly ObstacleKind[] Mix = { ObstacleKind.Thicket, ObstacleKind.Chasm, ObstacleKind.Wall, ObstacleKind.Flood, ObstacleKind.Boulder };
        static readonly float[] MixWeights = { 1f, 1f, 1f, 1f, 0.7f };

        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly bool endless;
        int nextChunk;

        public Track(int seed)
        {
            Seed = seed;
            endless = true;
        }

        Track(IEnumerable<Obstacle> fixedObstacles, IEnumerable<Pickup> fixedPickups)
        {
            obstacles.AddRange(fixedObstacles);
            obstacles.Sort((a, b) => a.Start.CompareTo(b.Start));
            if (fixedPickups != null) foreach (Pickup p in fixedPickups) Insert(p);
            GeneratedUntil = float.MaxValue;
        }

        /// <summary>A hand-built track that never grows (tests, menus).</summary>
        public static Track Fixed(IEnumerable<Obstacle> obstacles, IEnumerable<Pickup> pickups = null) => new Track(obstacles, pickups);

        public int Seed { get; }
        public IReadOnlyList<Obstacle> Obstacles => obstacles;
        public IReadOnlyList<Pickup> Pickups => pickups;
        public float GeneratedUntil { get; private set; }

        /// <summary>Seconds of running required between two obstacles: more when a shift is needed between them.</summary>
        public static float GapSeconds(ObstacleKind previous, ObstacleKind next)
        {
            float seconds = FormRules.RequiredForm(previous) == FormRules.RequiredForm(next)
                ? Tuning.SameFormGap
                : Tuning.ShiftCooldown + Tuning.ReactionTime;
            if (previous == ObstacleKind.Boulder || next == ObstacleKind.Boulder)
            {
                seconds = Math.Max(seconds, Tuning.JumpAirtime + 0.3f);
            }
            return seconds;
        }

        public void EnsureUntil(float x)
        {
            if (!endless) return;
            while (GeneratedUntil < x)
            {
                BuildChunk(nextChunk++);
            }
        }

        /// <summary>The obstacle whose span holds x, or null.</summary>
        public Obstacle ObstacleAt(float x)
        {
            int lo = 0, hi = obstacles.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                Obstacle o = obstacles[mid];
                if (x < o.Start) hi = mid - 1;
                else if (x >= o.End) lo = mid + 1;
                else return o;
            }
            return null;
        }

        // ---- Generation ----

        void BuildChunk(int index)
        {
            float start = index * Tuning.ChunkLength;
            float end = start + Tuning.ChunkLength;
            var rng = new Rng(Rng.Combine(Seed, index));
            var added = new List<Pickup>();
            var placed = new List<Obstacle>();
            float cursor = start;
            if (obstacles.Count > 0) cursor = Math.Max(cursor, obstacles[obstacles.Count - 1].End);

            if (index == 0)
            {
                // A calm opening: a line of coins to learn the run.
                CoinLine(added, 10f, 24f, rng);
            }
            else if (index <= Lessons.Length)
            {
                Obstacle lesson = Place(Lessons[index - 1], Math.Max(cursor, start + 18f), rng);
                placed.Add(lesson);
                cursor = lesson.End;
            }
            else
            {
                float difficulty = Math.Min(1f, (index - Lessons.Length - 1) / 40f);
                bool rest = rng.Value() < Lerp(0.2f, 0.06f, difficulty);
                while (!rest)
                {
                    ObstacleKind kind = Pick(rng, placed.Count > 0 ? placed[placed.Count - 1].Kind : (ObstacleKind?)null);
                    float slack = rng.Range(0f, Lerp(1.6f, 0.4f, difficulty)) * SpeedCurve.At(cursor);
                    float earliest = Earliest(kind, cursor + slack);
                    if (earliest > end - 3f) break;
                    Obstacle o = Place(kind, earliest, rng);
                    placed.Add(o);
                    cursor = o.End;
                }
            }

            // Coins: inside obstacles along the line their form travels, and in the gaps between.
            float gapStart = Math.Max(start, obstacles.Count > 0 ? obstacles[obstacles.Count - 1].End : start) + 2f;
            bool heartDue = index >= 8 && index % 10 == 9;
            foreach (Obstacle o in placed)
            {
                Gap(added, gapStart, o.Start - 2f, rng, ref heartDue);
                Inside(added, o);
                gapStart = o.End + 2f;
            }
            if (index > 0) Gap(added, gapStart, end - 2f, rng, ref heartDue);

            obstacles.AddRange(placed);
            foreach (Pickup p in added) Insert(p);
            GeneratedUntil = end;
        }

        float Earliest(ObstacleKind kind, float from)
        {
            Obstacle previous = LastObstacle;
            if (previous == null && from < Tuning.SafeStart) from = Tuning.SafeStart;
            if (previous == null) return from;
            float seconds = GapSeconds(previous.Kind, kind);
            float x = Math.Max(from, previous.End + seconds * SpeedCurve.At(previous.End));
            // The speed keeps rising; step forward until the gap holds at the speed reached there.
            while ((x - previous.End) / SpeedCurve.At(x) < seconds) x += 0.1f;
            return x;
        }

        Obstacle lastPlaced;
        Obstacle LastObstacle => lastPlaced ?? (obstacles.Count > 0 ? obstacles[obstacles.Count - 1] : null);

        Obstacle Place(ObstacleKind kind, float at, Rng rng)
        {
            at = Earliest(kind, at);
            float speed = SpeedCurve.At(at);
            float length;
            switch (kind)
            {
                case ObstacleKind.Thicket: length = rng.Range(3f, 5.5f); break;
                case ObstacleKind.Chasm: length = speed * rng.Range(0.78f, 0.95f); break;
                case ObstacleKind.Flood: length = speed * rng.Range(0.7f, 1.3f); break;
                case ObstacleKind.Wall: length = Tuning.WallLength; break;
                default: length = Tuning.BoulderLength; break;
            }
            float first = (float)Math.Ceiling(at * 100f) / 100f;
            var obstacle = new Obstacle(kind, first, Round(first + length));
            lastPlaced = obstacle;
            return obstacle;
        }

        static ObstacleKind Pick(Rng rng, ObstacleKind? previous)
        {
            float total = 0f;
            for (int i = 0; i < Mix.Length; i++) total += Weight(i, previous);
            float roll = rng.Value() * total;
            for (int i = 0; i < Mix.Length; i++)
            {
                roll -= Weight(i, previous);
                if (roll < 0f) return Mix[i];
            }
            return Mix[Mix.Length - 1];
        }

        // The same obstacle twice in a row is less likely, so most gaps ask for a shift.
        static float Weight(int i, ObstacleKind? previous) => MixWeights[i] * (previous == Mix[i] ? 0.35f : 1f);

        static void Inside(List<Pickup> added, Obstacle o)
        {
            switch (o.Kind)
            {
                case ObstacleKind.Chasm:
                    for (float x = o.Start + 1f; x < o.End - 0.5f; x += 2f) added.Add(new Pickup(PickupKind.Coin, Round(x), Tuning.BirdHover + Tuning.BodyCenter));
                    break;
                case ObstacleKind.Thicket:
                case ObstacleKind.Flood:
                    for (float x = o.Start + 0.75f; x < o.End - 0.5f; x += 1.5f) added.Add(new Pickup(PickupKind.Coin, Round(x), Tuning.BodyCenter));
                    break;
                case ObstacleKind.Boulder:
                    float mid = (o.Start + o.End) / 2f;
                    added.Add(new Pickup(PickupKind.Coin, Round(mid - 1.6f), Tuning.BodyCenter + 1.1f));
                    added.Add(new Pickup(PickupKind.Coin, Round(mid), Tuning.BodyCenter + 1.6f));
                    added.Add(new Pickup(PickupKind.Coin, Round(mid + 1.6f), Tuning.BodyCenter + 1.1f));
                    break;
            }
        }

        void Gap(List<Pickup> added, float from, float to, Rng rng, ref bool heartDue)
        {
            if (to - from < 3f) return;
            if (heartDue)
            {
                added.Add(new Pickup(PickupKind.Heart, Round((from + to) / 2f), Tuning.BodyCenter + 0.2f));
                heartDue = false;
                return;
            }
            if (rng.Value() < 0.55f) CoinLine(added, from, to, rng);
        }

        static void CoinLine(List<Pickup> added, float from, float to, Rng rng)
        {
            int count = Math.Min(rng.Range(3, 7), (int)((to - from) / 1.4f) + 1);
            float center = (from + to) / 2f;
            float first = center - (count - 1) * 0.7f;
            for (int i = 0; i < count; i++) added.Add(new Pickup(PickupKind.Coin, Round(first + i * 1.4f), Tuning.BodyCenter));
        }

        void Insert(Pickup p)
        {
            int i = pickups.Count;
            while (i > 0 && pickups[i - 1].X > p.X) i--;
            pickups.Insert(i, p);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // Whole hundredths keep generated numbers readable in tests and logs.
        static float Round(float x) => (float)Math.Round(x * 100f) / 100f;
    }

    /// <summary>SplitMix64: a small, fast generator that gives the same numbers on every platform.</summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(ulong seed)
        {
            state = seed;
        }

        public static ulong Combine(int seed, int index) => ((ulong)(uint)seed << 32) ^ (uint)index ^ 0x5DEECE66DUL;

        public ulong Next()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value() => (Next() >> 40) / (float)(1UL << 24);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>An int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive) => min + (int)(Next() % (ulong)(maxExclusive - min));
    }
}
