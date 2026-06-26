using System.Drawing;
using System.Drawing.Drawing2D;

namespace Multizip.App.Util
{
    /// <summary>
    /// Simple, classic-looking icons drawn with GDI+ so the app ships no binary image
    /// assets and stays tiny. Each returns a 48x48 bitmap suitable for a launcher
    /// button or toolbar (toolbar uses the 16px overload).
    /// </summary>
    public static class Glyphs
    {
        private static readonly Color Manila = Color.FromArgb(255, 206, 99);
        private static readonly Color ManilaEdge = Color.FromArgb(212, 160, 23);
        private static readonly Color Paper = Color.FromArgb(250, 250, 250);
        private static readonly Color Ink = Color.FromArgb(70, 70, 70);
        private static readonly Color Blue = Color.FromArgb(0, 102, 204);
        private static readonly Color Green = Color.FromArgb(46, 160, 67);

        public static Bitmap NewArchive(int s = 48) => Render(s, DrawArchive);
        public static Bitmap OpenArchive(int s = 48) => Render(s, DrawOpenFolder);
        public static Bitmap Extract(int s = 48) => Render(s, DrawExtract);
        public static Bitmap Browse(int s = 48) => Render(s, DrawBrowse);
        public static Bitmap Test(int s = 48) => Render(s, DrawShieldCheck);
        public static Bitmap Checksum(int s = 48) => Render(s, DrawHash);
        public static Bitmap Benchmark(int s = 48) => Render(s, DrawGauge);
        public static Bitmap Settings(int s = 48) => Render(s, DrawGear);

