using System.Collections.Generic;
using UnityEngine;

namespace ShapeShifter.Game
{
    /// <summary>Short sprite particles from the effects sheet: dust, sparkles, splashes, rocks, rings, leaves.</summary>
    public sealed class Effects : MonoBehaviour
    {
        sealed class Particle
        {
            public SpriteRenderer Renderer;
            public int First;
            public int Count;
            public float Fps;
            public Vector2 Velocity;
            public float Gravity;
            public float Age;
            public float Life;
            public float Spin;
            public bool Loop;
        }

        readonly List<Particle> live = new List<Particle>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        Sprite[] frames;

        public void Init(GameArt art)
        {
            frames = art.Frames(art.Effects, 16, 0);
        }

        public void Clear()
        {
            foreach (Particle p in live) Release(p.Renderer);
            live.Clear();
        }

        public void Spawn(Vector2 at, int first, int count, float fps, Vector2 velocity, float gravity = 0f, float life = -1f, float spin = 0f, bool loop = false, Color? tint = null)
        {
            SpriteRenderer r = pool.Count > 0 ? pool.Pop() : CreateRenderer();
            r.gameObject.SetActive(true);
            r.transform.position = new Vector3(at.x, at.y, 0f);
            r.transform.rotation = Quaternion.identity;
            r.sprite = frames[first];
            r.color = tint ?? Color.white;
            live.Add(new Particle
            {
                Renderer = r, First = first, Count = count, Fps = fps, Velocity = velocity, Gravity = gravity,
                Life = life > 0f ? life : count / fps, Spin = spin, Loop = loop,
            });
        }

        public void Dust(Vector2 at, int puffs = 3)
        {
            for (int i = 0; i < puffs; i++)
            {
                Spawn(at + new Vector2(Random.Range(-0.4f, 0.4f), 0.2f), 0, 4, 12f, new Vector2(Random.Range(-2f, -0.5f), Random.Range(0.3f, 1.2f)));
            }
        }

        public void Sparkle(Vector2 at, Color tint) => Spawn(at, 4, 4, 16f, new Vector2(0f, 1f), tint: tint);

        public void Splash(Vector2 at)
        {
            Spawn(at, 8, 4, 12f, Vector2.zero);
            for (int i = 0; i < 4; i++) Spawn(at + new Vector2(0f, 0.4f), 8, 1, 1f, new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 7f)), 22f, 0.6f);
        }

        public void Rocks(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Spawn(at + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0f, 3f)), 12 + (i % 2), 1, 1f,
                    new Vector2(Random.Range(2f, 9f), Random.Range(2f, 9f)), 30f, 1.2f, Random.Range(-720f, 720f));
            }
        }

        public void Leaves(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Spawn(at + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.3f, 2f)), 18 + (i % 2), 1, 1f,
                    new Vector2(Random.Range(-3f, 1f), Random.Range(1f, 4f)), 6f, 0.9f, Random.Range(-360f, 360f));
            }
        }

        public void Ring(Vector2 at, Color tint) => Spawn(at, 14, 4, 18f, Vector2.zero, tint: tint);

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Particle p = live[i];
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    Release(p.Renderer);
                    live.RemoveAt(i);
                    continue;
                }
                p.Velocity.y -= p.Gravity * dt;
                Transform t = p.Renderer.transform;
                t.position += (Vector3)(p.Velocity * dt);
                if (p.Spin != 0f) t.Rotate(0f, 0f, p.Spin * dt);
                int frame = (int)(p.Age * p.Fps);
                p.Renderer.sprite = frames[p.First + (p.Loop ? frame % p.Count : Mathf.Min(frame, p.Count - 1))];
                if (p.Gravity > 0f)
                {
                    Color c = p.Renderer.color;
                    c.a = Mathf.Clamp01((p.Life - p.Age) / 0.3f);
                    p.Renderer.color = c;
                }
            }
        }

        SpriteRenderer CreateRenderer()
        {
            var go = new GameObject("Particle");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = 20;
            return r;
        }

        void Release(SpriteRenderer r)
        {
            r.gameObject.SetActive(false);
            pool.Push(r);
        }
    }
}
