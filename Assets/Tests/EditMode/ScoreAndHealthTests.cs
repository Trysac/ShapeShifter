using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    public class ScoreAndHealthTests
    {
        [Test]
        public void TheTotalAddsWholeMetresCoinsAndBonuses()
        {
            var score = new Score();
            score.AddDistance(12.7f);
            score.AddCoin();
            score.AddCoin();
            score.AddBonus(40);
            Assert.That(score.Distance, Is.EqualTo(12.7f).Within(1e-4f));
            Assert.That(score.Coins, Is.EqualTo(2));
            Assert.That(score.Total, Is.EqualTo(12 * Tuning.DistancePoints + 2 * Tuning.CoinPoints + 40));
        }

        [Test]
        public void TheMultiplierGrowsEveryFewCleanPassesUpToItsCap()
        {
            var score = new Score();
            Assert.That(score.Multiplier, Is.EqualTo(1));
            for (int i = 0; i < Tuning.StreakPerMultiplier; i++) score.AddCleanPass();
            Assert.That(score.Multiplier, Is.EqualTo(2));
            for (int i = 0; i < 100; i++) score.AddCleanPass();
            Assert.That(score.Multiplier, Is.EqualTo(Tuning.MaxMultiplier));
            Assert.That(score.AddCleanPass(), Is.EqualTo(Tuning.PassBonus * Tuning.MaxMultiplier));
        }

        [Test]
        public void BreakingTheStreakResetsTheMultiplierButKeepsTheBest()
        {
            var score = new Score();
            for (int i = 0; i < 7; i++) score.AddCleanPass();
            int bonus = score.Bonus;
            score.BreakStreak();
            Assert.That(score.Streak, Is.EqualTo(0));
            Assert.That(score.Multiplier, Is.EqualTo(1));
            Assert.That(score.BestStreak, Is.EqualTo(7));
            Assert.That(score.Bonus, Is.EqualTo(bonus), "points already earned stay");
        }

        [Test]
        public void DamageCostsOneHeartAndGrantsInvulnerability()
        {
            var health = new Health(5);
            Assert.That(health.Damage(), Is.True);
            Assert.That(health.Hearts, Is.EqualTo(4));
            Assert.That(health.Invulnerable, Is.True);
            Assert.That(health.InvulnerableLeft, Is.EqualTo(Tuning.InvulnerableTime));
            Assert.That(health.Damage(), Is.False, "no damage while invulnerable");
            Assert.That(health.Hearts, Is.EqualTo(4));
            health.Tick(Tuning.InvulnerableTime + 0.01f);
            Assert.That(health.Invulnerable, Is.False);
            Assert.That(health.Damage(), Is.True);
            Assert.That(health.Hearts, Is.EqualTo(3));
        }

        [Test]
        public void TheLastHeartLostMeansDeadAndNothingMoreChanges()
        {
            var health = new Health(2);
            health.Damage();
            health.Tick(10f);
            health.Damage();
            Assert.That(health.Dead, Is.True);
            Assert.That(health.Hearts, Is.EqualTo(0));
            Assert.That(health.Heal(), Is.False);
            health.Tick(10f);
            Assert.That(health.Damage(), Is.False);
            Assert.That(health.Hearts, Is.EqualTo(0));
        }

        [Test]
        public void HealingRestoresOneHeartButNotAboveTheMaximum()
        {
            var health = new Health(5);
            Assert.That(health.Heal(), Is.False);
            health.Damage();
            Assert.That(health.Heal(), Is.True);
            Assert.That(health.Hearts, Is.EqualTo(5));
        }
    }
}
