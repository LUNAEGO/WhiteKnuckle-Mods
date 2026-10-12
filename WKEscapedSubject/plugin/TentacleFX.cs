using UnityEngine;

namespace WKEscapedSubject
{
    // 촉수 사출 파티클: 발사하는 손에서 검은 덩어리가 터지고, 벽에 닿은 지점에서 검은 촉수액이 튄다.
    // 색은 손 변이 촉수와 같은 팔레트 (#0A080E 본체, #46285A 광택, #221628 그늘)
    internal static class TentacleFX
    {
        static ParticleSystem ps;
        static readonly Color Ink = new Color(0.04f, 0.03f, 0.055f, 1f);
        static readonly Color Shade = new Color(0.13f, 0.086f, 0.157f, 1f);
        static readonly Color Sheen = new Color(0.275f, 0.157f, 0.353f, 1f);

        static ParticleSystem Get()
        {
            if (ps != null) return ps;
            var go = new GameObject("WKEscaped_TentacleFX");
            Object.DontDestroyOnLoad(go);
            ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.gravityModifier = 1.2f;
            var emission = ps.emission;
            emission.enabled = false; // 직접 Emit으로만 뿌린다
            var shape = ps.shape;
            shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f; // 공기 저항으로 점점 느려짐
            limit.limit = 0f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = MakeMaterial();
            ps.Play();
            return ps;
        }

        // 도트 느낌의 작은 덩어리 (8x8, 가장자리 투명)
        static Material MakeMaterial()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    float dx = x - 3.5f, dy = y - 3.5f;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= 11f ? Color.white : Color.clear);
                }
            tex.Apply();
            Shader sh = Shader.Find("Sprites/Default");
            Material m;
            if (sh != null) m = new Material(sh);
            else
            {
                // 셰이더를 이름으로 못 찾으면 SpriteRenderer 기본 재질을 빌린다
                var tmp = new GameObject("tmp");
                m = new Material(tmp.AddComponent<SpriteRenderer>().sharedMaterial);
                Object.Destroy(tmp);
            }
            m.mainTexture = tex;
            return m;
        }

        static Color RandomInk()
        {
            float r = Random.value;
            return r < 0.65f ? Ink : r < 0.9f ? Shade : Sheen;
        }

        // 발사 순간: 쏘는 손바닥 앞(손 그림보다 안쪽)에서 검은 덩어리가 터지고,
        // 촉수가 지나가는 길 앞부분을 따라 검은 방울이 흩뿌려진다
        public static void Burst(Vector3 start, Vector3 dir)
        {
            var p = Get();
            var e = new ParticleSystem.EmitParams();
            dir = dir.normalized;
            for (int i = 0; i < 40; i++)
            {
                Vector3 v = (dir + Random.insideUnitSphere * 0.9f).normalized * Random.Range(0.8f, 3.2f);
                e.position = start + Random.insideUnitSphere * 0.05f;
                e.velocity = v;
                e.startSize = Random.Range(0.03f, 0.075f);
                e.startLifetime = Random.Range(0.3f, 0.6f);
                e.startColor = RandomInk();
                p.Emit(e, 1);
            }
            // 촉수 길 앞 3m에 남는 방울 자국
            for (int i = 0; i < 18; i++)
            {
                float k = Random.Range(0.1f, 3f);
                e.position = start + dir * k + Random.insideUnitSphere * 0.04f;
                e.velocity = Random.insideUnitSphere * 0.4f + dir * 0.5f;
                e.startSize = Random.Range(0.02f, 0.05f);
                e.startLifetime = Random.Range(0.35f, 0.7f);
                e.startColor = RandomInk();
                p.Emit(e, 1);
            }
        }

        // 벽에 닿은 순간: 맞은 면에서 튀어나오는 검은 촉수액
        public static void Splat(Vector3 pos, Vector3 normal, float scale = 1f)
        {
            var p = Get();
            var e = new ParticleSystem.EmitParams();
            int n = Mathf.RoundToInt(30 * scale);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = (normal.normalized + Random.insideUnitSphere * 0.9f).normalized * Random.Range(1.2f, 4.5f) * Mathf.Sqrt(scale);
                e.position = pos + normal * 0.03f;
                e.velocity = v;
                e.startSize = Random.Range(0.03f, 0.11f) * Mathf.Sqrt(scale);
                e.startLifetime = Random.Range(0.45f, 0.9f);
                e.startColor = RandomInk();
                p.Emit(e, 1);
            }
        }
    }
}
