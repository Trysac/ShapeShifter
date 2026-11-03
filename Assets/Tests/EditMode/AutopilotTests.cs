using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    /// <summary>The strongest fairness check: a player who reacts in time is never hit.</summary>
    public class AutopilotTests
    {
        [TestCase(1)]
        [TestCase(42)]
        [TestCase(2021)]
        [TestCase(31337)]
        [TestCase(987654)]
        public void APlayerWhoReadsTheSignsRunsFiveKilometresWithoutAHit(int seed)
        {
            var run = new Run(seed);
            int passed = 0;
            while (run.Distance < 5000f && !run.Over)
            {
                run.Step(1f / 60f, Autopilot.Decide(run));
                foreach (RunEvent e in run.Events)
                {
                    Assert.That(e.Kind, Is.Not.EqualTo(RunEventKind.Hurt), $"seed {seed}: hit by a {e.Obstacle} at {e.X:0.0}");
                    if (e.Kind == RunEventKind.Passed) passed++;
                }
            }
            Assert.That(run.Health.Hearts, Is.EqualTo(Tuning.MaxHearts));
            Assert.That(passed, Is.GreaterThan(150));
            Assert.That(run.Score.Multiplier, Is.EqualTo(Tuning.MaxMultiplier));
        }

        [Test]
        public void ThePilotWaitsWhenNothingIsNear()
        {
            var run = new Run(Track.Fixed(new[] { new Obstacle(ObstacleKind.Wall, 100f, 101.5f) }));
            RunInput input = Autopilot.Decide(run);
            Assert.That(input.Shift, Is.Null);
            Assert.That(input.Jump, Is.False);
        }
    }
}
