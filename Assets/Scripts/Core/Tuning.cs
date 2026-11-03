namespace ShapeShifter.Core
{
    /// <summary>Every number that shapes the game, in world units (1 unit = 16 pixels) and seconds.</summary>
    public static class Tuning
    {
        // Forms
        public const float ShiftCooldown = 0.35f;
        /// <summary>Time a player is given to read the next obstacle and press a key.</summary>
        public const float ReactionTime = 0.55f;

        // Health
        public const int MaxHearts = 5;
        public const float InvulnerableTime = 1.5f;

        // Speed
        public const float BaseSpeed = 7f;
        public const float MaxSpeed = 16f;
        /// <summary>Distance over which about two thirds of the speed range is gained.</summary>
        public const float SpeedRamp = 1100f;

        // Jumping and flying
        public const float Gravity = 34f;
        public const float JumpVelocity = 11f;
        public static float JumpAirtime => 2f * JumpVelocity / Gravity;
        public const float BirdHover = 0.9f;
        public const float BirdRise = 7f;
        public const float BirdFlapVelocity = 8f;
        public const float BirdGravity = 18f;
        public const float BoulderHeight = 0.8f;
        /// <summary>Height of the middle of the runner's body above its feet; pickups are tested against it.</summary>
        public const float BodyCenter = 0.6f;
        public const float PickupReachX = 0.7f;
        public const float PickupReachY = 0.9f;

        // Track
        public const float ChunkLength = 48f;
        public const float SafeStart = 36f;
        public const float LookAhead = 120f;
        public const float TelegraphSeconds = 1.6f;
        public const float SameFormGap = 0.5f;
        public const float WallLength = 1.5f;
        public const float BoulderLength = 1f;

        // Score
        public const int DistancePoints = 1;
        public const int CoinPoints = 10;
        public const int PassBonus = 25;
        public const int HeartBonus = 100;
        public const int StreakPerMultiplier = 3;
        public const int MaxMultiplier = 5;
    }
}
