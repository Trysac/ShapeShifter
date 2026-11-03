using System.Collections.Generic;

namespace ShapeShifter.Core
{
    /// <summary>The four shapes of the runner.</summary>
    public enum Form
    {
        Fox,
        Bird,
        Golem,
        Fish,
    }

    public enum ObstacleKind
    {
        /// <summary>A low tangle of branches; only the small fox slips through.</summary>
        Thicket,
        /// <summary>A gap in the ground; only the bird crosses it (a jump is too short).</summary>
        Chasm,
        /// <summary>A stone wall; only the golem smashes through.</summary>
        Wall,
        /// <summary>A flooded tunnel, water to the roof; only the fish swims it.</summary>
        Flood,
        /// <summary>A boulder on the path: jumped by any form, or smashed by the golem.</summary>
        Boulder,
    }

    public static class FormRules
    {
        public static readonly IReadOnlyList<Form> All = new[] { Form.Fox, Form.Bird, Form.Golem, Form.Fish };

        /// <summary>The one form that passes a gated obstacle, or null for a boulder (jump or smash).</summary>
        public static Form? RequiredForm(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Thicket: return Form.Fox;
                case ObstacleKind.Chasm: return Form.Bird;
                case ObstacleKind.Wall: return Form.Golem;
                case ObstacleKind.Flood: return Form.Fish;
                default: return null;
            }
        }

        /// <summary>Whether a runner in this form, with its feet at this height, is safe inside the obstacle.</summary>
        public static bool Passes(ObstacleKind kind, Form form, float height)
        {
            switch (kind)
            {
                case ObstacleKind.Chasm: return form == Form.Bird || height > 0f;
                case ObstacleKind.Boulder: return form == Form.Golem || height >= Tuning.BoulderHeight;
                default: return RequiredForm(kind) == form;
            }
        }

        public static bool CanJump(Form form) => form != Form.Golem;

        public static string Name(Form form) => form.ToString();
    }

    public enum ShiftResult
    {
        Shifted,
        CoolingDown,
        AlreadyInForm,
    }

    /// <summary>The current form and the cooldown that follows every shift.</summary>
    public sealed class Shifter
    {
        public Form Form { get; private set; } = Form.Fox;

        /// <summary>Seconds until the next shift is allowed.</summary>
        public float Cooldown { get; private set; }

        public bool Ready => Cooldown <= 0f;

        /// <summary>1 right after a shift, falling to 0 when the next shift is allowed.</summary>
        public float CooldownFraction => Cooldown / Tuning.ShiftCooldown;

        public ShiftResult TryShift(Form target)
        {
            if (target == Form) return ShiftResult.AlreadyInForm;
            if (!Ready) return ShiftResult.CoolingDown;
            Form = target;
            Cooldown = Tuning.ShiftCooldown;
            return ShiftResult.Shifted;
        }

        public void Tick(float dt)
        {
            Cooldown = Cooldown - dt > 0f ? Cooldown - dt : 0f;
        }
    }

    /// <summary>Hearts and the short invulnerability after a hit.</summary>
    public sealed class Health
    {
        public Health(int max = Tuning.MaxHearts)
        {
            Max = max;
            Hearts = max;
        }

        public int Max { get; }
        public int Hearts { get; private set; }
        public float InvulnerableLeft { get; private set; }
        public bool Invulnerable => InvulnerableLeft > 0f;
        public bool Dead => Hearts <= 0;

        /// <summary>Takes one heart unless invulnerable or already dead. Returns whether a heart was lost.</summary>
        public bool Damage()
        {
            if (Dead || Invulnerable) return false;
            Hearts--;
            if (!Dead) InvulnerableLeft = Tuning.InvulnerableTime;
            return true;
        }

        public bool Heal()
        {
            if (Dead || Hearts >= Max) return false;
            Hearts++;
            return true;
        }

        public void Tick(float dt)
        {
            InvulnerableLeft = InvulnerableLeft - dt > 0f ? InvulnerableLeft - dt : 0f;
        }
    }

    /// <summary>Speed in units per second as a function of the distance run.</summary>
    public static class SpeedCurve
    {
        public static float At(float distance)
        {
            if (distance <= 0f) return Tuning.BaseSpeed;
            return Tuning.BaseSpeed + (Tuning.MaxSpeed - Tuning.BaseSpeed) * (1f - (float)System.Math.Exp(-distance / Tuning.SpeedRamp));
        }
    }

    /// <summary>Metres, coins, and the bonus from clean passes multiplied by the streak.</summary>
    public sealed class Score
    {
        public float Distance { get; private set; }
        public int Coins { get; private set; }
        public int Bonus { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }

        public int Multiplier => 1 + System.Math.Min(Tuning.MaxMultiplier - 1, Streak / Tuning.StreakPerMultiplier);

        public int Total => (int)Distance * Tuning.DistancePoints + Coins * Tuning.CoinPoints + Bonus;

        public void AddDistance(float units) => Distance += units;

        public void AddCoin() => Coins++;

        public void AddBonus(int points) => Bonus += points;

        /// <summary>Pays the pass bonus at the current multiplier, then grows the streak. Returns the points.</summary>
        public int AddCleanPass()
        {
            int points = Tuning.PassBonus * Multiplier;
            Bonus += points;
            Streak++;
            if (Streak > BestStreak) BestStreak = Streak;
            return points;
        }

        public void BreakStreak() => Streak = 0;
    }
}
