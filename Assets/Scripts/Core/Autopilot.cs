namespace ShapeShifter.Core
{
    /// <summary>
    /// A simple player that reads the next obstacle like a person would: it shifts into the
    /// needed form a moment before the obstacle and jumps boulders. It runs the title-screen
    /// demo, and the tests use it to prove that generated tracks can be run without a hit.
    /// </summary>
    public static class Autopilot
    {
        /// <summary>How long before an obstacle the pilot shifts (well under the guaranteed gap).</summary>
        public const float ShiftLead = 0.45f;
        public const float JumpLead = 0.12f;

        public static RunInput Decide(Run run)
        {
            Obstacle next = run.NextObstacle;
            if (next == null || run.Over) return default;
            float seconds = (next.Start - run.X) / run.Speed;
            if (next.Kind == ObstacleKind.Boulder)
            {
                bool safe = run.Form == Form.Golem || run.Form == Form.Bird;
                return !safe && seconds < JumpLead && run.Grounded ? RunInput.JumpNow : default;
            }
            Form need = next.RequiredForm.Value;
            return run.Form != need && seconds < ShiftLead ? RunInput.ShiftTo(need) : default;
        }
    }
}
