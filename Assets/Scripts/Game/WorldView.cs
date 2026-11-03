using System.Collections.Generic;
using ShapeShifter.Core;
using UnityEngine;

namespace ShapeShifter.Game
{
    /// <summary>
    /// Draws a <see cref="Run"/>: parallax backdrops that blend between forest and cavern, ground
    /// pieces between chasms, every obstacle with its warning sign, pickups and the runner. Pieces
    /// are built just ahead of the camera from the track and destroyed once behind it.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const float BiomeLength = 600f;
        const float GroundDepth = 7f;
        const float Behind = CameraRig.HalfWidth + 6f;
        const float Ahead = CameraRig.HalfWidth + 8f;
        const float CrumbleTime = 0.35f;

        sealed class Piece
        {
            public GameObject Root;
            public float End;
            public Obstacle Obstacle;
            /// <summary>Seconds of a smashed wall or boulder sinking into the ground; 0 when standing.</summary>
            public float Crumble;
        }

        sealed class Item
        {
            public Pickup Pickup;
            public SpriteRenderer Renderer;
        }

        GameArt art;
        Run run;
        string skin;
        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Obstacle, Piece> byObstacle = new Dictionary<Obstacle, Piece>();
        readonly List<Item> items = new List<Item>();
        SpriteRenderer[] layers;
        SpriteRenderer runner;
        int obstaclesBuilt;
        int pickupsBuilt;
        int chasmCursor;
        float groundBuiltUntil;
        float animTime;
        float smashTime;
        float pop;
        float clock;

        public SpriteRenderer Runner => runner;

        public void Init(GameArt gameArt)
        {
            art = gameArt;
            layers = new SpriteRenderer[8];
            Texture2D[] textures = { art.ForestFar, art.CaveFar, art.ForestNear, art.CaveNear };
            for (int i = 0; i < 8; i++)
            {
                var go = new GameObject("Backdrop " + i);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = art.Whole(textures[i / 2]);
                r.sortingOrder = -100 + i / 2;
                go.transform.localScale = new Vector3(1f, 1.04f, 1f);
                layers[i] = r;
            }
            var body = new GameObject("Runner");
            body.transform.SetParent(transform, false);
            runner = body.AddComponent<SpriteRenderer>();
            runner.sortingOrder = 5;
        }

        public void Show(Run newRun, string skinId)
        {
            Clear();
            run = newRun;
            skin = skinId;
            groundBuiltUntil = -Behind - 4f;
            runner.gameObject.SetActive(true);
        }

        public void SetSkin(string skinId) => skin = skinId;

        public void Clear()
        {
            foreach (Piece p in pieces) Destroy(p.Root);
            pieces.Clear();
            byObstacle.Clear();
            foreach (Item i in items) Destroy(i.Renderer.gameObject);
            items.Clear();
            obstaclesBuilt = 0;
            pickupsBuilt = 0;
            chasmCursor = 0;
            run = null;
            if (runner != null) runner.gameObject.SetActive(false);
        }

        public static int Biome(float x) => ((int)Mathf.Floor(x / BiomeLength) % 2 + 2) % 2;

