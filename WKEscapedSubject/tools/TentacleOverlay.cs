// 촉수 변이 오버레이 생성기 (시안·초안용)
// 손 스프라이트(128x128)의 불투명 영역 안에서 "손목으로부터의 거리"를 계산하고,
// 손목에서 뻗어 나가는 검은 촉수 무늬를 그린 뒤 거리 기준으로 단계별(손목 → 손등 → 손 전체)로 잘라 낸다.
// 아티스트는 최종 단계 1장만 그리고, 1·2단계는 이 도구로 같은 거리 기준을 써서 잘라 낼 수 있다.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class TentacleOverlay
{
    static readonly Color Ink = Color.FromArgb(255, 10, 8, 14);      // 촉수 본체
    static readonly Color Sheen = Color.FromArgb(255, 70, 40, 90);   // 표면 광택 (보랏빛)
    static readonly Color Rim = Color.FromArgb(255, 34, 22, 40);     // 촉수 가장자리 그늘

    internal static int[] Load(string path, out int w, out int h)
    {
        using (var src = new Bitmap(path))
        {
            w = src.Width; h = src.Height;
            var bmp = src.Clone(new Rectangle(0, 0, w, h), PixelFormat.Format32bppArgb);
            var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new int[w * h];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            bmp.UnlockBits(d);
            bmp.Dispose();
            return px;
        }
    }

    internal static Bitmap ToBitmap(int[] px, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(px, 0, d.Scan0, px.Length);
        bmp.UnlockBits(d);
        return bmp;
    }

    // 손목 = 불투명 영역 중 화면 아래쪽 가장자리에 닿은 부분(소매 디더링)으로 보고 BFS 거리 계산 → 0~1 정규화
    internal static float[] WristDistance(int[] px, int w, int h, out bool[] mask)
    {
        mask = new bool[w * h];
        int maxY = 0;
        for (int i = 0; i < px.Length; i++)
            if (((px[i] >> 24) & 255) > 20) { mask[i] = true; maxY = Math.Max(maxY, i / w); }
        var dist = new int[w * h];
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;
        var q = new Queue<int>();
        for (int i = 0; i < px.Length; i++)
            if (mask[i] && i / w >= maxY - 10) { dist[i] = 0; q.Enqueue(i); }
        int far = 1;
        while (q.Count > 0)
        {
            int p = q.Dequeue(); int x = p % w, y = p / w;
            // 소매 디더링(체크무늬)도 이어지도록 8방향
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int k = ny * w + nx;
                    if (mask[k] && dist[k] < 0) { dist[k] = dist[p] + 1; far = Math.Max(far, dist[k]); q.Enqueue(k); }
                }
        }
        var f = new float[w * h];
        for (int i = 0; i < f.Length; i++) f[i] = dist[i] < 0 ? 2f : dist[i] / (float)far;
        return f;
    }

    // 최종 단계 촉수 무늬: 손목에서 거리 증가 방향으로 꿈틀대며 뻗는 굵은 줄기 + 가지
    internal static int[] FullPattern(float[] dist, bool[] mask, int w, int h, int seed)
    {
        var rnd = new Random(seed);
        var ink = new byte[w * h]; // 0 없음, 1 본체, 2 광택
        var starts = new List<int>();
        for (int i = 0; i < dist.Length; i++) if (mask[i] && dist[i] < 0.05f) starts.Add(i);
        if (starts.Count == 0) return new int[w * h];

        // 촉수마다 선호 방향을 부채꼴로 나눠 줘서 손 전체로 퍼지게 한다
        int strands = 8;
        starts.Sort((a, b) => (a % w).CompareTo(b % w));
        for (int s = 0; s < strands; s++)
        {
            int p = starts[Math.Min(starts.Count - 1, (int)((s + rnd.NextDouble()) / strands * starts.Count))];
            double ang = -Math.PI / 2 + (s - (strands - 1) / 2.0) * 0.32 + (rnd.NextDouble() - 0.5) * 0.2;
            double bx0 = Math.Cos(ang), by0 = Math.Sin(ang);
            int thick = 4;
            for (int step = 0; step < 260; step++)
            {
                int x = p % w, y = p / w;
                Stamp(ink, mask, w, h, x, y, thick);
                // 다음 칸: 거리가 늘어나는 이웃 중 무작위(꿈틀거림)
                int best = -1; float bestScore = -9f;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int k = ny * w + nx;
                        if (!mask[k]) continue;
                        float score = (dist[k] - dist[p]) * 40f
                                    + (float)((dx * bx0 + dy * by0) / Math.Sqrt(dx * dx + dy * dy)) * 0.9f
                                    + (float)rnd.NextDouble() * 1.1f;
                        if (score > bestScore) { bestScore = score; best = k; }
                    }
                if (best < 0 || dist[best] >= 0.97f) break;
                p = best;
                // 끝으로 갈수록 가늘게 (4 → 3 → 2)
                thick = dist[p] < 0.35f ? 4 : dist[p] < 0.7f ? 3 : 2;
                if (rnd.NextDouble() < 0.012) // 잔가지
                {
                    int bp = p;
                    for (int b = 0; b < 25; b++)
                    {
                        int bx = bp % w + rnd.Next(-1, 2), by = bp / w + rnd.Next(-1, 2);
                        if (bx < 0 || by < 0 || bx >= w || by >= h || !mask[by * w + bx]) break;
                        bp = by * w + bx; Stamp(ink, mask, w, h, bx, by, 1);
                    }
                }
            }
        }
        // 광택: 촉수 위쪽 가장자리 1px을 밝게 (빛이 위에서 온다고 가정) / 가장자리 아래는 그늘
        var outPx = new int[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (ink[i] == 0) continue;
                bool topEdge = y > 0 && ink[i - w] == 0;
                bool bottomEdge = y < h - 1 && ink[i + w] == 0;
                Color c = topEdge ? Sheen : bottomEdge ? Rim : Ink;
                outPx[i] = c.ToArgb();
            }
        // 피부 위로 "솟아오른" 느낌: 촉수 오른쪽 아래 피부에 1px 그림자
        int shadow = Color.FromArgb(255, 92, 88, 98).ToArgb();
        for (int y = 1; y < h; y++)
            for (int x = 1; x < w; x++)
            {
                int i = y * w + x;
                if (ink[i] == 0 && mask[i] && ink[i - w - 1] != 0) outPx[i] = shadow;
            }
        return outPx;
    }

    static void Stamp(byte[] ink, bool[] mask, int w, int h, int x, int y, int r)
    {
        for (int dy = -r / 2; dy <= (r - 1) / 2 + (r > 1 ? 0 : 0); dy++)
            for (int dx = -r / 2; dx <= (r - 1) / 2; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                int k = ny * w + nx;
                if (mask[k]) ink[k] = 1;
            }
    }

    // 최종 무늬에서 거리 limit 이하만 남긴다. 경계 근처는 체크무늬 디더링으로 "막 올라오는" 느낌
    internal static int[] Reveal(int[] full, float[] dist, int w, int h, float limit)
    {
        var o = new int[full.Length];
        for (int i = 0; i < full.Length; i++)
        {
            if (full[i] == 0) continue;
            float d = dist[i];
            if (d <= limit - 0.04f) o[i] = full[i];
            else if (d <= limit && ((i % w + i / w) & 1) == 0) o[i] = full[i];
        }
        return o;
    }

    static int[] Composite(int[] baseHand, int[] overlay)
    {
        var o = (int[])baseHand.Clone();
        for (int i = 0; i < o.Length; i++) if (overlay[i] != 0) o[i] = overlay[i];
        return o;
    }

    // 폴더의 모든 손 스프라이트에 대해 단계별 덧그림을 outDir/stageN/<이름>.png로 저장 (플러그인 임시 에셋용)
    public static string RunBatch(string srcDir, string outDir, float[] stages)
    {
        int count = 0;
        foreach (string file in System.IO.Directory.GetFiles(srcDir, "*.png"))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(file);
            int seed = 17;
            foreach (char c in name) seed = seed * 31 + c; // 이름마다 고정된 무늬
            int w, h;
            int[] baseHand = Load(file, out w, out h);
            bool[] mask;
            float[] dist = WristDistance(baseHand, w, h, out mask);
            int[] full = FullPattern(dist, mask, w, h, seed);
            for (int s = 0; s < stages.Length; s++)
            {
                string dir = System.IO.Path.Combine(outDir, "stage" + (s + 1));
                System.IO.Directory.CreateDirectory(dir);
                using (var b = ToBitmap(Reveal(full, dist, w, h, stages[s]), w, h))
                    b.Save(System.IO.Path.Combine(dir, name + ".png"), ImageFormat.Png);
            }
            count++;
        }
        return count + "장 처리";
    }

    // 손 스프라이트 + 최종 단계 덧그림을 합친 그림 (속박 아이콘 임시용)
    public static string Composite(string handPng, string overlayPng, string dst)
    {
        int w, h, w2, h2;
        int[] a = Load(handPng, out w, out h);
        int[] b = Load(overlayPng, out w2, out h2);
        for (int i = 0; i < a.Length; i++) if (((b[i] >> 24) & 255) > 0) a[i] = b[i];
        using (var bmp = ToBitmap(a, w, h)) bmp.Save(dst, ImageFormat.Png);
        return dst;
    }

    // stages: 각 단계의 거리 한계값 (예: 0.3, 0.65, 1.0)
    public static string Run(string handPng, string outDir, string name, int seed, float[] stages)
    {
        int w, h;
        int[] baseHand = Load(handPng, out w, out h);
        bool[] mask;
        float[] dist = WristDistance(baseHand, w, h, out mask);
        int[] full = FullPattern(dist, mask, w, h, seed);
        System.IO.Directory.CreateDirectory(outDir);
        var frames = new List<int[]> { baseHand };
        for (int s = 0; s < stages.Length; s++)
        {
            int[] ov = Reveal(full, dist, w, h, stages[s]);
            using (var b = ToBitmap(ov, w, h)) b.Save(System.IO.Path.Combine(outDir, name + "_stage" + (s + 1) + "_overlay.png"), ImageFormat.Png);
            frames.Add(Composite(baseHand, ov));
        }
        // 시안 시트: 0초(원래 손) + 단계별, 3배 확대
        int scale = 3, pad = 8;
        using (var sheet = new Bitmap(frames.Count * (w * scale + pad) + pad, h * scale + pad * 2 + 18, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Malgun Gothic", 10f))
        {
            g.Clear(Color.FromArgb(255, 58, 58, 66));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            string[] labels = { "0초 (원래 손)", "1초 손목", "2초 손등", "3초 손 전체", "4초", "5초" };
            for (int f = 0; f < frames.Count; f++)
            {
                using (var b = ToBitmap(frames[f], w, h))
                    g.DrawImage(b, pad + f * (w * scale + pad), pad + 18, w * scale, h * scale);
                g.DrawString(f < labels.Length ? labels[f] : "", font, Brushes.White, pad + f * (w * scale + pad), 4);
            }
            sheet.Save(System.IO.Path.Combine(outDir, name + "_concept_sheet.png"), ImageFormat.Png);
        }
        return name + ": 완료";
    }
}
