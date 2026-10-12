using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WKEscapedSubject
{
    // 그림 로딩 (파일은 AssetStore: DLL 안의 리소스)
    internal static class Assets
    {
        // 프레임(1~FrameCount) → 스프라이트 이름 → 덧그림 텍스처. 프레임이 클수록 촉수가 손목에서 손끝까지 더 올라온 상태
        static Dictionary<string, Texture2D>[] overlays;
        static int frameCount = -1;

        // Overlays/stage1, stage2, … 폴더 개수
        public static int FrameCount
        {
            get
            {
                if (frameCount < 0)
                {
                    frameCount = 0;
                    while (AssetStore.DirExists("Overlays/stage" + (frameCount + 1))) frameCount++;
                    overlays = new Dictionary<string, Texture2D>[frameCount];
                    Plugin.Log.LogInfo("촉수 덧그림 프레임 " + frameCount + "개");
                }
                return frameCount;
            }
        }
        static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        static readonly Sprite[] counter = new Sprite[10];

        // rel: Assets 기준 경로 ("UI/barnacle_counter_1.png")
        public static Texture2D LoadTexture(string rel, bool point)
        {
            byte[] bytes = AssetStore.Read(rel);
            if (bytes == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            // Unity 6의 LoadImage에는 ReadOnlySpan 오버로드가 섞여 있어 net472로는 직접 호출이 안 되므로 byte[] 버전을 리플렉션으로 부른다
            var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
            if (!(bool)load.Invoke(null, new object[] { tex, bytes, false })) return null;
            tex.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return tex;
        }

        // 기존 같은 종류(속박) 퍽 아이콘의 월드 크기에 맞춰 아이콘 스프라이트 생성
        public static Sprite LoadIcon(string file, WKAssetDatabase db, Perk.PerkType type)
        {
            Texture2D tex = LoadTexture(file, true);
            if (tex == null)
            {
                Plugin.Log.LogWarning("아이콘 파일이 없음: " + file);
                tex = Texture2D.whiteTexture;
            }
            var sizes = new List<float>();
            if (db.perkAssets != null)
                foreach (Perk p in db.perkAssets)
                {
                    if (p == null || p.icon == null || p.perkType != type) continue;
                    sizes.Add(Mathf.Max(p.icon.rect.width, p.icon.rect.height) / p.icon.pixelsPerUnit);
                }
            sizes.Sort();
            float world = sizes.Count > 0 ? sizes[sizes.Count / 2] : 1f;
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height) / world);
            s.name = Path.GetFileNameWithoutExtension(file);
            s.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return s;
        }

        // 속박이 켜진 동안 매 프레임 한 장씩 미리 읽어 둔다 (변이 도중 끊김 방지)
        public static void PreloadStep()
        {
            for (int i = 0; i < FrameCount; i++)
                if (overlays[i] == null) { overlays[i] = LoadFrame(i + 1); return; }
            for (int k = 2; k <= ShootFrames; k++)
                if (shootTex[k] == null) { shootTex[k] = LoadShootFrame(k); return; }
        }

        // 프레임 하나의 덧그림을 처음 쓸 때 읽는다
        static Dictionary<string, Texture2D> LoadFrame(int frame)
        {
            var d = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in AssetStore.List("Overlays/stage" + frame, ".png"))
                {
                    Texture2D t = LoadTexture(f, true);
                    if (t != null) d[Path.GetFileNameWithoutExtension(f)] = t;
                }
            return d;
        }

        // 지금 손에 표시된 스프라이트(baseSprite)에 겹칠 프레임별 덧그림 스프라이트. 크기·기준점을 원래 스프라이트에 맞춘다
        public static Sprite GetOverlay(int stage, Sprite baseSprite)
        {
            if (baseSprite == null || stage < 1 || stage > FrameCount) return null;
            if (overlays[stage - 1] == null) overlays[stage - 1] = LoadFrame(stage);
            string name = baseSprite.name.Replace("(Clone)", "").Trim();
            Texture2D tex;
            if (!overlays[stage - 1].TryGetValue(name, out tex)) return null;

            return MakeSprite("Tentacle_" + stage, name, tex, baseSprite);
        }

        // ───── 사출 손 모션 (Shoot/) ─────
        const int ShootFrames = 9;
        static readonly Dictionary<string, Texture2D>[] shootTex = new Dictionary<string, Texture2D>[ShootFrames + 1];
        static Dictionary<string, float[]> hands;   // 스프라이트 → {손목 x, 손목 y(위에서부터, 128px 기준), full}
        static HandPose[] shootKeys;

        static void LoadHands()
        {
            if (hands != null) return;
            hands = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            string[] lines = AssetStore.ReadLines("Shoot/hands.txt");
            if (lines == null) return;
            foreach (string line in lines)
            {
                string[] p = line.Split(' ');
                float x, y;
                if (p.Length < 4 || !TryF(p[1], out x) || !TryF(p[2], out y)) continue;
                hands[p[0]] = new[] { x, y, p[3] == "1" ? 1f : 0f };
            }
        }

        static bool TryF(string s, out float v)
        {
            return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);
        }

        static string BaseName(Sprite sp) { return sp.name.Replace("(Clone)", "").Trim(); }

        // 손목 위치 (스프라이트 픽셀, 위에서부터). 모르는 그림이면 아래쪽 가운데
        public static Vector2 GetWrist(string spriteName, float width, float height)
        {
            LoadHands();
            float[] h;
            if (hands.TryGetValue(spriteName.Replace("(Clone)", "").Trim(), out h)) return new Vector2(h[0] * width / 128f, h[1] * height / 128f);
            return new Vector2(width * 0.4f, height * 0.85f);
        }

        // 사출 프레임별 손 전체 움직임 (Shoot/keys.txt: 프레임 ms 크기 회전 x y)
        public static HandPose ShootKey(int frame)
        {
            if (shootKeys == null)
            {
                shootKeys = new HandPose[ShootFrames + 1];
                for (int i = 0; i <= ShootFrames; i++) shootKeys[i] = HandPose.Identity;
                string[] lines = AssetStore.ReadLines("Shoot/keys.txt");
                if (lines != null)
                    foreach (string line in lines)
                    {
                        string[] p = line.Split(' ');
                        int k; float sc, ro, dx, dy;
                        if (p.Length < 6 || !int.TryParse(p[0], out k) || k < 1 || k > ShootFrames) continue;
                        if (TryF(p[2], out sc) && TryF(p[3], out ro) && TryF(p[4], out dx) && TryF(p[5], out dy))
                            shootKeys[k] = new HandPose { scale = sc, rot = ro, dx = dx, dy = dy };
                    }
            }
            return shootKeys[Mathf.Clamp(frame, 1, ShootFrames)];
        }

        static Dictionary<string, Texture2D> LoadShootFrame(int frame)
        {
            var d = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (string f in AssetStore.List("Shoot/frame" + frame, ".png"))
                {
                    Texture2D t = LoadTexture(f, true);
                    if (t != null) d[Path.GetFileNameWithoutExtension(f)] = t;
                }
            return d;
        }

        // 사출 프레임(2~9) 그림. full = 손가락을 굽힌 손 전체 그림(원래 손을 숨기고 그린다), 아니면 덧그림
        public static Sprite GetShoot(int frame, Sprite baseSprite, out bool full)
        {
            full = false;
            if (baseSprite == null || frame < 2 || frame > ShootFrames) return null;
            if (shootTex[frame] == null) shootTex[frame] = LoadShootFrame(frame);
            string name = BaseName(baseSprite);
            Texture2D tex;
            if (!shootTex[frame].TryGetValue(name, out tex)) return null;
            LoadHands();
            float[] h;
            full = hands.TryGetValue(name, out h) && h[2] > 0f;
            return MakeSprite("Shoot_" + frame, name, tex, baseSprite);
        }

        static Sprite MakeSprite(string prefix, string name, Texture2D tex, Sprite baseSprite)
        {
            Rect r = baseSprite.rect;
            Vector2 pivot = new Vector2(baseSprite.pivot.x / r.width, baseSprite.pivot.y / r.height);
            float ppu = baseSprite.pixelsPerUnit * (tex.width / r.width);
            string key = prefix + "|" + name + "|" + ppu + "|" + pivot.x + "," + pivot.y;
            Sprite sp;
            if (!spriteCache.TryGetValue(key, out sp) || sp == null)
            {
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);
                sp.name = prefix + "_" + name;
                sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
                spriteCache[key] = sp;
            }
            return sp;
        }

        // 남은 횟수 표시 이미지 (UI/barnacle_counter_N.png)
        public static Sprite GetCounter(int remaining)
        {
            remaining = Mathf.Clamp(remaining, 0, counter.Length - 1);
            if (counter[remaining] == null)
            {
                Texture2D t = LoadTexture("UI/barnacle_counter_" + remaining + ".png", true);
                if (t == null) return null;
                counter[remaining] = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(1f, 0f), 1f);
                counter[remaining].hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return counter[remaining];
        }
    }
}
