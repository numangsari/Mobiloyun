using System.Collections.Generic;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>
    /// Tiny pooled particle system: sprite renderers updated in one loop.
    /// Cheaper and easier to tune from code than configuring ParticleSystem modules.
    /// </summary>
    public sealed class FxPool : MonoBehaviour
    {
        private const int Capacity = 96;

        private sealed class Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Life, Age, Size, Spin, Gravity, Drag;
            public Color Color;
            public bool Alive;
        }

        private readonly List<Particle> particles = new List<Particle>(Capacity);
        private int next;

        public static FxPool Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            var pool = go.AddComponent<FxPool>();
            for (int i = 0; i < Capacity; i++)
            {
                var p = new GameObject("p");
                p.transform.SetParent(go.transform, false);
                var r = p.AddComponent<SpriteRenderer>();
                r.sortingOrder = SortOrder.Effects;
                p.SetActive(false);
                pool.particles.Add(new Particle { Renderer = r });
            }
            return pool;
        }

        /// <summary>Juicy burst for a filled mold: coloured blobs plus white sparkles.</summary>
        public void Burst(Vector3 position, Color color, int count, float power = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i / (float)count + Random.value * 0.15f) * Mathf.PI * 2f;
                float speed = Random.Range(1.6f, 3.2f) * power;
                Spawn(position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Color.Lerp(color, Color.white, Random.value * 0.3f),
                    SpriteFactory.SoftCircle(), Random.Range(0.14f, 0.26f), Random.Range(0.45f, 0.7f), -4f, 3.5f);
            }
            for (int i = 0; i < count / 2; i++)
            {
                Vector2 v = Random.insideUnitCircle * 1.8f * power;
                Spawn(position + (Vector3)(Random.insideUnitCircle * 0.25f), v, Color.white,
                    SpriteFactory.Sparkle(), Random.Range(0.18f, 0.32f), Random.Range(0.4f, 0.65f), 0f, 2.5f);
            }
        }

        /// <summary>Confetti rain for the win celebration.</summary>
        public void Confetti(Vector3 center, float width, Color[] colors, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = center + new Vector3(Random.Range(-width, width) * 0.5f, Random.Range(-0.5f, 0.5f), 0f);
                Vector2 v = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(3f, 6.5f));
                Sprite s = Random.value < 0.35f ? SpriteFactory.Sparkle() : SpriteFactory.SoftCircle();
                Spawn(p, v, colors[Random.Range(0, colors.Length)], s, Random.Range(0.14f, 0.28f), Random.Range(1.1f, 1.8f), -7f, 0.8f);
            }
        }

        /// <summary>Small puff where a press starts.</summary>
        public void Puff(Vector3 position, Color color)
        {
            for (int i = 0; i < 5; i++)
            {
                Spawn(position, Random.insideUnitCircle * 1.1f, color, SpriteFactory.SoftCircle(), Random.Range(0.1f, 0.18f), 0.3f, 0f, 5f);
            }
        }

        private void Spawn(Vector3 position, Vector2 velocity, Color color, Sprite sprite, float size, float life, float gravity, float drag)
        {
            Particle p = particles[next];
            next = (next + 1) % particles.Count;
            p.Alive = true;
            p.Age = 0f;
            p.Life = life;
            p.Size = size;
            p.Velocity = velocity;
            p.Gravity = gravity;
            p.Drag = drag;
            p.Spin = Random.Range(-360f, 360f);
            p.Color = color;
            p.Renderer.sprite = sprite;
            p.Renderer.color = color;
            Transform t = p.Renderer.transform;
            t.position = position;
            t.localScale = Vector3.one * size;
            p.Renderer.gameObject.SetActive(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (Particle p in particles)
            {
                if (!p.Alive) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Alive = false;
                    p.Renderer.gameObject.SetActive(false);
                    continue;
                }
                float k = p.Age / p.Life;
                p.Velocity *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Velocity.y += p.Gravity * dt;
                Transform t = p.Renderer.transform;
                t.position += (Vector3)(p.Velocity * dt);
                t.Rotate(0f, 0f, p.Spin * dt);
                float scale = p.Size * (k < 0.15f ? Ease.OutBack(k / 0.15f) : 1f - 0.6f * (k - 0.15f));
                t.localScale = Vector3.one * Mathf.Max(0f, scale);
                Color c = p.Color;
                c.a = 1f - k * k;
                p.Renderer.color = c;
            }
        }
    }
}
