using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    /// <summary>Which form passes which obstacle, and how shifting and its cooldown work.</summary>
    public class FormTests
    {
        [TestCase(ObstacleKind.Thicket, Form.Fox)]
        [TestCase(ObstacleKind.Chasm, Form.Bird)]
        [TestCase(ObstacleKind.Wall, Form.Golem)]
        [TestCase(ObstacleKind.Flood, Form.Fish)]
        public void EachGatedObstacleNamesExactlyOneForm(ObstacleKind kind, Form form)
        {
            Assert.That(FormRules.RequiredForm(kind), Is.EqualTo(form));
            foreach (Form other in FormRules.All)
            {
                Assert.That(FormRules.Passes(kind, other, 0f), Is.EqualTo(other == form), $"{other} on the ground at a {kind}");
            }
        }

        [Test]
        public void ABoulderIsJumpedByAnyFormOrSmashedByTheGolem()
        {
            Assert.That(FormRules.RequiredForm(ObstacleKind.Boulder), Is.Null);
            Assert.That(FormRules.Passes(ObstacleKind.Boulder, Form.Golem, 0f), Is.True, "the golem smashes it");
            foreach (Form form in new[] { Form.Fox, Form.Bird, Form.Fish })
            {
                Assert.That(FormRules.Passes(ObstacleKind.Boulder, form, 0f), Is.False, $"{form} on the ground");
                Assert.That(FormRules.Passes(ObstacleKind.Boulder, form, Tuning.BoulderHeight + 0.01f), Is.True, $"{form} above it");
            }
        }

        [Test]
        public void BeingAirborneCarriesAnyFormOverAChasmButNotThroughAWallThicketOrFlood()
        {
            foreach (Form form in FormRules.All)
            {
                Assert.That(FormRules.Passes(ObstacleKind.Chasm, form, 1f), Is.True, form.ToString());
            }
            Assert.That(FormRules.Passes(ObstacleKind.Wall, Form.Fox, 2f), Is.False);
            Assert.That(FormRules.Passes(ObstacleKind.Thicket, Form.Bird, 1f), Is.False);
            Assert.That(FormRules.Passes(ObstacleKind.Flood, Form.Fox, 1f), Is.False);
        }

        [Test]
        public void OnlyTheGolemCannotJump()
        {
            Assert.That(FormRules.CanJump(Form.Fox), Is.True);
            Assert.That(FormRules.CanJump(Form.Bird), Is.True);
            Assert.That(FormRules.CanJump(Form.Fish), Is.True);
            Assert.That(FormRules.CanJump(Form.Golem), Is.False);
        }

        [Test]
        public void TheRunnerStartsAsAFoxWithNoCooldown()
        {
            var shifter = new Shifter();
            Assert.That(shifter.Form, Is.EqualTo(Form.Fox));
            Assert.That(shifter.Cooldown, Is.EqualTo(0f));
            Assert.That(shifter.Ready, Is.True);
        }

        [Test]
        public void ShiftingChangesFormAndStartsTheCooldown()
        {
            var shifter = new Shifter();
            Assert.That(shifter.TryShift(Form.Golem), Is.EqualTo(ShiftResult.Shifted));
            Assert.That(shifter.Form, Is.EqualTo(Form.Golem));
            Assert.That(shifter.Cooldown, Is.EqualTo(Tuning.ShiftCooldown));
            Assert.That(shifter.Ready, Is.False);
            Assert.That(shifter.CooldownFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ShiftingDuringTheCooldownIsRefusedAndKeepsTheForm()
        {
            var shifter = new Shifter();
            shifter.TryShift(Form.Bird);
            shifter.Tick(Tuning.ShiftCooldown * 0.5f);
            Assert.That(shifter.TryShift(Form.Fish), Is.EqualTo(ShiftResult.CoolingDown));
            Assert.That(shifter.Form, Is.EqualTo(Form.Bird));
            Assert.That(shifter.Cooldown, Is.EqualTo(Tuning.ShiftCooldown * 0.5f).Within(1e-5f), "a refused shift does not restart the cooldown");
            Assert.That(shifter.CooldownFraction, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void TheCooldownRunsOutAndAllowsTheNextShift()
        {
            var shifter = new Shifter();
            shifter.TryShift(Form.Bird);
            shifter.Tick(Tuning.ShiftCooldown + 0.01f);
            Assert.That(shifter.Cooldown, Is.EqualTo(0f));
            Assert.That(shifter.Ready, Is.True);
            Assert.That(shifter.TryShift(Form.Fish), Is.EqualTo(ShiftResult.Shifted));
            Assert.That(shifter.Form, Is.EqualTo(Form.Fish));
        }

        [Test]
        public void ShiftingIntoTheCurrentFormDoesNothing()
        {
            var shifter = new Shifter();
            Assert.That(shifter.TryShift(Form.Fox), Is.EqualTo(ShiftResult.AlreadyInForm));
            Assert.That(shifter.Cooldown, Is.EqualTo(0f), "no cooldown is spent");
        }
    }
}
