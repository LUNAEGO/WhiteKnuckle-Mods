// 바나클 훅 남은 횟수 표시 시안 생성기
// 두 갈래 촉수가 서로 꼬인 띠를 4칸으로 나눈 픽셀 아트. 남은 횟수만큼 칸이 보이고, 쓴 칸은 사라진다.
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class BarnacleCounterIcon
{
    const int SegW = 20, H = 28, Gap = 3, Pad = 2;
    static readonly Color Ink = Color.FromArgb(255, 10, 8, 14);
    static readonly Color Sheen = Color.FromArgb(255, 112, 64, 140);
    static readonly Color Mid = Color.FromArgb(255, 52, 30, 66);
    static readonly Color Outline = Color.FromArgb(255, 150, 128, 168);

    static int Total = 4; // 전체 칸 수

    // remaining: 남은 횟수 0~Total
    static Bitmap Draw(int remaining)
    {
        int w = Pad * 2 + SegW * Total + Gap * (Total - 1);
        var bmp = new Bitmap(w, H, PixelFormat.Format32bppArgb);
        for (int seg = 0; seg < remaining; seg++)
        {
            int x0 = Pad + seg * (SegW + Gap);
            var body = new bool[SegW, H];
            // 0 없음, 1 뒤 가닥, 2 앞 가닥, 3 앞 가닥 광택
            var kind = new int[SegW, H];
            // 한 칸 = 꼬임 한 번. 뒤 가닥(왼쪽 위 → 오른쪽 아래)을 먼저 그리고, 앞 가닥(왼쪽 아래 → 오른쪽 위)을 덮어 그린다
            for (int strand = 0; strand < 2; strand++)
            {
                bool front = strand == 1;
                for (int x = 0; x < SegW; x++)
                {
                    double t = (x + 0.5) / SegW;                          // 0~1
                    double s = Math.Sin((t - 0.5) * Math.PI);              // -1~1 S자 곡선
                    double cy = H / 2.0 + (front ? -s : s) * (H / 2.0 - 7);
                    double half = 3.2 - Math.Abs(t - 0.5) * 2.4;          // 가운데 굵고 칸 끝으로 갈수록 가늘게
                    for (int y = 0; y < H; y++)
                    {
                        double d = y + 0.5 - cy;
                        if (Math.Abs(d) > half) continue;
                        body[x, y] = true;
                        if (front) kind[x, y] = d < -half + 1.2 ? 3 : 2;
                        else if (kind[x, y] == 0) kind[x, y] = 1;
                    }
                }
            }
            for (int x = 0; x < SegW; x++)
                for (int y = 0; y < H; y++)
                {
                    if (!body[x, y]) continue;
                    Color c = kind[x, y] == 3 ? Sheen : kind[x, y] == 2 ? Ink : Mid;
                    bmp.SetPixel(x0 + x, y, c);
                }
            // 밝은 외곽선 1px (어두운 화면에서도 보이도록)
            for (int x = -1; x <= SegW; x++)
                for (int y = 0; y < H; y++)
                {
                    int bx = x0 + x;
                    if (bx < 0 || bx >= w) continue;
                    if (bmp.GetPixel(bx, y).A != 0) continue;
                    bool near = false;
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        for (int dy = -1; dy <= 1 && !near; dy++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && nx < SegW && ny >= 0 && ny < H && body[nx, ny] && Math.Abs(dx) + Math.Abs(dy) == 1) near = true;
                        }
                    if (near) bmp.SetPixel(bx, y, Outline);
                }
        }
        return bmp;
    }

    public static string Run(string outDir, int total = 4)
    {
        Total = total;
        System.IO.Directory.CreateDirectory(outDir);
        for (int r = 0; r <= Total; r++)
            using (var b = Draw(r)) b.Save(System.IO.Path.Combine(outDir, "barnacle_counter_" + r + ".png"), ImageFormat.Png);

        // 시안 시트: 어두운 게임 화면 위 우측 하단 배치 예시 (4배 확대)
        int scale = 4;
        using (var one = Draw(Total))
        using (var sheet = new Bitmap((Total + 1) * (one.Width * scale + 24) + 24, one.Height * scale + 60, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Malgun Gothic", 10f))
        {
            g.Clear(Color.FromArgb(255, 34, 34, 40));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            for (int r = Total; r >= 0; r--)
            {
                int x = 24 + (Total - r) * (one.Width * scale + 24);
                g.DrawString(r > 0 ? "남은 횟수 " + r : "0 (표시 사라짐)", font, Brushes.White, x, 8);
                using (var b = Draw(r)) g.DrawImage(b, x, 36, b.Width * scale, b.Height * scale);
            }
            sheet.Save(System.IO.Path.Combine(outDir, "barnacle_counter_sheet.png"), ImageFormat.Png);
        }
        return "완료";
    }
}
