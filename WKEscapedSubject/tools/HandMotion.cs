// 손 모션 시안 생성기 (게임에 적용하지 않음, 아티스트 전달용)
// 1) Shoot : 촉수를 사출하는 손의 움직임 (0.75초 유지 → 발사 → 반동 → 유지 → 회수)
// 2) Mutate: 5초 변이 동안 양손이 떨리면서 손등·손가락·손바닥 쪽으로 촉수가 감기는 과정
// 손 그림은 게임의 Infested 손 스프라이트(128x128)를 그대로 쓰고, 움직임은 이동·회전·크기 변화로만 표현한다.
// 손가락을 굽히는 등 포즈 자체가 바뀌는 부분은 정식 그림에서 아티스트가 그려야 한다 (시트의 메모 참고).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public static class HandMotion
{
    static readonly int Ink = Color.FromArgb(255, 10, 8, 14).ToArgb();
    static readonly int Sheen = Color.FromArgb(255, 70, 40, 90).ToArgb();
    static readonly int Rim = Color.FromArgb(255, 34, 22, 40).ToArgb();
    static readonly int Shadow = Color.FromArgb(255, 92, 88, 98).ToArgb();

    // ───────── 공용 ─────────

    class Hand
    {
        public int[] px; public int w, h; public bool[] mask; public float[] dist;
        public float pivotX, pivotY; // 손목 (회전·크기 변화의 중심)
        public float palmX, palmY;   // 손바닥 중심 (촉수가 나오는 곳)
    }

    static Hand LoadHand(string png, bool mirror)
    {
        var hd = new Hand();
        hd.px = TentacleOverlay.Load(png, out hd.w, out hd.h);
        if (mirror)
        {
            var m = new int[hd.px.Length];
            for (int y = 0; y < hd.h; y++)
                for (int x = 0; x < hd.w; x++) m[y * hd.w + x] = hd.px[y * hd.w + (hd.w - 1 - x)];
            hd.px = m;
        }
        hd.dist = TentacleOverlay.WristDistance(hd.px, hd.w, hd.h, out hd.mask);
        double wx = 0, wy = 0, pxs = 0, pys = 0; int wn = 0, pn = 0;
        for (int i = 0; i < hd.px.Length; i++)
        {
            if (!hd.mask[i]) continue;
            int x = i % hd.w, y = i / hd.w;
            if (hd.dist[i] < 0.08f) { wx += x; wy += y; wn++; }
            if (hd.dist[i] > 0.28f && hd.dist[i] < 0.48f) { pxs += x; pys += y; pn++; }
        }
        hd.pivotX = (float)(wx / Math.Max(1, wn)); hd.pivotY = (float)(wy / Math.Max(1, wn));
        hd.palmX = (float)(pxs / Math.Max(1, pn)); hd.palmY = (float)(pys / Math.Max(1, pn));
        return hd;
    }

    // src를 캔버스의 (ox,oy)에 놓고, 손목 기준으로 크기·회전·이동 (최근접 샘플링: 도트 유지)
    static void Blit(int[] canvas, int cw, int ch, int[] src, int sw, int sh, float ox, float oy,
                     float pivX, float pivY, float scale, float rotDeg, float dx, float dy)
    {
        double r = rotDeg * Math.PI / 180.0, cs = Math.Cos(r), sn = Math.Sin(r);
        float cx = ox + pivX + dx, cy = oy + pivY + dy;
        for (int Y = 0; Y < ch; Y++)
            for (int X = 0; X < cw; X++)
            {
                double rx = X + 0.5 - cx, ry = Y + 0.5 - cy;
                double ux = (rx * cs + ry * sn) / scale, uy = (-rx * sn + ry * cs) / scale;
                int sx = (int)Math.Floor(ux + pivX), sy = (int)Math.Floor(uy + pivY);
                if (sx < 0 || sy < 0 || sx >= sw || sy >= sh) continue;
                int c = src[sy * sw + sx];
                if (((c >> 24) & 255) > 0) canvas[Y * cw + X] = c;
            }
    }

    // 손 좌표의 점이 Blit 뒤 캔버스 어디로 가는지
    static PointF MapPoint(Hand hd, float ox, float oy, float x, float y, float scale, float rotDeg, float dx, float dy)
    {
        double r = rotDeg * Math.PI / 180.0, cs = Math.Cos(r), sn = Math.Sin(r);
        double ux = (x - hd.pivotX) * scale, uy = (y - hd.pivotY) * scale;
        return new PointF((float)(ox + hd.pivotX + dx + ux * cs - uy * sn), (float)(oy + hd.pivotY + dy + ux * sn + uy * cs));
    }

    static void Disc(byte[] ink, int w, int h, float cx, float cy, float r)
    {
        for (int y = (int)(cy - r - 1); y <= (int)(cy + r + 1); y++)
            for (int x = (int)(cx - r - 1); x <= (int)(cx + r + 1); x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float ddx = x + 0.5f - cx, ddy = y + 0.5f - cy;
                if (ddx * ddx + ddy * ddy <= r * r) ink[y * w + x] = 1;
            }
    }

    // 굵기가 줄어드는 휘어진 촉수 (2차 곡선)
    static void Tentacle(byte[] ink, int w, int h, PointF a, PointF ctrl, PointF b, float r0, float r1, float wave, float phase)
    {
        float len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        int steps = Math.Max(8, (int)(len * 2));
        float nx = -(b.Y - a.Y) / Math.Max(1f, len), ny = (b.X - a.X) / Math.Max(1f, len);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1 - t;
            float x = u * u * a.X + 2 * u * t * ctrl.X + t * t * b.X;
            float y = u * u * a.Y + 2 * u * t * ctrl.Y + t * t * b.Y;
            float s = (float)Math.Sin(t * 9 + phase) * wave * t; // 끝으로 갈수록 크게 꿈틀
            Disc(ink, w, h, x + nx * s, y + ny * s, r0 + (r1 - r0) * t);
        }
    }

    // 잉크 층에 광택·그늘을 넣어 캔버스에 합친다 (TentacleOverlay와 같은 규칙)
    static void Shade(int[] canvas, byte[] ink, int w, int h, bool skinShadow)
    {
        var src = (int[])canvas.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (ink[i] == 0)
                {
                    if (skinShadow && x > 0 && y > 0 && ink[i - w - 1] != 0 && ((src[i] >> 24) & 255) > 0) canvas[i] = Shadow;
                    continue;
                }
                bool top = y > 0 && ink[i - w] == 0, bottom = y < h - 1 && ink[i + w] == 0;
                canvas[i] = top ? Sheen : bottom ? Rim : Ink;
            }
    }

    static void Overlay(int[] canvas, int[] ov)
    {
        for (int i = 0; i < canvas.Length; i++) if (ov[i] != 0) canvas[i] = ov[i];
    }

    static int Hash(int a, int b) { unchecked { int x = a * 73856093 ^ b * 19349663; x ^= x >> 13; x *= 0x5bd1e995; x ^= x >> 15; return x & 0x7fffffff; } }
    static float Rand(int a, int b) { return Hash(a, b) / (float)int.MaxValue * 2f - 1f; } // -1..1

    class Frame { public string label, note; public int ms; public int[] px; }

    static void SaveFrames(List<Frame> frames, int w, int h, string dir, string title, int scale, int cols)
    {
        Directory.CreateDirectory(dir);
        for (int f = 0; f < frames.Count; f++)
            using (var b = TentacleOverlay.ToBitmap(frames[f].px, w, h))
                b.Save(Path.Combine(dir, string.Format("frame_{0:00}.png", f + 1)), ImageFormat.Png);

        // 정지 시트: 프레임 + 시간·설명
        int pad = 10, labelH = 46, rows = (frames.Count + cols - 1) / cols;
        int cellW = w * scale, cellH = h * scale;
        using (var sheet = new Bitmap(pad + cols * (cellW + pad), 34 + rows * (cellH + labelH + pad) + pad, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        using (var big = new Font("Malgun Gothic", 13f, FontStyle.Bold))
        using (var font = new Font("Malgun Gothic", 9.5f))
        using (var dim = new SolidBrush(Color.FromArgb(255, 190, 186, 200)))
        {
            g.Clear(Color.FromArgb(255, 58, 58, 66));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.DrawString(title, big, Brushes.White, pad, 6);
            for (int f = 0; f < frames.Count; f++)
            {
                int cx = pad + (f % cols) * (cellW + pad), cy = 34 + (f / cols) * (cellH + labelH + pad);
                g.DrawString((f + 1) + ". " + frames[f].label, font, Brushes.White, cx, cy);
                g.DrawString(frames[f].note, font, dim, new RectangleF(cx, cy + 17, cellW, labelH - 17));
                using (var b = TentacleOverlay.ToBitmap(frames[f].px, w, h))
                    g.DrawImage(b, cx, cy + labelH, cellW, cellH);
            }
            sheet.Save(Path.Combine(dir, "sheet.png"), ImageFormat.Png);
        }

        // 실제 속도 미리보기 (브라우저로 열기)
        var sb = new StringBuilder();
        sb.Append("<!doctype html><meta charset=\"utf-8\"><title>").Append(title).Append("</title>\n<style>")
          .Append("body{background:#3a3a42;color:#eee;font-family:'Malgun Gothic',sans-serif;margin:16px}")
          .Append("img{image-rendering:pixelated;width:").Append(w * 4).Append("px;max-width:100%;background:#2c2c33}")
          .Append("button{font:inherit;margin-right:6px}#lab{margin:8px 0;min-height:3em}</style>\n<h3>").Append(title).Append("</h3>\n")
          .Append("<img id=\"v\" src=\"frame_01.png\"><div id=\"lab\"></div>\n")
          .Append("<button onclick=\"play=!play;if(play)step()\">재생/정지</button><button onclick=\"speed=speed==1?0.25:1\">속도 1x / 0.25x</button>\n<script>\n")
          .Append("const F=[");
        for (int f = 0; f < frames.Count; f++)
            sb.AppendFormat("[{0},\"{1}\",\"{2}\"],", frames[f].ms, Esc(frames[f].label), Esc(frames[f].note));
        sb.Append("];\nlet i=0,play=true,speed=1;const v=document.getElementById('v'),lab=document.getElementById('lab');\n")
          .Append("F.forEach((_,k)=>{new Image().src='frame_'+String(k+1).padStart(2,'0')+'.png'});\n")
          .Append("function show(){v.src='frame_'+String(i+1).padStart(2,'0')+'.png';lab.textContent=(i+1)+'. '+F[i][1]+' — '+F[i][2];}\n")
          .Append("function step(){if(!play)return;show();const d=F[i][0]/speed;i=(i+1)%F.length;setTimeout(step,d);}\nstep();\n</script>\n");
        File.WriteAllText(Path.Combine(dir, "preview.html"), sb.ToString(), new UTF8Encoding(false));
    }

    static string Esc(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }

    // ───────── 손가락 포즈 (임시: 펼친 손 그림에서 손가락을 떼어 굽힘·벌림) ─────────

    class Finger { public List<int> pix = new List<int>(); public float bx, by, ax, ay, len; public int sign; }

    // 손가락 찾기: 손목에서 먼 쪽(dist > T) 영역에서, 손가락이 서로 떨어져 보이는 줄(행)들로 손가락마다 중심선을 맞춘 뒤
    // 각 칸을 가장 가까운 중심선의 손가락에 붙인다. 중심선에서 먼 칸(옆으로 꺾인 엄지 등)은 따로 떨어진 덩어리면 손가락으로 따로 잡는다
    static List<Finger> FindFingers(Hand hd, float T)
    {
        int w = hd.w, h = hd.h;
        Func<int, bool> inR = i => hd.mask[i] && hd.dist[i] > T;
        var rowsCenters = new List<KeyValuePair<int, List<float>>>();
        int maxRuns = 0;
        for (int y = 0; y < h; y++)
        {
            var c = new List<float>();
            for (int x = 0; x < w; )
            {
                if (!inR(y * w + x)) { x++; continue; }
                int x0 = x; while (x < w && inR(y * w + x)) x++;
                if (x - x0 >= 3) c.Add((x0 + x - 1) / 2f);
            }
            rowsCenters.Add(new KeyValuePair<int, List<float>>(y, c));
            maxRuns = Math.Max(maxRuns, c.Count);
        }
        var list = new List<Finger>();
        if (maxRuns == 0) return list;
        // 손가락별 (y, x중심) 모아 직선 x = a + b*y 맞추기
        var lines = new List<double[]>();
        for (int f = 0; f < maxRuns; f++)
        {
            double n = 0, sy = 0, sx = 0, syy = 0, sxy = 0;
            foreach (var kv in rowsCenters)
            {
                if (kv.Value.Count != maxRuns) continue;
                double y = kv.Key, x = kv.Value[f];
                n++; sy += y; sx += x; syy += y * y; sxy += x * y;
            }
            double b = n > 1 ? (n * sxy - sx * sy) / Math.Max(1e-6, n * syy - sy * sy) : 0, a = (sx - b * sy) / Math.Max(1, n);
            lines.Add(new[] { a, b });
            list.Add(new Finger());
        }
        var assigned = new bool[w * h];
        for (int i = 0; i < w * h; i++)
        {
            if (!inR(i)) continue;
            int x = i % w, y = i / w, best = -1; double bd = 1e9;
            for (int f = 0; f < lines.Count; f++)
            {
                double d = Math.Abs(x - (lines[f][0] + lines[f][1] * y));
                if (d < bd) { bd = d; best = f; }
            }
            if (bd <= 9) { list[best].pix.Add(i); assigned[i] = true; }
        }
        // 남은 덩어리 중 큰 것(엄지 등)은 각각 손가락
        var seen = new bool[w * h];
        for (int i = 0; i < w * h; i++)
        {
            if (!inR(i) || assigned[i] || seen[i]) continue;
            var f = new Finger(); var q = new Queue<int>(); q.Enqueue(i); seen[i] = true;
            while (q.Count > 0)
            {
                int p = q.Dequeue(); f.pix.Add(p); int x = p % w, y = p / w;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int k = ny * w + nx; if (inR(k) && !assigned[k] && !seen[k]) { seen[k] = true; q.Enqueue(k); }
                    }
            }
            if (f.pix.Count >= 150) { list.Add(f); continue; }
            // 작은 조각(손가락 끝이나 사이 틈의 자투리)은 가장 가까운 손가락에 붙인다
            foreach (int p in f.pix)
            {
                int x = p % w, y = p / w, best = 0; double bd = 1e9;
                for (int k = 0; k < lines.Count; k++)
                {
                    double d = Math.Abs(x - (lines[k][0] + lines[k][1] * y));
                    if (d < bd) { bd = d; best = k; }
                }
                list[best].pix.Add(p);
            }
        }
        list.RemoveAll(f => f.pix.Count < 30);
        foreach (var f in list)
        {
            // 뿌리 = 손목 쪽(거리가 가장 작은) 칸들의 중심, 끝 = 거리가 가장 큰 칸
            float minD = 9f; int tip = f.pix[0];
            foreach (int p in f.pix) { minD = Math.Min(minD, hd.dist[p]); if (hd.dist[p] > hd.dist[tip]) tip = p; }
            double sx = 0, sy = 0; int n = 0;
            foreach (int p in f.pix) if (hd.dist[p] < minD + 0.05f) { sx += p % w; sy += p / w; n++; }
            f.bx = (float)(sx / Math.Max(1, n)); f.by = (float)(sy / Math.Max(1, n));
            float ax = tip % w - f.bx, ay = tip / w - f.by, l = (float)Math.Sqrt(ax * ax + ay * ay);
            f.ax = ax / Math.Max(1f, l); f.ay = ay / Math.Max(1f, l); f.len = l;
            f.sign = f.bx < hd.palmX ? -1 : 1;
        }
        return list;
    }

    public static string DebugFingers(string handPng, string dst, float T)
    {
        Hand hd = LoadHand(handPng, false);
        var fs = FindFingers(hd, T);
        var px = (int[])hd.px.Clone();
        int[] cols = { Color.Red.ToArgb(), Color.Lime.ToArgb(), Color.Blue.ToArgb(), Color.Orange.ToArgb(), Color.Magenta.ToArgb(), Color.Cyan.ToArgb(), Color.Yellow.ToArgb() };
        for (int i = 0; i < fs.Count; i++) foreach (int p in fs[i].pix) px[p] = cols[i % cols.Length];
        using (var b = TentacleOverlay.ToBitmap(px, hd.w, hd.h)) b.Save(dst, ImageFormat.Png);
        var sb = new StringBuilder();
        foreach (var f in fs) sb.AppendFormat("n={0} base=({1:0},{2:0}) len={3:0} side={4}; ", f.pix.Count, f.bx, f.by, f.len, f.sign);
        return sb.ToString();
    }

    // curl: 0 펴짐 ~ 1 완전히 오므림 (손바닥 쪽으로 말려 들어가 짧아 보임)
    // splay: 바깥으로 벌리는 각도(도), 음수면 안쪽으로 모음. jitter: 손가락마다 다르게 움찔하는 정도
    static int[] PoseFingers(Hand hd, List<Finger> fs, int[] img, float curl, float splay, float jitter, int seed)
    {
        int w = hd.w, h = hd.h;
        var owner = new int[w * h];
        for (int i = 0; i < fs.Count; i++) foreach (int p in fs[i].pix) owner[p] = i + 1;
        // 손 외곽선 밖으로 칠해진 칸(감긴 띠의 가장자리)도 옆 손가락과 같이 움직이게
        for (int pass = 0; pass < 2; pass++)
        {
            var add = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] != 0 || hd.mask[i] || ((img[i] >> 24) & 255) == 0) continue;
                int x = i % w, y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    if (owner[ny * w + nx] != 0) { add.Add(new KeyValuePair<int, int>(i, owner[ny * w + nx])); break; }
                }
            }
            foreach (var kv in add) owner[kv.Key] = kv.Value;
        }
        var o = new int[w * h];
        for (int i = 0; i < o.Length; i++)
        {
            if (owner[i] == 0) { o[i] = img[i]; continue; } // 손바닥·손목
            Finger f0 = fs[owner[i] - 1]; // 손가락 뿌리(마디) 쪽은 제자리에 남겨 틈이 생기지 않게
            if ((i % w + 0.5f - f0.bx) * f0.ax + (i / w + 0.5f - f0.by) * f0.ay <= 3f) o[i] = img[i];
        }
        for (int fi = 0; fi < fs.Count; fi++)
        {
            Finger f = fs[fi];
            float c = Math.Max(0f, Math.Min(1f, curl + Rand(seed, fi) * jitter));
            float L = 1f - 0.55f * c;                        // 말려 들어가는 만큼 짧아 보임
            float ang = (splay + Rand(seed + 5, fi) * jitter * 12f) * f.sign - 14f * c * f.sign; // 오므리면 가운데로 모임
            double r = ang * Math.PI / 180.0, cs = Math.Cos(r), sn = Math.Sin(r);
            float px_ = -f.ay, py_ = f.ax; // 손가락 축에 수직
            int x0 = (int)(f.bx - f.len - 4), x1 = (int)(f.bx + f.len + 4), y0 = (int)(f.by - f.len - 4), y1 = (int)(f.by + f.len + 4);
            for (int y = Math.Max(0, y0); y <= Math.Min(h - 1, y1); y++)
                for (int x = Math.Max(0, x0); x <= Math.Min(w - 1, x1); x++)
                {
                    // 회전을 되돌린 뒤 축 방향으로 늘려 원래 손가락의 어느 칸인지 찾는다
                    double ux = x + 0.5 - f.bx, uy = y + 0.5 - f.by;
                    double rx = ux * cs + uy * sn, ry = -ux * sn + uy * cs;
                    double a = rx * f.ax + ry * f.ay, b = rx * px_ + ry * py_;
                    double sa = a > 0 ? a / L : a;
                    int sx = (int)Math.Floor(f.bx + f.ax * sa + px_ * b), sy = (int)Math.Floor(f.by + f.ay * sa + py_ * b);
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h || owner[sy * w + sx] != fi + 1) continue;
                    int col = img[sy * w + sx];
                    // 오므린 손가락 끝은 손바닥 쪽으로 숨어 어둡게 (마디가 보이는 느낌)
                    if (c > 0.15f && sa > f.len * 0.6f) col = Darken(col, 1f - 0.45f * c * (float)((sa / f.len - 0.6) / 0.4));
                    o[y * w + x] = col;
                }
        }
        return o;
    }

    static int Darken(int c, float k)
    {
        int a = (c >> 24) & 255, r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
        k = Math.Max(0.3f, Math.Min(1f, k));
        return (a << 24) | ((int)(r * k) << 16) | ((int)(g * k) << 8) | (int)(b * k);
    }

    // ───────── 1) 촉수 사출 손 모션 ─────────

    // label, note, ms, scale, rot, dx, dy, bulge, tentacleLen(0~1), retract, splash, curl, splay, jitter
    static readonly object[][] ShootKeys = new object[][]
    {
        new object[]{"0.00초 대기", "촉수 상태로 손을 뻗고 있음, 손가락 펴짐", 400, 1.00f, 0f, 0f, 0f, 0f, 0f, false, false, 0f, 0f, 0f},
        new object[]{"0.10초 버튼 누름", "손이 살짝 뒤로 당겨지며 손가락이 오므라들기 시작, 손등 가운데가 부풂", 120, 0.97f, 2f, 1f, 3f, 3f, 0f, false, false, 0.2f, 0f, 0.05f},
        new object[]{"0.35초 꿈틀", "손가락이 하나씩 엇갈려 경련, 손등 덩어리가 커짐 (사출음 시작)", 250, 0.96f, 3f, -1f, 4f, 5f, 0f, false, false, 0.4f, -2f, 0.3f},
        new object[]{"0.60초 최대 수축", "손가락을 손바닥 쪽으로 깊게 움켜쥐고 손을 가장 깊게 움츠림", 150, 0.93f, 5f, 1f, 7f, 7f, 0f, false, false, 0.8f, -4f, 0.08f},
        new object[]{"0.75초 발사", "손가락이 한 번에 쫙 펴지며 바깥으로 벌어짐, 손이 앞으로 튕김 + 검은 덩어리 파티클", 70, 1.07f, -4f, -2f, -5f, 4f, 0.45f, false, true, 0f, 11f, 0f},
        new object[]{"0.80초 반동", "손목이 꺾이며 뒤로 밀림, 벌어진 손가락이 부르르 떨림", 90, 0.97f, 7f, 3f, 6f, 3f, 0.85f, false, true, 0.12f, 7f, 0.2f},
        new object[]{"0.90초 박힘", "촉수가 벽에 박힘, 손과 손가락이 원래 자리로 돌아옴", 180, 1.00f, 2f, 0f, 2f, 2f, 1f, false, false, 0.08f, 3f, 0.05f},
        new object[]{"유지 (버튼 누르는 동안)", "촉수를 붙잡듯 손가락을 반쯤 오므린 채 끌려가는 쪽으로 당겨짐", 450, 1.02f, -2f, -1f, -2f, 2f, 1f, false, false, 0.38f, 0f, 0.1f},
        new object[]{"회수 (버튼 뗌)", "촉수가 손바닥으로 빨려 들어오며 손가락이 움찔 오므라듦, 손등이 다시 부풂", 160, 0.98f, 3f, 1f, 3f, 4f, 0.4f, true, false, 0.55f, -2f, 0.3f},
    };


    public static string Shoot(string handPng, string outDir)
    {
        Hand hd = LoadHand(handPng, false);
        // 변이 시안(Mutate)의 5초 완료 모습과 같은 손 (오른손: 줄기 시드 7, 감기는 띠 시드 7)
        int[] handFull = RenderMutHand(hd, TentacleOverlay.FullPattern(hd.dist, hd.mask, hd.w, hd.h, 7), 5f, 7);

        const int W = 200, H = 200;
        float ox = 60, oy = 72; // 손을 오른쪽 아래에 두고, 촉수는 화면 가운데(왼쪽 위)로 나간다
        var target = new PointF(18, 10); // 화면 중앙 방향

        // 손가락: 펼친 손 그림에서 떼어 내 굽힘(curl)·벌림(splay)·손가락마다 다른 움찔(jitter)을 준다 (임시, 정식 그림 전까지)
        List<Finger> fingers = FindFingers(hd, 0.4f);

        var keys = ShootKeys;

        var frames = new List<Frame>();
        for (int k = 0; k < keys.Length; k++)
        {
            object[] e = keys[k];
            float scale = (float)e[3], rot = (float)e[4], dx = (float)e[5], dy = (float)e[6], bulge = (float)e[7], len = (float)e[8];
            bool retract = (bool)e[9], splash = (bool)e[10];
            int[] posed = PoseFingers(hd, fingers, handFull, (float)e[11], (float)e[12], (float)e[13], k * 13 + 3);
            var canvas = new int[W * H];
            PointF palm = MapPoint(hd, ox, oy, hd.palmX, hd.palmY, scale, rot, dx, dy);

            // 날아가는 촉수 자체는 게임의 바나클 훅 촉수(검은 메시)가 그리므로 손 그림에는 넣지 않는다
            Blit(canvas, W, H, posed, hd.w, hd.h, ox, oy, hd.pivotX, hd.pivotY, scale, rot, dx, dy);

            // 손등 쪽: 부풀어 오르는 덩어리와 핏줄, 앞으로 튀는 파티클
            var ink = new byte[W * H];
            if (bulge > 0)
            {
                Disc(ink, W, H, palm.X, palm.Y, bulge);
                // 덩어리에서 손가락 쪽으로 갈라지는 핏줄
                for (int c = 0; c < 4; c++)
                {
                    double a = -Math.PI / 2 + (c - 1.5) * 0.55 + Rand(k, c) * 0.15;
                    var end = new PointF(palm.X + (float)Math.Cos(a) * bulge * 2.2f, palm.Y + (float)Math.Sin(a) * bulge * 2.2f);
                    Tentacle(ink, W, H, palm, new PointF((palm.X + end.X) / 2 + Rand(k, c + 9) * 2, (palm.Y + end.Y) / 2), end, 1.2f, 0.5f, 0f, 0f);
                }
            }
            if (splash)
            {
                // 촉수가 나가는 방향(화면 가운데)으로 부채꼴로 튀는 검은 덩어리, 손 가장자리에도 몇 방울
                double baseA = Math.Atan2(target.Y - palm.Y, target.X - palm.X);
                for (int s = 0; s < 26; s++)
                {
                    double a = baseA + Rand(k * 3, s) * 0.9;
                    float d = 14 + (Rand(s, k) + 1) * (k == 4 ? 22 : 34);
                    Disc(ink, W, H, palm.X + (float)Math.Cos(a) * d, palm.Y + (float)Math.Sin(a) * d, 1 + (Rand(s * 7, k) + 1) * 1.6f);
                }
                for (int s = 0; s < 6; s++)
                    Disc(ink, W, H, palm.X + Rand(s, k + 50) * 30, palm.Y + Rand(k + 50, s) * 18 - 10, 1.2f);
            }
            Shade(canvas, ink, W, H, true);
            frames.Add(new Frame { label = (string)e[0], note = (string)e[1], ms = (int)e[2], px = canvas });
        }
        SaveFrames(frames, W, H, outDir, "촉수 사출 손 모션 시안 v3 (손가락 굽힘·폄 임시 시안, 오른손 기준, 왼손은 좌우 반전)", 2, 5);
        return "사출 " + frames.Count + "프레임 → " + outDir;
    }

    // ───────── 2) 촉수 변이 5초 양손 모션 ─────────

    // 감기는 띠: 손목에서의 거리(dist)를 따라 반복되는 줄무늬를 비스듬히 기울여 나선처럼 보이게 한다
    static byte[] Rings(Hand hd, float limit, float width, bool wrapEdge, int seed)
    {
        int w = hd.w, h = hd.h;
        var ink = new byte[w * h];
        float tilt = seed % 2 == 0 ? 1.4f : -1.4f;
        for (int i = 0; i < ink.Length; i++)
        {
            if (!hd.mask[i] || hd.dist[i] > limit) continue;
            int x = i % w, y = i / w;
            float d = hd.dist[i];
            // 띠 5~6개, 구불구불하게 기울어진 나선
            float phase = d * 5.5f + x / (float)w * tilt + (float)Math.Sin(x * 0.13f + y * 0.07f + seed) * 0.28f;
            int band = (int)Math.Floor(phase);
            float fr = phase - band;
            // 띠마다 굵기가 다르고, 부풀었다 가늘어졌다 한다
            float wd = width * (0.75f + 0.5f * (Hash(band, seed) % 100) / 100f) * (1f + 0.35f * (float)Math.Sin(x * 0.31f + band * 2.1f));
            wd *= d < 0.35f ? 1.2f : d < 0.7f ? 1f : 0.8f; // 손목은 굵게, 손가락은 가늘게
            // 손가락·손 뒤로 넘어가는 구간은 끊어 보이게 (가려짐)
            bool hidden = Math.Sin(x * 0.19f + band * 1.7f + seed) > 0.82f;
            if (fr < wd && !hidden) ink[i] = 1;
        }
        if (wrapEdge)
        {
            // 띠가 손 가장자리를 넘어 반대편(손바닥)으로 감겨 들어가는 느낌: 외곽선 밖 1px까지 칠한다
            var add = new List<int>();
            for (int i = 0; i < ink.Length; i++)
            {
                if (ink[i] == 0) continue;
                int x = i % w, y = i / w;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int n = ny * w + nx;
                    if (!hd.mask[n]) add.Add(n);
                }
            }
            foreach (int n in add) ink[n] = 1;
        }
        return ink;
    }

    static int[] RenderMutHand(Hand hd, int[] strandsFull, float t, int seed)
    {
        // 0~1초 손목, 1~2초 손등, 2~3.5초 손가락, 3.5~5초 손바닥 쪽으로 감아 돌아감
        float strand = t < 1 ? 0.25f * t : t < 2 ? 0.25f + 0.3f * (t - 1) : t < 3.5f ? 0.55f + 0.46f * (t - 2) / 1.5f : 1.01f;
        float ringT = Math.Max(0f, t - 0.5f); // 감기는 띠는 기어오른 줄기를 0.5초 뒤따라온다
        float ring = ringT < 1 ? 0.25f * ringT : ringT < 2 ? 0.25f + 0.3f * (ringT - 1) : ringT < 3.5f ? 0.55f + 0.46f * (ringT - 2) / 1.5f : 1.01f;
        float width = t < 3.5f ? 0.13f : 0.13f + 0.09f * Math.Min(1f, (t - 3.5f) / 1.5f); // 마지막 1.5초: 띠가 조여 들며 굵어짐
        var px = (int[])hd.px.Clone();
        Overlay(px, TentacleOverlay.Reveal(strandsFull, hd.dist, hd.w, hd.h, strand));
        if (ring > 0) Shade(px, Rings(hd, ring, width, t >= 3.5f, seed), hd.w, hd.h, true);
        return px;
    }

    public static string Mutate(string handPng, string outDir)
    {
        Hand right = LoadHand(handPng, false), left = LoadHand(handPng, true);
        int[] sr = TentacleOverlay.FullPattern(right.dist, right.mask, right.w, right.h, 7);
        int[] sl = TentacleOverlay.FullPattern(left.dist, left.mask, left.w, left.h, 12);

        const int W = 300, H = 160;
        var times = new List<float>();
        for (int i = 0; i <= 20; i++) times.Add(i * 0.25f);
        var frames = new List<Frame>();
        for (int f = 0; f < times.Count; f++)
        {
            float t = times[f];
            var canvas = new int[W * H];
            // 떨림: 시간이 갈수록 커지고, 양손이 엇박자로 떤다. 1초·3.5초에 크게 움찔
            float amp = 0.4f + 1.8f * t / 5f;
            bool jerk = Math.Abs(t - 1f) < 0.01f || Math.Abs(t - 3.5f) < 0.01f;
            for (int side = 0; side < 2; side++)
            {
                Hand hd = side == 0 ? left : right;
                int seed = side == 0 ? 12 : 7;
                float dx = Rand(f, side * 2 + 1) * amp, dy = Rand(f, side * 2 + 2) * amp * 0.7f;
                float rot = Rand(f + 40, side) * (0.6f + t * 0.5f) * (side == 0 ? -1 : 1);
                float scale = jerk ? 0.95f : 1f + Rand(f + 80, side) * 0.01f;
                if (jerk) { dy += 3; rot += side == 0 ? -4 : 4; }
                int[] img = RenderMutHand(hd, side == 0 ? sl : sr, t, seed);
                float ox = side == 0 ? 6 : W - hd.w - 6, oy = H - hd.h + 14;
                Blit(canvas, W, H, img, hd.w, hd.h, ox, oy, hd.pivotX, hd.pivotY, scale, rot, dx, dy);
            }
            string label = t.ToString("0.00") + "초";
            string note =
                t == 0f ? "맨손 버튼을 누르기 시작 (Infested 손으로 바뀜)" :
                t < 1f ? "양손이 미세하게 떨림, 손목에서 촉수가 올라옴" :
                jerk && t < 2f ? "움찔: 손목을 감은 촉수가 조여 양손이 동시에 튐" :
                t < 2f ? "손등으로 기어오르며 감기기 시작" :
                t < 3.5f ? "손가락 마디마다 촉수가 감김 (손가락이 경련하듯 굽혔다 펴짐 — 정식 그림)" :
                jerk ? "움찔: 손바닥 쪽으로 감아 돌아가는 순간 크게 튐" :
                t < 5f ? "띠가 손 가장자리를 넘어 손바닥까지 감기고 점점 조여 듦, 떨림 최대" :
                "변이 완료 → 촉수 상태 (떨림 멈춤)";
            int[] px = canvas;
            if (t >= 5f)
            {
                // 완료 프레임은 떨림 없이 정지
                px = new int[W * H];
                for (int side = 0; side < 2; side++)
                {
                    Hand hd = side == 0 ? left : right;
                    int[] img = RenderMutHand(hd, side == 0 ? sl : sr, t, side == 0 ? 12 : 7);
                    Blit(px, W, H, img, hd.w, hd.h, side == 0 ? 6 : W - hd.w - 6, H - hd.h + 14, hd.pivotX, hd.pivotY, 1f, 0f, 0f, 0f);
                }
            }
            frames.Add(new Frame { label = label, note = note, ms = t >= 5f ? 900 : 250, px = px });
        }
        SaveFrames(frames, W, H, outDir, "촉수 변이 5초 양손 모션 시안 (0.25초 간격)", 2, 4);
        return "변이 " + frames.Count + "프레임 → " + outDir;
    }

    // ───────── 게임 적용용 에셋 (플러그인 Assets/) ─────────
    // Overlays/stage1~16/<스프라이트>.png : 변이 덧그림 (원래 손과 다른 칸만). 0~5초를 16등분한 시점의 줄기 + 감긴 띠
    // Shoot/frame2~9/<스프라이트>.png     : 사출 덧그림. 손가락을 굽히는 포즈(full)는 손 전체 그림, 나머지는 덧그림
    // Shoot/keys.txt  : 프레임별 ms, 크기, 회전(도), x·y 이동(px) — 게임에서 손 전체를 움직일 때 쓴다
    // Shoot/hands.txt : 스프라이트별 손목 x·y(px, 위에서부터), full 여부
    public static string BuildGameAssets(string srcDir, string assetDir)
    {
        string ovDir = Path.Combine(assetDir, "Overlays"), shDir = Path.Combine(assetDir, "Shoot");
        if (Directory.Exists(ovDir)) Directory.Delete(ovDir, true);
        if (Directory.Exists(shDir)) Directory.Delete(shDir, true);
        Directory.CreateDirectory(shDir);
        var meta = new StringBuilder();
        int count = 0, full = 0;
        foreach (string file in Directory.GetFiles(srcDir, "HND_*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int seed = 17; foreach (char c in name) seed = seed * 31 + c;
            Hand hd = LoadHand(file, false);
            int[] strands = TentacleOverlay.FullPattern(hd.dist, hd.mask, hd.w, hd.h, seed);
            for (int f = 1; f <= 16; f++)
                SavePng(Diff(RenderMutHand(hd, strands, 5f * f / 16f, seed), hd.px), hd.w, hd.h, Path.Combine(ovDir, "stage" + f), name);

            int[] mutated = RenderMutHand(hd, strands, 5f, seed);
            // 손가락 굽힘은 앞 손가락 레이어(F)가 따로 없는 펼친 손 포즈에서, 손가락이 3개 이상 잡힐 때만
            List<Finger> fingers = name.StartsWith("HND_B1") ? FindFingers(hd, 0.4f) : new List<Finger>();
            fingers.RemoveAll(x => x.pix.Count < 150 || x.len < 18);
            bool isFull = fingers.Count >= 3;
            if (isFull) full++;
            for (int k = 1; k < ShootKeys.Length; k++)
            {
                object[] e = ShootKeys[k];
                int[] img = isFull ? PoseFingers(hd, fingers, mutated, (float)e[11], (float)e[12], (float)e[13], k * 13 + 3) : (int[])mutated.Clone();
                float bulge = (float)e[7];
                if (bulge > 0)
                {
                    var ink = new byte[hd.w * hd.h];
                    Disc(ink, hd.w, hd.h, hd.palmX, hd.palmY, bulge);
                    for (int c = 0; c < 4; c++)
                    {
                        double a = -Math.PI / 2 + (c - 1.5) * 0.55 + Rand(k, c) * 0.15;
                        var end = new PointF(hd.palmX + (float)Math.Cos(a) * bulge * 2.2f, hd.palmY + (float)Math.Sin(a) * bulge * 2.2f);
                        Tentacle(ink, hd.w, hd.h, new PointF(hd.palmX, hd.palmY), new PointF((hd.palmX + end.X) / 2 + Rand(k, c + 9) * 2, (hd.palmY + end.Y) / 2), end, 1.2f, 0.5f, 0f, 0f);
                    }
                    Shade(img, ink, hd.w, hd.h, true);
                }
                SavePng(isFull ? img : Diff(img, hd.px), hd.w, hd.h, Path.Combine(shDir, "frame" + (k + 1)), name);
            }
            meta.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:0.0} {2:0.0} {3}\n", name, hd.pivotX, hd.pivotY, isFull ? 1 : 0);
            count++;
        }
        File.WriteAllText(Path.Combine(shDir, "hands.txt"), meta.ToString());
        var keys = new StringBuilder("# 프레임 ms 크기 회전(도,화면 시계방향) x이동(px) y이동(px,아래+)\n");
        for (int k = 0; k < ShootKeys.Length; k++)
        {
            object[] e = ShootKeys[k];
            keys.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5}\n", k + 1, e[2], e[3], e[4], e[5], e[6]);
        }
        File.WriteAllText(Path.Combine(shDir, "keys.txt"), keys.ToString());
        return count + "장 처리, 손가락 굽힘 " + full + "장";
    }

    // 원래 손과 다른 칸만 남긴 덧그림
    static int[] Diff(int[] img, int[] baseHand)
    {
        var o = new int[img.Length];
        for (int i = 0; i < o.Length; i++) if (img[i] != baseHand[i]) o[i] = img[i];
        return o;
    }

    static void SavePng(int[] px, int w, int h, string dir, string name)
    {
        Directory.CreateDirectory(dir);
        using (var b = TentacleOverlay.ToBitmap(px, w, h)) b.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
    }

    // 손가락 굽힘 포즈 확인용 시트 (full 스프라이트 × 사출 프레임)
    public static string GameShootSheet(string srcDir, string assetDir, string dst)
    {
        var names = new List<string>();
        foreach (string line in File.ReadAllLines(Path.Combine(assetDir, "Shoot", "hands.txt")))
        {
            string[] p = line.Split(' ');
            if (p.Length >= 4 && p[3] == "1") names.Add(p[0]);
        }
        int S = 128;
        using (var sheet = new Bitmap(S * 9, Math.Max(1, names.Count) * S, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(255, 58, 58, 66));
            for (int r = 0; r < names.Count; r++)
            {
                using (var b = new Bitmap(Path.Combine(srcDir, names[r] + ".png"))) g.DrawImage(b, 0, r * S, S, S);
                using (var o = new Bitmap(Path.Combine(assetDir, "Overlays", "stage16", names[r] + ".png"))) g.DrawImage(o, 0, r * S, S, S);
                for (int k = 2; k <= 9; k++)
                    using (var b = new Bitmap(Path.Combine(assetDir, "Shoot", "frame" + k, names[r] + ".png"))) g.DrawImage(b, (k - 1) * S, r * S, S, S);
                g.DrawString(names[r], SystemFonts.DefaultFont, Brushes.Yellow, 2, r * S + 2);
            }
            sheet.Save(dst, ImageFormat.Png);
        }
        return string.Join(",", names.ToArray());
    }
}