        /// <summary>How much of the cavern shows at x: 0 in the forest, 1 in the cave, blended at the borders.</summary>
        public static float CaveWeight(float x)
        {
            float t = Mathf.Repeat(x, BiomeLength * 2f);
            float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BiomeLength - 30f, BiomeLength + 10f, t));
            float down = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BiomeLength * 2f - 30f, BiomeLength * 2f + 10f, t));
            return t < BiomeLength * 1.5f ? up : 1f - down;
        }

        public void OnEvent(RunEvent e)
        {
            switch (e.Kind)
            {
                case RunEventKind.Shifted:
                    pop = 1f;
                    break;
                case RunEventKind.Smashed:
                    smashTime = 0.25f;
                    Obstacle o = run.Track.ObstacleAt(e.X + 0.01f) ?? run.Track.ObstacleAt(e.X + 0.5f);
                    if (o != null && byObstacle.TryGetValue(o, out Piece piece)) piece.Crumble = CrumbleTime;
                    break;
            }
        }

        public void Tick(float cameraX, float dt)
        {
            clock += dt;
            Backdrops(cameraX);
            if (run == null) return;
            Build(cameraX + Ahead);
            Cull(cameraX - Behind);
            Pickups(dt);
            Crumbles(dt);
            Body(dt);
        }

        void Backdrops(float cameraX)
        {
            float cave = CaveWeight(cameraX);
            const float width = 27f;
            float[] drift = { 0.08f, 0.08f, 0.3f, 0.3f };
            for (int layer = 0; layer < 4; layer++)
            {
                float shift = Mathf.Repeat(cameraX * drift[layer], width);
                float alpha = layer == 0 ? 1f : layer == 1 || layer == 3 ? cave : 1f - cave;
                for (int copy = 0; copy < 2; copy++)
                {
                    SpriteRenderer r = layers[layer * 2 + copy];
                    r.transform.position = new Vector3(cameraX - shift + copy * width, CameraRig.CenterY, 0f);
                    r.color = new Color(1f, 1f, 1f, alpha);
                    r.enabled = alpha > 0.01f;
                }
            }
        }

        // ---- Building the track ----

        void Build(float until)
        {
            Track track = run.Track;
            until = Mathf.Min(until, track.GeneratedUntil);
            BuildGround(until);
            while (obstaclesBuilt < track.Obstacles.Count && track.Obstacles[obstaclesBuilt].TelegraphX < until)
            {
                BuildObstacle(track.Obstacles[obstaclesBuilt++]);
            }
            while (pickupsBuilt < track.Pickups.Count && track.Pickups[pickupsBuilt].X < until)
            {
                Pickup p = track.Pickups[pickupsBuilt++];
                if (p.Taken) continue;
                var go = new GameObject(p.Kind.ToString());
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(p.X, p.Y, 0f);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = 4;
                r.sprite = art.Frames(art.Items, 12, 0)[p.Kind == PickupKind.Coin ? 0 : Frame.HeartItem];
                items.Add(new Item { Pickup = p, Renderer = r });
            }
        }

        void BuildGround(float until)
        {
            IReadOnlyList<Obstacle> obstacles = run.Track.Obstacles;
            while (groundBuiltUntil < until)
            {
                float x = groundBuiltUntil;
                while (chasmCursor < obstacles.Count && (obstacles[chasmCursor].Kind != ObstacleKind.Chasm || obstacles[chasmCursor].End <= x)) chasmCursor++;
                Obstacle chasm = chasmCursor < obstacles.Count ? obstacles[chasmCursor] : null;
                if (chasm != null && chasm.Start <= x)
                {
                    Pit(chasm);
                    groundBuiltUntil = chasm.End;
                    continue;
                }
                float end = Mathf.Min(x + 48f, (Mathf.Floor(x / BiomeLength) + 1f) * BiomeLength);
                if (chasm != null) end = Mathf.Min(end, chasm.Start);
                // Never past the generated track: a chasm may still appear there.
                end = Mathf.Min(end, run.Track.GeneratedUntil);
                Ground(x, end);
                groundBuiltUntil = end;
            }
        }

        void Ground(float from, float to)
        {
            int biome = Biome(from);
            GameObject root = NewPiece("Ground", from, to, null);
            Tiled(root, art.Tile(Frame.TileTop, biome), to - from, 1f, -0.5f, 0);
            Tiled(root, art.Tile(Frame.TileFill, biome), to - from, GroundDepth, -1f - GroundDepth / 2f, 0);
        }

        void Pit(Obstacle chasm)
        {
            GameObject root = NewPiece("Chasm", chasm.Start, chasm.End, null);
            SpriteRenderer dark = Tiled(root, art.Tile(Frame.TileFill, Biome(chasm.Start)), chasm.Length, GroundDepth, -1.5f - GroundDepth / 2f, -1);
            dark.color = new Color(0.16f, 0.13f, 0.2f);
        }

        void BuildObstacle(Obstacle o)
        {
            int biome = Biome(o.Start);
            GameObject root = NewPiece(o.Kind.ToString(), o.Start, o.End, o);
            switch (o.Kind)
            {
                case ObstacleKind.Wall:
                    Tiled(root, art.Tile(Frame.TileWall, biome), o.Length, 5f, 2.5f, 3);
                    Tiled(root, art.Tile(Frame.TileWallTop, biome), o.Length, 1f, 5.5f, 3);
                    break;
                case ObstacleKind.Thicket:
                    Tiled(root, art.Whole(art.Thicket), o.Length, 2.5f, 1.25f, 2);
                    break;
                case ObstacleKind.Flood:
                    SpriteRenderer back = Tiled(root, art.Tile(Frame.TileFill, 1), o.Length, 3.6f, 1.8f, -2);
                    back.color = new Color(0.35f, 0.4f, 0.5f);
                    Tiled(root, art.Tile(Frame.TileWater, biome), o.Length, 2.6f, 1.3f, 4);
                    Tiled(root, art.Tile(Frame.TileWaterTop, biome), o.Length, 1f, 3.1f, 7);
                    Tiled(root, art.Tile(Frame.TileCeiling, 1), o.Length, 1f, 3.9f, 8);
                    SpriteRenderer rock = Tiled(root, art.Tile(Frame.TileFill, 1), o.Length, 9f, 8.9f, 8);
                    rock.color = new Color(0.55f, 0.55f, 0.65f);
                    break;
                case ObstacleKind.Boulder:
                    var boulder = new GameObject("Boulder").AddComponent<SpriteRenderer>();
                    boulder.transform.SetParent(root.transform, false);
                    boulder.sprite = art.Whole(art.Boulder, true);
                    boulder.sortingOrder = 3;
                    break;
            }
            // The warning sign, far enough ahead to read and shift.
            var signObject = new GameObject("Sign");
            signObject.transform.SetParent(transform, false);
            signObject.transform.position = new Vector3(o.TelegraphX, 0f, 0f);
            var sign = signObject.AddComponent<SpriteRenderer>();
            Form? needed = o.RequiredForm;
            sign.sprite = art.Frames(art.Signs, 16)[needed.HasValue ? (int)needed.Value : Frame.SignJump];
            sign.sortingOrder = 1;
            pieces.Add(new Piece { Root = signObject, End = o.TelegraphX + 1f });
        }

        GameObject NewPiece(string name, float from, float to, Obstacle obstacle)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3((from + to) / 2f, 0f, 0f);
            var piece = new Piece { Root = root, End = to, Obstacle = obstacle };
            pieces.Add(piece);
            if (obstacle != null) byObstacle[obstacle] = piece;
            return root;
        }

        static SpriteRenderer Tiled(GameObject parent, Sprite sprite, float width, float height, float y, int order)
        {
            var go = new GameObject(sprite.name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = new Vector2(width, height);
            r.sortingOrder = order;
            return r;
        }

        void Cull(float left)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                if (pieces[i].End >= left) continue;
                if (pieces[i].Obstacle != null) byObstacle.Remove(pieces[i].Obstacle);
                Destroy(pieces[i].Root);
                pieces.RemoveAt(i);
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Pickup.X >= left) continue;
                Destroy(items[i].Renderer.gameObject);
                items.RemoveAt(i);
            }
        }

        // ---- Animation ----

        void Pickups(float dt)
        {
            Sprite[] frames = art.Frames(art.Items, 12, 0);
            int coinFrame = (int)(clock * 10f) % Frame.CoinFrames;
            foreach (Item item in items)
            {
                bool show = !item.Pickup.Taken;
                if (item.Renderer.enabled != show) item.Renderer.enabled = show;
                if (!show) continue;
                if (item.Pickup.Kind == PickupKind.Coin)
                {
                    item.Renderer.sprite = frames[coinFrame];
                }
                else
                {
                    float beat = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(clock * 6f));
                    item.Renderer.transform.localScale = new Vector3(beat, beat, 1f);
                }
            }
        }

        void Crumbles(float dt)
        {
            foreach (Piece p in pieces)
            {
                if (p.Crumble <= 0f || !p.Root.activeSelf) continue;
                p.Crumble = Mathf.Max(0f, p.Crumble - dt);
                float left = p.Crumble / CrumbleTime;
                p.Root.transform.localScale = new Vector3(1f + (1f - left) * 0.15f, left, 1f);
                if (p.Crumble <= 0f) p.Root.SetActive(false);
            }
        }

        void Body(float dt)
        {
            smashTime = Mathf.Max(0f, smashTime - dt);
            pop = Mathf.Max(0f, pop - dt * 6f);
            Form form = run.Form;
            float floor = form == Form.Bird ? Tuning.BirdHover : 0f;
            bool airborne = run.Y > floor + 0.05f;
            animTime += dt * (form == Form.Bird ? 10f : 5f + run.Speed * 0.7f);
            int frame = (int)animTime % Frame.RunFrames;
            if (run.Over) frame = Frame.Hurt;
            else if (form == Form.Golem && smashTime > 0f) frame = Frame.Air;
            else if (airborne && form != Form.Bird) frame = Frame.Air;
            else if (form == Form.Bird && run.VelocityY < -0.5f) frame = Frame.Air;
            runner.sprite = art.RunnerSprite(skin, form, frame);
            bool blink = run.Health.Invulnerable && !run.Over && (int)(clock * 12f) % 2 == 0;
            runner.color = blink ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            float scale = 1f + pop * 0.3f;
            runner.transform.localScale = new Vector3(scale, scale, 1f);
            float x = Mathf.Round(run.X * 16f) / 16f;
            float y = Mathf.Round(run.Y * 16f) / 16f;
            runner.transform.position = new Vector3(x, y, 0f);
        }
    }
}
