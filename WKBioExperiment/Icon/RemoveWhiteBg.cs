using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class RemoveWhiteBg
{
    // 가장자리에서 이어진 밝은 무채색 픽셀만 배경으로 보고 투명 처리 → 내용 영역으로 잘라 정사각형(여백 포함)으로 리사이즈
    public static string Run(string src, string dst, int outSize, double padRatio)
    {
        Bitmap bmp;
        using (var tmp = new Bitmap(src)) bmp = tmp.Clone(new Rectangle(0, 0, tmp.Width, tmp.Height), PixelFormat.Format32bppArgb);
        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[w * h * 4];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        Func<int, int> whiteness = i => {
            int b = px[i], g = px[i + 1], r = px[i + 2];
            int mn = Math.Min(r, Math.Min(g, b)), mx = Math.Max(r, Math.Max(g, b));
            if (mx - mn > 40) return 0;          // 채도가 있으면 배경 아님
            return mn;                            // 밝기
        };

        var bg = new bool[w * h];
        var q = new Queue<int>();
        for (int x = 0; x < w; x++) { q.Enqueue(x); q.Enqueue((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { q.Enqueue(y * w); q.Enqueue(y * w + w - 1); }
        while (q.Count > 0)
        {
            int p = q.Dequeue();
            if (bg[p] || whiteness(p * 4) < 200) continue;
            bg[p] = true;
            int x = p % w, y = p / w;
            if (x > 0) q.Enqueue(p - 1);
            if (x < w - 1) q.Enqueue(p + 1);
            if (y > 0) q.Enqueue(p - w);
            if (y < h - 1) q.Enqueue(p + w);
        }

        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int p = 0; p < w * h; p++)
        {
            int i = p * 4;
            if (bg[p]) { px[i + 3] = 0; continue; }
            // 배경과 맞닿은 가장자리의 흰빛 섞인 픽셀은 밝을수록 투명하게 (번짐 제거)
            int x = p % w, y = p / w;
            bool edge = (x > 0 && bg[p - 1]) || (x < w - 1 && bg[p + 1]) || (y > 0 && bg[p - w]) || (y < h - 1 && bg[p + w]);
            if (edge)
            {
                int wv = whiteness(i);
                if (wv > 150) px[i + 3] = (byte)Math.Max(0, 255 - (wv - 150) * 255 / 105);
            }
            if (px[i + 3] > 0)
            {
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        }
        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);

        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        int side = Math.Max(cw, ch);
        int canvas = (int)Math.Round(side * (1 + 2 * padRatio));
        using (var outBmp = new Bitmap(outSize, outSize, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(outBmp))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingMode = CompositingMode.SourceCopy;
            float scale = (float)outSize / canvas;
            float dw = cw * scale, dh = ch * scale;
            var destRect = new RectangleF((outSize - dw) / 2f, (outSize - dh) / 2f, dw, dh);
            using (var attr = new ImageAttributes())
            {
                attr.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(bmp, Rectangle.Round(destRect), minX, minY, cw, ch, GraphicsUnit.Pixel, attr);
            }
            outBmp.Save(dst, ImageFormat.Png);
        }
        bmp.Dispose();
        return string.Format("원본 {0}x{1}, 내용 영역 {2}x{3} @({4},{5}), 출력 {6}x{6}", w, h, cw, ch, minX, minY, outSize);
    }
}
