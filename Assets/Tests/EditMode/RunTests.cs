using System.Linq;
using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    /// <summary>The run simulation on hand-built tracks: passing, failing, hearts, coins and score.</summary>
    public class RunTests
    {
        const float Step = 1f / 60f;

        static Run On(params Obstacle[] obstacles) => new Run(Track.Fixed(obstacles));

        static Run On(Obstacle[] obstacles, Pickup[] pickups) => new Run(Track.Fixed(obstacles, pickups));

        /// <summary>Steps until the runner reaches x, holding the input; returns every event seen.</summary>
        static System.Collections.Generic.List<RunEvent> RunTo(Run run, float x, RunInput input = default, float limit = 20f)
        {
            var seen = new System.Collections.Generic.List<RunEvent>();
            float t = 0f;
            while (run.X < x && !run.Over && t < limit)
            {
                run.Step(Step, input);
                seen.AddRange(run.Events);
                input = new RunInput(null, false);
                t += Step;
            }
            return seen;
        }

        [Test]
        public void TheRunnerStartsAsAFullHealthFoxAndRunsAtTheBaseSpeed()
        {
            Run run = On();
            Assert.That(run.Form, Is.EqualTo(Form.Fox));
            Assert.That(run.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));
            Assert.That(run.Health.Max, Is.EqualTo(5), "five hearts, as in 2021");
            for (int i = 0; i < 60; i++) run.Step(Step, default);
            Assert.That(run.X, Is.EqualTo(Tuning.BaseSpeed).Within(0.2f));
            Assert.That(run.Distance, Is.EqualTo(run.X).Within(1e-4f));
            Assert.That(run.Speed, Is.EqualTo(SpeedCurve.At(run.Distance)).Within(0.05f));
            Assert.That(run.Elapsed, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void ShiftingEmitsAnEventAndARefusedShiftSaysSo()
        {
            Run run = On();
            run.Step(Step, RunInput.ShiftTo(Form.Bird));
            Assert.That(run.Form, Is.EqualTo(Form.Bird));
            Assert.That(run.Events.Single().Kind, Is.EqualTo(RunEventKind.Shifted));
            Assert.That(run.Events.Single().Form, Is.EqualTo(Form.Bird));
            run.Step(Step, RunInput.ShiftTo(Form.Fish));
            Assert.That(run.Form, Is.EqualTo(Form.Bird));
            Assert.That(run.Events.Single().Kind, Is.EqualTo(RunEventKind.ShiftBlocked));
        }

        [Test]
        public void TheGolemSmashesAWallAndEarnsAPassBonus()
        {
            Run run = On(new Obstacle(ObstacleKind.Wall, 10f, 11.5f));
            run.Step(Step, RunInput.ShiftTo(Form.Golem));
            var events = RunTo(run, 13f);
            Assert.That(events.Count(e => e.Kind == RunEventKind.Smashed), Is.EqualTo(1));
            RunEvent passed = events.Single(e => e.Kind == RunEventKind.Passed);
            Assert.That(passed.Obstacle, Is.EqualTo(ObstacleKind.Wall));
            Assert.That(passed.Points, Is.EqualTo(Tuning.PassBonus));
            Assert.That(run.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));
            Assert.That(run.Score.Streak, Is.EqualTo(1));
            Assert.That(run.Track.Obstacles[0].Failed, Is.False);
            Assert.That(run.Track.Obstacles[0].Resolved, Is.True);
        }

        [Test]
        public void TheWrongFormLosesAHeartAndTheStreakButKeepsRunning()
        {
            Run run = On(new Obstacle(ObstacleKind.Wall, 5f, 6.5f), new Obstacle(ObstacleKind.Wall, 30f, 31.5f));
            run.Step(Step, RunInput.ShiftTo(Form.Golem));
            RunTo(run, 7f);
            Assert.That(run.Score.Streak, Is.EqualTo(1));
            run.Step(Step, RunInput.ShiftTo(Form.Fox));
            var events = RunTo(run, 33f);
            RunEvent hurt = events.Single(e => e.Kind == RunEventKind.Hurt);
            Assert.That(hurt.Obstacle, Is.EqualTo(ObstacleKind.Wall));
            Assert.That(events.Any(e => e.Kind == RunEventKind.Passed), Is.False, "no bonus for a failed obstacle");
            Assert.That(run.Health.Hearts, Is.EqualTo(Tuning.MaxHearts - 1));
            Assert.That(run.Score.Streak, Is.EqualTo(0));
            Assert.That(run.Track.Obstacles[1].Failed, Is.True);
            Assert.That(run.X, Is.GreaterThan(33f), "a hit is a stumble, not a stop");
        }

        [Test]
        public void AHitGivesAShortInvulnerabilityThatThenWearsOff()
        {
            float near = 5f + Tuning.BaseSpeed * Tuning.InvulnerableTime * 0.5f;
            float later = near + Tuning.BaseSpeed * Tuning.InvulnerableTime * 1.5f;
            Run run = On(new Obstacle(ObstacleKind.Wall, 5f, 6f), new Obstacle(ObstacleKind.Wall, near, near + 1f), new Obstacle(ObstacleKind.Wall, later, later + 1f));
            RunTo(run, 6.5f);
            Assert.That(run.Health.Hearts, Is.EqualTo(4));
            Assert.That(run.Health.Invulnerable, Is.True);
            RunTo(run, near + 1.5f);
            Assert.That(run.Health.Hearts, Is.EqualTo(4), "the second wall is passed through while invulnerable");
            Assert.That(run.Track.Obstacles[1].Failed, Is.True);
            RunTo(run, later - 0.5f);
            Assert.That(run.Health.Invulnerable, Is.False, "worn off before the third wall");
            RunTo(run, later + 1.5f);
            Assert.That(run.Health.Hearts, Is.EqualTo(3));
        }

        [Test]
        public void ANonBirdFallsIntoAChasmAndIsCarriedAcrossHurt()
        {
            Run run = On(new Obstacle(ObstacleKind.Chasm, 8f, 16f));
            var events = RunTo(run, 9f);
            RunEvent fell = events.Single(e => e.Kind == RunEventKind.Fell);
            Assert.That(fell.Obstacle, Is.EqualTo(ObstacleKind.Chasm));
            Assert.That(events.Any(e => e.Kind == RunEventKind.Hurt), Is.True);
            Assert.That(run.X, Is.GreaterThanOrEqualTo(16f), "back on solid ground past the chasm");
            Assert.That(run.Y, Is.EqualTo(0f));
            Assert.That(run.Health.Hearts, Is.EqualTo(4));
        }

        [Test]
        public void TheBirdGlidesOverAChasm()
        {
            Run run = On(new Obstacle(ObstacleKind.Chasm, 8f, 16f));
            run.Step(Step, RunInput.ShiftTo(Form.Bird));
            var events = RunTo(run, 17f);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Fell || e.Kind == RunEventKind.Hurt), Is.False);
            Assert.That(events.Single(e => e.Kind == RunEventKind.Passed).Obstacle, Is.EqualTo(ObstacleKind.Chasm));
            Assert.That(run.Y, Is.EqualTo(Tuning.BirdHover).Within(0.01f), "the bird flies low");
        }

        [Test]
        public void TheFishSwimsAFloodButLeavingFishFormInsideHurts()
        {
            Run clean = On(new Obstacle(ObstacleKind.Flood, 6f, 14f));
            clean.Step(Step, RunInput.ShiftTo(Form.Fish));
            Assert.That(RunTo(clean, 15f).Single(e => e.Kind == RunEventKind.Passed).Obstacle, Is.EqualTo(ObstacleKind.Flood));
            Assert.That(clean.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));

            Run early = On(new Obstacle(ObstacleKind.Flood, 6f, 14f));
            early.Step(Step, RunInput.ShiftTo(Form.Fish));
            RunTo(early, 9f);
            early.Step(Step, RunInput.ShiftTo(Form.Fox));
            var events = early.Events.ToList();
            events.AddRange(RunTo(early, 15f));
            Assert.That(events.Single(e => e.Kind == RunEventKind.Hurt).Obstacle, Is.EqualTo(ObstacleKind.Flood));
            Assert.That(events.Any(e => e.Kind == RunEventKind.Passed), Is.False);
        }

        [Test]
        public void OnlyTheFoxSlipsThroughAThicket()
        {
            Run fox = On(new Obstacle(ObstacleKind.Thicket, 6f, 10f));
            Assert.That(RunTo(fox, 11f).Any(e => e.Kind == RunEventKind.Passed), Is.True);
            Assert.That(fox.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));

            Run bird = On(new Obstacle(ObstacleKind.Thicket, 6f, 10f));
            bird.Step(Step, RunInput.ShiftTo(Form.Bird));
            Assert.That(RunTo(bird, 11f).Any(e => e.Kind == RunEventKind.Hurt), Is.True);
        }

        [Test]
        public void ABoulderIsJumpedOrSmashedButHurtsAGroundedFox()
        {
            Run grounded = On(new Obstacle(ObstacleKind.Boulder, 8f, 9f));
            Assert.That(RunTo(grounded, 10f).Any(e => e.Kind == RunEventKind.Hurt), Is.True);

            Run jumper = On(new Obstacle(ObstacleKind.Boulder, 8f, 9f));
            RunTo(jumper, 8f - Tuning.BaseSpeed * 0.2f);
            jumper.Step(Step, RunInput.JumpNow);
            Assert.That(jumper.Events.Any(e => e.Kind == RunEventKind.Jumped), Is.True);
            var events = RunTo(jumper, 10f);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Hurt), Is.False);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Passed), Is.True);

            Run golem = On(new Obstacle(ObstacleKind.Boulder, 8f, 9f));
            golem.Step(Step, RunInput.ShiftTo(Form.Golem));
            events = RunTo(golem, 10f);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Smashed), Is.True);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Hurt), Is.False);
        }

        [Test]
        public void AJumpRisesAndLandsButTheGolemCannotJump()
        {
            Run run = On();
            run.Step(Step, RunInput.JumpNow);
            Assert.That(run.Grounded, Is.False);
            var events = RunTo(run, run.X + Tuning.BaseSpeed * (Tuning.JumpAirtime + 0.1f));
            Assert.That(events.Count(e => e.Kind == RunEventKind.Landed), Is.EqualTo(1));
            Assert.That(run.Grounded, Is.True);
            Assert.That(run.Y, Is.EqualTo(0f));

            run.Step(Step, RunInput.ShiftTo(Form.Golem));
            run.Step(Step, RunInput.JumpNow);
            Assert.That(run.Events.Any(e => e.Kind == RunEventKind.Jumped), Is.False);
            Assert.That(run.Y, Is.EqualTo(0f));
        }

        [Test]
        public void AJumpIsOnlyAllowedFromTheGround()
        {
            Run run = On();
            run.Step(Step, RunInput.JumpNow);
            run.Step(Step, default);
            float vy = run.VelocityY;
            run.Step(Step, RunInput.JumpNow);
            Assert.That(run.Events.Any(e => e.Kind == RunEventKind.Jumped), Is.False);
            Assert.That(run.VelocityY, Is.LessThan(vy), "gravity keeps pulling; no double jump");
        }

        [Test]
        public void LosingTheFifthHeartEndsTheRunAndFreezesIt()
        {
            var walls = Enumerable.Range(0, 6).Select(i => new Obstacle(ObstacleKind.Wall, 5f + i * 20f, 6f + i * 20f)).ToArray();
            Run run = On(walls);
            var events = RunTo(run, 200f, default, 60f);
            Assert.That(run.Over, Is.True);
            Assert.That(run.Health.Hearts, Is.EqualTo(0));
            Assert.That(events.Count(e => e.Kind == RunEventKind.Hurt), Is.EqualTo(5));
            Assert.That(events.Last().Kind, Is.EqualTo(RunEventKind.GameOver));
            float x = run.X;
            run.Step(Step, RunInput.ShiftTo(Form.Golem));
            Assert.That(run.X, Is.EqualTo(x));
            Assert.That(run.Events, Is.Empty);
        }

        [Test]
        public void EndingARunEarlyKeepsItsScoreAndStopsIt()
        {
            Run run = On(new Obstacle[0], new[] { new Pickup(PickupKind.Coin, 3f, Tuning.BodyCenter) });
            RunTo(run, 10f);
            int total = run.Score.Total;
            run.EndNow();
            Assert.That(run.Over, Is.True);
            Assert.That(run.Events.Single().Kind, Is.EqualTo(RunEventKind.GameOver));
            Assert.That(run.Score.Total, Is.EqualTo(total));
            Assert.That(run.Score.Coins, Is.EqualTo(1));
            Assert.That(run.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));
            run.EndNow();
            Assert.That(run.Events, Is.Empty, "ending twice does nothing");
        }

        [Test]
        public void CoinsAreCollectedOnTheRunnersPathButNotFarAboveIt()
        {
            Run run = On(new Obstacle[0], new[] { new Pickup(PickupKind.Coin, 5f, Tuning.BodyCenter), new Pickup(PickupKind.Coin, 8f, 3.2f) });
            var events = RunTo(run, 10f);
            Assert.That(events.Count(e => e.Kind == RunEventKind.Coin), Is.EqualTo(1));
            Assert.That(run.Score.Coins, Is.EqualTo(1));
            Assert.That(run.Track.Pickups[0].Taken, Is.True);
            Assert.That(run.Track.Pickups[1].Taken, Is.False);
            Assert.That(run.Score.Total, Is.EqualTo((int)run.Distance * Tuning.DistancePoints + Tuning.CoinPoints));
        }

        [Test]
        public void AHeartHealsAHurtRunnerOrPaysABonusAtFullHealth()
        {
            Run hurt = On(new[] { new Obstacle(ObstacleKind.Wall, 3f, 4f) }, new[] { new Pickup(PickupKind.Heart, 10f, Tuning.BodyCenter) });
            RunTo(hurt, 5f);
            Assert.That(hurt.Health.Hearts, Is.EqualTo(4));
            var events = RunTo(hurt, 11f);
            Assert.That(events.Any(e => e.Kind == RunEventKind.Heart), Is.True);
            Assert.That(hurt.Health.Hearts, Is.EqualTo(5));
            Assert.That(hurt.Score.Bonus, Is.EqualTo(0));

            Run full = On(new Obstacle[0], new[] { new Pickup(PickupKind.Heart, 10f, Tuning.BodyCenter) });
            RunTo(full, 11f);
            Assert.That(full.Health.Hearts, Is.EqualTo(5));
            Assert.That(full.Score.Bonus, Is.EqualTo(Tuning.HeartBonus));
        }

        [Test]
        public void AStreakOfCleanPassesRaisesTheMultiplier()
        {
            var thickets = Enumerable.Range(0, 4).Select(i => new Obstacle(ObstacleKind.Thicket, 5f + i * 10f, 7f + i * 10f)).ToArray();
            Run run = On(thickets);
            var passes = RunTo(run, 50f).Where(e => e.Kind == RunEventKind.Passed).ToList();
            Assert.That(passes.Select(p => p.Points), Is.EqualTo(new[] { Tuning.PassBonus, Tuning.PassBonus, Tuning.PassBonus, Tuning.PassBonus * 2 }));
            Assert.That(run.Score.Multiplier, Is.EqualTo(2));
            Assert.That(run.Score.BestStreak, Is.EqualTo(4));
        }

        [Test]
        public void TheSeededRunGeneratesTrackAheadOfTheRunner()
        {
            var run = new Run(77);
            run.Step(Step, default);
            Assert.That(run.Track.GeneratedUntil, Is.GreaterThanOrEqualTo(run.X + Tuning.LookAhead));
            Assert.That(run.Track.Seed, Is.EqualTo(77));
            Assert.That(run.NextObstacle, Is.SameAs(run.Track.Obstacles[0]));
        }
    }
}
