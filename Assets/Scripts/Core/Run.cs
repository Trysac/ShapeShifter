using System.Collections.Generic;

namespace ShapeShifter.Core
{
    /// <summary>What the player asks for this step: a form to shift into (or none) and a jump.</summary>
    public readonly struct RunInput
    {
        public RunInput(Form? shift, bool jump)
        {
            Shift = shift;
            Jump = jump;
        }

        public Form? Shift { get; }
        public bool Jump { get; }

        public static RunInput ShiftTo(Form form) => new RunInput(form, false);
        public static RunInput JumpNow => new RunInput(null, true);
    }

    public enum RunEventKind
    {
        Shifted,
        ShiftBlocked,
        Jumped,
        Landed,
        Coin,
        Heart,
        Hurt,
        Fell,
        Smashed,
        Passed,
        GameOver,
    }

    /// <summary>Something the view should show or play.</summary>
    public readonly struct RunEvent
    {
        public RunEvent(RunEventKind kind, float x, float y, Form form, ObstacleKind obstacle = default, int points = 0)
        {
            Kind = kind;
            X = x;
            Y = y;
            Form = form;
            Obstacle = obstacle;
            Points = points;
        }

        public RunEventKind Kind { get; }
        public float X { get; }
        public float Y { get; }
        public Form Form { get; }
        public ObstacleKind Obstacle { get; }
        public int Points { get; }
    }

    /// <summary>
    /// One endless run: the runner moves right at the speed of the curve, shifts form, jumps,
    /// and is tested against every obstacle it is inside. The wrong form costs a heart (unless
    /// invulnerable) and the streak; a chasm also drops a non-bird and carries it across. The
    /// engine steps it at a fixed rate; <see cref="Events"/> lists what happened in the last step.
    /// </summary>
    public sealed class Run
    {
        readonly List<RunEvent> events = new List<RunEvent>();
        int obstacleCursor;
        int pickupCursor;

        public Run(int seed) : this(new Track(seed))
        {
        }

        public Run(Track track)
        {
            Track = track;
            Track.EnsureUntil(Tuning.LookAhead);
            Speed = SpeedCurve.At(0f);
        }

        public Track Track { get; }
        public Shifter Shifter { get; } = new Shifter();
        public Health Health { get; } = new Health();
        public Score Score { get; } = new Score();
        public Form Form => Shifter.Form;
        public float X { get; private set; }
        /// <summary>Height of the runner's feet above the ground.</summary>
        public float Y { get; private set; }
        public float VelocityY { get; private set; }
        public float Speed { get; private set; }
        public float Elapsed { get; private set; }
        public float Distance => Score.Distance;
        public bool Over { get; private set; }
        public IReadOnlyList<RunEvent> Events => events;

        /// <summary>On the ground (or, for the bird, at its gliding height).</summary>
        public bool Grounded => VelocityY <= 0f && Y <= Floor + 0.001f;

        float Floor => Form == Form.Bird ? Tuning.BirdHover : 0f;

        /// <summary>The first obstacle not yet behind the runner, or null.</summary>
        public Obstacle NextObstacle
        {
            get
            {
                for (int i = obstacleCursor; i < Track.Obstacles.Count; i++)
                {
                    Obstacle o = Track.Obstacles[i];
                    if (!o.Resolved && o.End > X) return o;
                }
                return null;
            }
        }

        public void Step(float dt, RunInput input)
        {
            events.Clear();
            if (Over) return;
            Elapsed += dt;
            Shifter.Tick(dt);
            Health.Tick(dt);

            if (input.Shift.HasValue)
            {
                ShiftResult result = Shifter.TryShift(input.Shift.Value);
                if (result == ShiftResult.Shifted) Emit(RunEventKind.Shifted);
                else if (result == ShiftResult.CoolingDown) Emit(RunEventKind.ShiftBlocked, form: input.Shift.Value);
            }
            if (input.Jump && Grounded && FormRules.CanJump(Form))
            {
                VelocityY = Form == Form.Bird ? Tuning.BirdFlapVelocity : Tuning.JumpVelocity;
                Emit(RunEventKind.Jumped);
            }

            Vertical(dt);

            Speed = SpeedCurve.At(Distance);
            float advance = Speed * dt;
            X += advance;
            Score.AddDistance(advance);
            Track.EnsureUntil(X + Tuning.LookAhead);

            Obstacles();
            if (!Over) Pickups();
        }

