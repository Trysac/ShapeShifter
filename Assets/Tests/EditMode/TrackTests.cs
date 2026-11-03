using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    /// <summary>Seeded chunk generation: determinism, the speed curve and the fairness guarantees.</summary>
    public class TrackTests
    {
        static readonly int[] Seeds = { 1, 7, 42, 2021, 99991, 123456789 };
        const float Far = 6000f;

        static string Describe(Track track) =>
            string.Join(";", track.Obstacles.Select(o => $"{o.Kind}@{o.Start:0.###}-{o.End:0.###}")) + "|" +
            string.Join(";", track.Pickups.Select(p => $"{p.Kind}@{p.X:0.###},{p.Y:0.###}"));

        [Test]
        public void TheSameSeedBuildsTheSameTrack()
        {
            var a = new Track(42);
            var b = new Track(42);
            a.EnsureUntil(Far);
            b.EnsureUntil(Far);
            Assert.That(a.Obstacles.Count, Is.GreaterThan(50));
            Assert.That(Describe(a), Is.EqualTo(Describe(b)));
        }

        [Test]
        public void DifferentSeedsBuildDifferentTracks()
        {
            var a = new Track(1);
            var b = new Track(2);
            a.EnsureUntil(Far);
            b.EnsureUntil(Far);
            Assert.That(Describe(a), Is.Not.EqualTo(Describe(b)));
        }

        [Test]
        public void GeneratingInSmallStepsGivesTheSameTrackAsAllAtOnce()
        {
            var whole = new Track(2021);
            whole.EnsureUntil(Far);
            var steps = new Track(2021);
            for (float x = 10f; x <= Far; x += 37f) steps.EnsureUntil(x);
            steps.EnsureUntil(Far);
            Assert.That(steps.GeneratedUntil, Is.EqualTo(whole.GeneratedUntil));
            Assert.That(Describe(steps), Is.EqualTo(Describe(whole)));
        }

        [Test]
        public void ChunksAreGeneratedWholeAndOnlyAsFarAsAsked()
        {
            var track = new Track(5);
            Assert.That(track.GeneratedUntil, Is.EqualTo(0f));
            track.EnsureUntil(1f);
            Assert.That(track.GeneratedUntil, Is.EqualTo(Tuning.ChunkLength));
            track.EnsureUntil(Tuning.ChunkLength * 2.5f);
            Assert.That(track.GeneratedUntil, Is.EqualTo(Tuning.ChunkLength * 3));
        }

        [Test]
        public void TheOpeningIsSafeAndThenTeachesEveryFormInTurn()
        {
            foreach (int seed in Seeds)
            {
                var track = new Track(seed);
                track.EnsureUntil(Far);
                Assert.That(track.Obstacles[0].Start, Is.GreaterThanOrEqualTo(Tuning.SafeStart), $"seed {seed}");
                List<ObstacleKind> gated = track.Obstacles.Where(o => o.Kind != ObstacleKind.Boulder).Take(4).Select(o => o.Kind).ToList();
                Assert.That(gated, Is.EqualTo(new[] { ObstacleKind.Wall, ObstacleKind.Chasm, ObstacleKind.Flood, ObstacleKind.Thicket }), $"seed {seed}");
            }
        }

        [Test]
        public void ObstaclesAreOrderedAndNeverOverlap()
        {
            foreach (int seed in Seeds)
            {
                var track = new Track(seed);
                track.EnsureUntil(Far);
                for (int i = 0; i < track.Obstacles.Count; i++)
                {
                    Obstacle o = track.Obstacles[i];
                    Assert.That(o.End, Is.GreaterThan(o.Start), $"seed {seed} #{i}");
                    if (i > 0) Assert.That(o.Start, Is.GreaterThan(track.Obstacles[i - 1].End), $"seed {seed} #{i}");
                }
            }
        }

        [Test]
        public void ThereIsAlwaysTimeToReactAndShiftBetweenObstacles()
        {
            foreach (int seed in Seeds)
            {
                var track = new Track(seed);
                track.EnsureUntil(Far);
                for (int i = 1; i < track.Obstacles.Count; i++)
                {
                    Obstacle previous = track.Obstacles[i - 1];
                    Obstacle next = track.Obstacles[i];
                    float seconds = (next.Start - previous.End) / SpeedCurve.At(next.Start);
                    Assert.That(seconds, Is.GreaterThanOrEqualTo(Track.GapSeconds(previous.Kind, next.Kind) - 1e-4f),
                        $"seed {seed}: {previous.Kind} then {next.Kind} at {next.Start:0.0}");
                }
            }
        }

        [Test]
        public void ADifferentFormNeedsMoreRoomThanTheSameForm()
        {
            Assert.That(Track.GapSeconds(ObstacleKind.Wall, ObstacleKind.Chasm), Is.EqualTo(Tuning.ShiftCooldown + Tuning.ReactionTime));
            Assert.That(Track.GapSeconds(ObstacleKind.Wall, ObstacleKind.Wall), Is.EqualTo(Tuning.SameFormGap));
            Assert.That(Track.GapSeconds(ObstacleKind.Wall, ObstacleKind.Chasm), Is.GreaterThan(Track.GapSeconds(ObstacleKind.Wall, ObstacleKind.Wall)));
        }

        [Test]
        public void EveryObstacleIsTelegraphedWellBeforeItArrives()
        {
            foreach (int seed in Seeds)
            {
                var track = new Track(seed);
                track.EnsureUntil(Far);
                foreach (Obstacle o in track.Obstacles)
                {
                    float lead = (o.Start - o.TelegraphX) / SpeedCurve.At(o.Start);
                    Assert.That(lead, Is.GreaterThanOrEqualTo(Tuning.TelegraphSeconds - 1e-4f), $"seed {seed} {o.Kind}@{o.Start}");
                    Assert.That(Tuning.TelegraphSeconds, Is.GreaterThan(Tuning.ShiftCooldown + Tuning.ReactionTime));
                }
            }
        }

        [Test]
        public void AChasmIsTooWideToJumpSoOnlyTheBirdCrossesIt()
        {
            foreach (int seed in Seeds)
            {
                var track = new Track(seed);
                track.EnsureUntil(Far);
                foreach (Obstacle o in track.Obstacles.Where(o => o.Kind == ObstacleKind.Chasm))
                {
                    Assert.That(o.End - o.Start, Is.GreaterThan(SpeedCurve.At(o.Start) * Tuning.JumpAirtime), $"seed {seed} chasm@{o.Start}");
                }
            }
        }

        [Test]
        public void PickupsAreOrderedAndStayInsideTheGeneratedTrack()
        {
            var track = new Track(7);
            track.EnsureUntil(Far);
            Assert.That(track.Pickups.Count(p => p.Kind == PickupKind.Coin), Is.GreaterThan(100));
            Assert.That(track.Pickups.Any(p => p.Kind == PickupKind.Heart), Is.True);
            for (int i = 1; i < track.Pickups.Count; i++)
            {
                Assert.That(track.Pickups[i].X, Is.GreaterThanOrEqualTo(track.Pickups[i - 1].X));
            }
            Assert.That(track.Pickups.Last().X, Is.LessThan(track.GeneratedUntil + 20f));
        }

        [Test]
        public void CoinsInsideAnObstacleSitWhereItsFormTravels()
        {
            var track = new Track(99991);
            track.EnsureUntil(Far);
            foreach (Pickup p in track.Pickups)
            {
                Obstacle o = track.ObstacleAt(p.X);
                if (o == null) continue;
                switch (o.Kind)
                {
                    case ObstacleKind.Chasm:
                        Assert.That(p.Y, Is.EqualTo(Tuning.BirdHover + Tuning.BodyCenter).Within(0.01f));
                        break;
                    case ObstacleKind.Thicket:
                    case ObstacleKind.Flood:
                        Assert.That(p.Y, Is.EqualTo(Tuning.BodyCenter).Within(0.01f));
                        break;
                }
            }
        }

        [Test]
        public void TheSpeedStartsAtTheBaseRisesSteadilyAndNeverPassesTheMaximum()
        {
            Assert.That(SpeedCurve.At(0f), Is.EqualTo(Tuning.BaseSpeed));
            float last = SpeedCurve.At(0f);
            for (float d = 50f; d < 8000f; d += 50f)
            {
                float speed = SpeedCurve.At(d);
                Assert.That(speed, Is.GreaterThan(last));
                Assert.That(speed, Is.LessThan(Tuning.MaxSpeed));
                last = speed;
            }
            Assert.That(SpeedCurve.At(5000f), Is.GreaterThan(Tuning.MaxSpeed * 0.95f), "close to the top by 5 km");
            Assert.That(SpeedCurve.At(-10f), Is.EqualTo(Tuning.BaseSpeed));
        }

        [Test]
        public void ObstacleAtFindsTheObstacleUnderAPoint()
        {
            var track = Track.Fixed(new[] { new Obstacle(ObstacleKind.Wall, 10f, 12f), new Obstacle(ObstacleKind.Flood, 20f, 30f) });
            Assert.That(track.ObstacleAt(9.9f), Is.Null);
            Assert.That(track.ObstacleAt(10f).Kind, Is.EqualTo(ObstacleKind.Wall));
            Assert.That(track.ObstacleAt(25f).Kind, Is.EqualTo(ObstacleKind.Flood));
            Assert.That(track.ObstacleAt(30f), Is.Null, "the end is exclusive");
        }
    }
}