        private static Bitmap Render(int size, System.Action<Graphics, int> draw)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g, size);
            }
            return bmp;
        }

        private static void DrawFolderBody(Graphics g, int s)
        {
            float u = s / 48f;
            var body = new RectangleF(6 * u, 16 * u, 36 * u, 24 * u);
            var tab = new RectangleF(6 * u, 12 * u, 16 * u, 8 * u);
            using (var brush = new SolidBrush(Manila))
            using (var pen = new Pen(ManilaEdge, 1.5f * u))
            {
                g.FillRectangle(brush, tab);
                g.FillRectangle(brush, body);
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            }
        }

        private static void DrawArchive(Graphics g, int s)
        {
            float u = s / 48f;
            DrawFolderBody(g, s);
            // Zipper down the middle.
            using (var pen = new Pen(ManilaEdge, 1.4f * u))
            {
                float x = 24 * u;
                for (float y = 18 * u; y < 38 * u; y += 4 * u)
                    g.DrawLine(pen, x - 3 * u, y, x + 3 * u, y);
                g.DrawLine(pen, x, 16 * u, x, 40 * u);
            }
        }

        private static void DrawOpenFolder(Graphics g, int s)
        {
            float u = s / 48f;
            using (var brush = new SolidBrush(Manila))
            using (var pen = new Pen(ManilaEdge, 1.5f * u))
            {
                var back = new RectangleF(6 * u, 14 * u, 36 * u, 26 * u);
                g.FillRectangle(brush, back);
                g.DrawRectangle(pen, back.X, back.Y, back.Width, back.Height);
                using (var front = new SolidBrush(Color.FromArgb(255, 222, 140)))
                {
                    PointF[] pts =
                    {
                        new PointF(10 * u, 22 * u), new PointF(46 * u, 22 * u),
                        new PointF(40 * u, 40 * u), new PointF(4 * u, 40 * u)
                    };
                    g.FillPolygon(front, pts);
                    g.DrawPolygon(pen, pts);
                }
            }
        }

        private static void DrawExtract(Graphics g, int s)
        {
            float u = s / 48f;
            DrawFolderBody(g, s);
            using (var brush = new SolidBrush(Green))
            using (var pen = new Pen(Green, 2 * u))
            {
                float cx = 24 * u;
                g.DrawLine(pen, cx, 18 * u, cx, 32 * u);
                PointF[] head =
                {
                    new PointF(cx - 6 * u, 28 * u), new PointF(cx + 6 * u, 28 * u), new PointF(cx, 36 * u)
                };
                g.FillPolygon(brush, head);
            }
        }

        private static void DrawBrowse(Graphics g, int s)
        {
            float u = s / 48f;
            // A document page with lines plus a magnifier.
            using (var paper = new SolidBrush(Paper))
            using (var pen = new Pen(Ink, 1.3f * u))
            {
                var page = new RectangleF(12 * u, 8 * u, 24 * u, 32 * u);
                g.FillRectangle(paper, page);
                g.DrawRectangle(pen, page.X, page.Y, page.Width, page.Height);
                using (var line = new Pen(Color.FromArgb(150, 150, 150), 1.1f * u))
                    for (int i = 0; i < 5; i++)
                        g.DrawLine(line, 16 * u, (14 + i * 4) * u, 32 * u, (14 + i * 4) * u);
            }
            using (var pen = new Pen(Blue, 2.4f * u))
            {
                g.DrawEllipse(pen, 26 * u, 26 * u, 12 * u, 12 * u);
                g.DrawLine(pen, 37 * u, 37 * u, 43 * u, 43 * u);
            }
        }

        private static void DrawShieldCheck(Graphics g, int s)
        {
            float u = s / 48f;
            PointF[] shield =
            {
                new PointF(24 * u, 6 * u), new PointF(40 * u, 12 * u),
                new PointF(40 * u, 26 * u), new PointF(24 * u, 42 * u),
                new PointF(8 * u, 26 * u), new PointF(8 * u, 12 * u)
            };
            using (var brush = new SolidBrush(Color.FromArgb(213, 232, 212)))
            using (var pen = new Pen(Green, 2 * u))
            {
                g.FillPolygon(brush, shield);
                g.DrawPolygon(pen, shield);
                g.DrawLines(pen, new[]
                {
                    new PointF(17 * u, 24 * u), new PointF(22 * u, 30 * u), new PointF(32 * u, 16 * u)
                });
            }
        }

        private static void DrawHash(Graphics g, int s)
        {
            float u = s / 48f;
            using (var pen = new Pen(Blue, 3 * u))
            {
                g.DrawLine(pen, 18 * u, 10 * u, 14 * u, 40 * u);
                g.DrawLine(pen, 32 * u, 10 * u, 28 * u, 40 * u);
                g.DrawLine(pen, 10 * u, 20 * u, 40 * u, 20 * u);
                g.DrawLine(pen, 8 * u, 30 * u, 38 * u, 30 * u);
            }
        }

        private static void DrawGauge(Graphics g, int s)
        {
            float u = s / 48f;
            var rect = new RectangleF(8 * u, 10 * u, 32 * u, 32 * u);
            using (var brush = new SolidBrush(Color.FromArgb(235, 235, 235)))
            using (var pen = new Pen(Ink, 2 * u))
            {
                // FillPie has no RectangleF overload; pass the float rectangle explicitly.
                g.FillPie(brush, rect.X, rect.Y, rect.Width, rect.Height, 180, 180);
                g.DrawArc(pen, rect, 180, 180);
            }
            using (var needle = new Pen(Color.FromArgb(200, 40, 40), 2.4f * u))
                g.DrawLine(needle, 24 * u, 26 * u, 34 * u, 16 * u);
            using (var hub = new SolidBrush(Ink))
                g.FillEllipse(hub, 21 * u, 23 * u, 6 * u, 6 * u);
        }

        private static void DrawGear(Graphics g, int s)
        {
            float u = s / 48f;
            using (var pen = new Pen(Ink, 2 * u))
            using (var brush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                var outer = new RectangleF(12 * u, 12 * u, 24 * u, 24 * u);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * System.Math.PI / 4;
                    float x = 24 * u + (float)System.Math.Cos(a) * 16 * u;
                    float y = 24 * u + (float)System.Math.Sin(a) * 16 * u;
                    g.FillRectangle(brush, x - 3 * u, y - 3 * u, 6 * u, 6 * u);
                }
                g.FillEllipse(brush, outer.X, outer.Y, outer.Width, outer.Height);
                g.DrawEllipse(pen, outer.X, outer.Y, outer.Width, outer.Height);
                g.DrawEllipse(pen, 20 * u, 20 * u, 8 * u, 8 * u);
            }
        }
    }
}