        void Vertical(float dt)
        {
            bool wasAirborne = Y > Floor + 0.001f || VelocityY > 0f;
            float floor = Floor;
            if (Y < floor && VelocityY <= 0f)
            {
                // A fresh bird rises to its gliding height.
                Y = System.Math.Min(floor, Y + Tuning.BirdRise * dt);
                return;
            }
            VelocityY -= (Form == Form.Bird ? Tuning.BirdGravity : Tuning.Gravity) * dt;
            Y += VelocityY * dt;
            if (Y <= floor)
            {
                Y = floor;
                VelocityY = 0f;
                if (wasAirborne) Emit(RunEventKind.Landed);
            }
        }

        void Obstacles()
        {
            IReadOnlyList<Obstacle> list = Track.Obstacles;
            while (obstacleCursor < list.Count && list[obstacleCursor].Resolved) obstacleCursor++;
            for (int i = obstacleCursor; i < list.Count; i++)
            {
                Obstacle o = list[i];
                if (o.Start > X) break;
                if (o.Resolved) continue;
                if (X < o.End)
                {
                    Inside(o);
                    if (Over) return;
                }
                if (X >= o.End && !o.Resolved)
                {
                    o.Resolved = true;
                    if (!o.Failed) Emit(RunEventKind.Passed, o.Kind, Score.AddCleanPass());
                }
            }
        }

        void Inside(Obstacle o)
        {
            if (o.Failed) return;
            if (FormRules.Passes(o.Kind, Form, Y))
            {
                if (Form == Form.Golem && (o.Kind == ObstacleKind.Wall || o.Kind == ObstacleKind.Boulder) && !o.Smashed)
                {
                    o.Smashed = true;
                    Emit(RunEventKind.Smashed, o.Kind);
                }
                return;
            }
            o.Failed = true;
            if (o.Kind == ObstacleKind.Chasm)
            {
                Emit(RunEventKind.Fell, o.Kind);
                float carried = o.End - X;
                X = o.End;
                Score.AddDistance(carried);
                Y = 0f;
                VelocityY = 0f;
            }
            if (Health.Damage())
            {
                Score.BreakStreak();
                Emit(RunEventKind.Hurt, o.Kind);
                if (Health.Dead)
                {
                    Over = true;
                    Emit(RunEventKind.GameOver, o.Kind);
                }
            }
        }

        void Pickups()
        {
            IReadOnlyList<Pickup> list = Track.Pickups;
            while (pickupCursor < list.Count && (list[pickupCursor].Taken || list[pickupCursor].X < X - Tuning.PickupReachX)) pickupCursor++;
            float body = Y + Tuning.BodyCenter;
            for (int i = pickupCursor; i < list.Count; i++)
            {
                Pickup p = list[i];
                if (p.X > X + Tuning.PickupReachX) break;
                if (p.Taken || System.Math.Abs(p.Y - body) >= Tuning.PickupReachY) continue;
                p.Taken = true;
                if (p.Kind == PickupKind.Coin)
                {
                    Score.AddCoin();
                    events.Add(new RunEvent(RunEventKind.Coin, p.X, p.Y, Form, points: Tuning.CoinPoints));
                }
                else
                {
                    int points = 0;
                    if (!Health.Heal())
                    {
                        points = Tuning.HeartBonus;
                        Score.AddBonus(points);
                    }
                    events.Add(new RunEvent(RunEventKind.Heart, p.X, p.Y, Form, points: points));
                }
            }
        }

        void Emit(RunEventKind kind, ObstacleKind obstacle = default, int points = 0, Form? form = null) =>
            events.Add(new RunEvent(kind, X, Y, form ?? Form, obstacle, points));
    }
}
