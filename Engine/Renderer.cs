// ============================================================
// Renderer.cs — Tüm parametreler RenderConfig'den okunur
// ============================================================

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using G_1_A3D_f.World;

namespace G_1_A3D_f.Engine
{
    public class Renderer : IDisposable
    {
        private readonly int _cols;
        private readonly int _rows;
        private readonly int _cw;
        private readonly int _ch;

        private readonly char[]  _chars;
        private readonly int[]   _colorsR;
        private readonly int[]   _colorsG;
        private readonly int[]   _colorsB;

        private readonly Dictionary<char, bool[]> _glyphs = new();
        private readonly Bitmap _frameBmp;
        private readonly Font   _font;

        private static readonly char[] USED_CHARS =
        {
            ' ', '.', ':', ';', '+', 'x', 'X', '%', '#', '-', '|', ',', '\'',
            (char)9608, (char)9619, (char)9618, (char)9617
        };

        public Renderer(int cols, int rows, int cellW, int cellH, float fontSize)
        {
            _cols = cols; _rows = rows; _cw = cellW; _ch = cellH;
            int total = cols * rows;
            _chars   = new char[total];
            _colorsR = new int [total];
            _colorsG = new int [total];
            _colorsB = new int [total];
            _frameBmp = new Bitmap(cols * cellW, rows * cellH, PixelFormat.Format32bppArgb);

            try   { _font = new Font("Consolas",    fontSize, FontStyle.Regular, GraphicsUnit.Point); }
            catch { _font = new Font("Courier New", fontSize, FontStyle.Regular, GraphicsUnit.Point); }

            BuildGlyphAtlas();
        }

        private void BuildGlyphAtlas()
        {
            using var bmp = new Bitmap(_cw, _ch, PixelFormat.Format32bppArgb);
            using var g   = Graphics.FromImage(bmp);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            foreach (char c in USED_CHARS)
            {
                g.Clear(Color.Black);
                TextRenderer.DrawText(g, c.ToString(), _font,
                    new Point(0, 0), Color.White,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                bool[] pixels = new bool[_cw * _ch];
                for (int py = 0; py < _ch; py++)
                    for (int px2 = 0; px2 < _cw; px2++)
                        pixels[py * _cw + px2] = bmp.GetPixel(px2, py).R > 80;
                _glyphs[c] = pixels;
            }
            if (!_glyphs.ContainsKey(' '))
                _glyphs[' '] = new bool[_cw * _ch];
        }

        // ── Yoğunluk → Karakter dönüşümü ─────────────────────
        // density: 0.0 = seyrek, 1.0 = dolu blok
        // d:       mesafe faktörü (0=yakın, 1=uzak)
        private static char DensityChar(float d, float densityBias)
        {
            float v = d - densityBias * 0.5f;   // Yoğunluk artınca eşikler aşağı kayar
            if      (v < 0.06f) return (char)9608;  // █
            else if (v < 0.16f) return (char)9619;  // ▓
            else if (v < 0.30f) return (char)9618;  // ▒
            else if (v < 0.46f) return '#';
            else if (v < 0.60f) return '%';
            else if (v < 0.74f) return 'X';
            else if (v < 0.86f) return 'x';
            else                return ':';
        }

        // ── Işık ──────────────────────────────────────────────
        private static float PointLight(float dist, float falloff, float ambient)
            => Math.Max(1.0f / (1.0f + dist * falloff), ambient);

        // ─────────────────────────────────────────────────────────
        // DUVAR
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) WallPixel(
            float u, float v, float dist, bool isNS, RenderConfig cfg)
        {
            float BW = cfg.BrickW, BH = cfg.BrickH, GR = cfg.GroutW;
            float vS   = v / BH;
            int   bRow = (int)MathF.Floor(vS);
            float fv   = vS - bRow;
            float uOff = (bRow % 2 == 0) ? 0f : BW * 0.5f;
            float uS   = (u + uOff) / BW;
            int   bCol = (int)MathF.Floor(uS);
            float fu   = uS - bCol;

            bool  hG    = fv < GR || fv > (1f - GR);
            bool  vG    = fu < GR || fu > (1f - GR);
            int   hash  = (bRow * 17 + bCol * 31) & 0xFF;
            float rough = (hash & 0xF) / 24f;
            float toneV = ((hash >> 4) / 15f) * 0.24f - 0.12f;

            float light    = PointLight(dist, cfg.LightFalloff, cfg.Ambient);
            float faceMult = isNS ? cfg.WallNSMult : cfg.WallEWMult;
            float ao       = 1.0f;
            float edgeDist = Math.Min(Math.Min(fu, 1f - fu), Math.Min(fv, 1f - fv));
            if (edgeDist < 0.15f) ao = 0.75f + (edgeDist / 0.15f) * 0.25f;
            float br = light * faceMult * ao;

            char ch; int cr, cg, cb;
            if (hG || vG)
            {
                ch = (hG && vG) ? '+' : hG ? '-' : '|';
                float moss = ((bRow * 3 + bCol * 5) & 0x7) / 14f;
                cr = (int)Math.Clamp((88 + moss * 20) * br, 0, 255);
                cg = (int)Math.Clamp((98 + moss * 55) * br, 0, 255);
                cb = (int)Math.Clamp((72 + moss * 15) * br, 0, 255);
            }
            else
            {
                bool m1 = isNS && dist < 10f && fv > 0.70f && (fu < 0.04f || fu > 0.96f);
                bool m2 = isNS && dist < 10f && fv < 0.16f && fu > 0.42f && fu < 0.58f;
                if (m1 || m2)
                {
                    ch = m2 ? ',' : ';';
                    cr = (int)Math.Clamp((28 + rough * 18) * br, 0, 255);
                    cg = (int)Math.Clamp((92 + rough * 38) * br, 0, 255);
                    cb = (int)Math.Clamp((18 + rough * 12) * br, 0, 255);
                }
                else
                {
                    float baseR = cfg.WallBaseR + rough * 32 + toneV * 40;
                    float baseG = cfg.WallBaseG + rough * 18 + toneV * 15;
                    float baseB = cfg.WallBaseB + rough *  8 + toneV *  8;
                    float d     = dist / cfg.MaxDepth + rough * 0.18f;
                    ch = DensityChar(d, cfg.WallDensity);
                    cr = (int)Math.Clamp(baseR * br, 0, 255);
                    cg = (int)Math.Clamp(baseG * br, 0, 255);
                    cb = (int)Math.Clamp(baseB * br, 0, 255);
                }
            }
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // TAVAN
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) CeilingPixel(
            float wx, float wy, float rowDist, RenderConfig cfg)
        {
            // Tile ölçeği: Büyük değer = daha büyük kareler = daha az derz çizgisi
            float bx = wx / cfg.CeilTileScale, by = wy / cfg.CeilTileScale;
            float fx = bx - MathF.Floor(bx), fy = by - MathF.Floor(by);
            float g_ = cfg.CeilGrout;
            bool  gr = fx < g_ || fx > (1f-g_) || fy < g_ || fy > (1f-g_);
            bool  co = (fx < g_ || fx > (1f-g_)) && (fy < g_ || fy > (1f-g_));
            int   hash  = ((int)MathF.Floor(bx) * 7 ^ (int)MathF.Floor(by) * 13) & 0xFF;
            float noise = (hash & 0xF) / 20f;
            float tv    = ((hash >> 4) / 15f) * 0.18f - 0.09f;

            float t      = Math.Clamp(rowDist / cfg.MaxDepth, 0f, 1f);
            float bright = Math.Max(cfg.CeilBright - t * cfg.CeilFade, cfg.Ambient);

            char ch;
            if (gr)
                ch = co ? '+' : (fx < g_ || fx > (1f-g_)) ? '|' : '-';
            else
                ch = DensityChar(t * 0.6f + noise * 0.4f, cfg.CeilDensity);

            int cr  = (int)Math.Clamp((cfg.CeilBaseR + noise * 18 + tv * 30) * bright, 0, 255);
            int cg  = (int)Math.Clamp((cfg.CeilBaseG + noise * 12 + tv * 20) * bright, 0, 255);
            int cb  = (int)Math.Clamp((cfg.CeilBaseB + noise *  8 + tv * 10) * bright, 0, 255);
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // ZEMİN
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) FloorPixel(
            float wx, float wy, float rowDist, RenderConfig cfg)
        {
            float tx = wx / cfg.FloorTileScale, ty = wy / cfg.FloorTileScale;
            float fx = tx - MathF.Floor(tx), fy = ty - MathF.Floor(ty);
            float g_ = cfg.FloorGrout;
            bool  gr = fx < g_ || fx > (1f-g_) || fy < g_ || fy > (1f-g_);
            bool  co = (fx < g_ || fx > (1f-g_)) && (fy < g_ || fy > (1f-g_));
            int   hash  = ((int)MathF.Floor(tx) * 5 ^ (int)MathF.Floor(ty) * 11) & 0xFF;
            float noise = (hash & 0xF) / 20f;
            float tv    = ((hash >> 4) / 15f) * 0.20f - 0.10f;

            float t      = Math.Clamp(rowDist / cfg.MaxDepth, 0f, 1f);
            float bright = Math.Max(cfg.FloorBright - t * cfg.FloorFade, cfg.Ambient);

            char ch;
            if (gr)
                ch = co ? '+' : (fx < g_ || fx > (1f-g_)) ? '|' : '-';
            else
                ch = DensityChar(t * 0.6f + noise * 0.4f, cfg.FloorDensity);

            int cr, cg, cb;
            if (gr)
            {
                cr = (int)Math.Clamp((52 + noise * 12) * bright, 0, 255);
                cg = (int)Math.Clamp((46 + noise * 16) * bright, 0, 255);
                cb = (int)Math.Clamp((28 + noise *  6) * bright, 0, 255);
            }
            else
            {
                cr = (int)Math.Clamp((cfg.FloorBaseR + noise * 28 + tv * 35) * bright, 0, 255);
                cg = (int)Math.Clamp((cfg.FloorBaseG + noise * 18 + tv * 22) * bright, 0, 255);
                cb = (int)Math.Clamp((cfg.FloorBaseB + noise *  8 + tv * 12) * bright, 0, 255);
            }
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // Render
        // ─────────────────────────────────────────────────────────
        public void Render(Graphics g, float px, float py, float angle, float pitch,
                           Map map, RenderConfig cfg)
        {
            float fovRad  = cfg.Fov * MathF.PI / 180f;
            int   pitchOff = (int)pitch;
            int   horizon  = Math.Clamp(_rows / 2 + pitchOff, 0, _rows - 1);

            float leftAngle  = angle - fovRad * 0.5f;
            float rightAngle = angle + fovRad * 0.5f;
            float lx = MathF.Sin(leftAngle),  ly = MathF.Cos(leftAngle);
            float rx = MathF.Sin(rightAngle), ry = MathF.Cos(rightAngle);

            // ── Zemin + Tavan ──
            for (int row = 0; row < _rows; row++)
            {
                bool isFloor   = row > horizon;
                bool isCeiling = row < horizon;
                if (!isFloor && !isCeiling)
                {
                    int rb = row * _cols;
                    for (int col = 0; col < _cols; col++)
                    { int i = rb + col; _chars[i] = ' '; _colorsR[i] = 0; _colorsG[i] = 0; _colorsB[i] = 0; }
                    continue;
                }

                float posZ    = isFloor ? (row - horizon) : (horizon - row);
                if (posZ < 0.5f) posZ = 0.5f;
                float rowDist = ((float)_rows * 0.5f) / posZ;

                float fx0 = px + lx * rowDist, fy0 = py + ly * rowDist;
                float fx1 = px + rx * rowDist, fy1 = py + ry * rowDist;
                float sx  = (fx1 - fx0) / (_cols - 1);
                float sy  = (fy1 - fy0) / (_cols - 1);

                int   rb2 = row * _cols;
                float wx  = fx0, wy = fy0;

                for (int col = 0; col < _cols; col++)
                {
                    int idx = rb2 + col;
                    if (isFloor)
                    {
                        var (ch, r, gv, b) = FloorPixel(wx, wy, rowDist, cfg);
                        _chars[idx] = ch; _colorsR[idx] = r; _colorsG[idx] = gv; _colorsB[idx] = b;
                    }
                    else
                    {
                        var (ch, r, gv, b) = CeilingPixel(wx, wy, rowDist, cfg);
                        _chars[idx] = ch; _colorsR[idx] = r; _colorsG[idx] = gv; _colorsB[idx] = b;
                    }
                    wx += sx; wy += sy;
                }
            }

            // ── Duvarlar ──
            for (int col = 0; col < _cols; col++)
            {
                float rayAngle = (angle - fovRad * 0.5f) + ((float)col / (_cols - 1)) * fovRad;
                float ex = MathF.Sin(rayAngle), ey = MathF.Cos(rayAngle);

                float dist = 0f; bool hit = false; bool isNS = false; float wallU = 0f;
                while (!hit && dist < cfg.MaxDepth)
                {
                    dist += cfg.RayStep;
                    int mx = (int)(px + ex * dist), my = (int)(py + ey * dist);
                    if (mx < 0 || mx >= map.Width || my < 0 || my >= map.Height)
                    { hit = true; dist = cfg.MaxDepth; }
                    else if (map.IsWall(mx, my))
                    {
                        hit  = true;
                        isNS = MathF.Abs(ey) > MathF.Abs(ex);
                        wallU = isNS ? (px + ex * dist) : (py + ey * dist);
                    }
                }

                if (dist >= cfg.MaxDepth) continue;

                float perpDist = dist * MathF.Cos(rayAngle - angle);
                if (perpDist < 0.01f) perpDist = 0.01f;

                int wallH   = (int)(_rows / perpDist);
                int wallTop = horizon - wallH / 2;
                int wallBot = horizon + wallH / 2;
                int drawTop = Math.Max(0,         wallTop);
                int drawBot = Math.Min(_rows - 1, wallBot);

                for (int row = drawTop; row <= drawBot; row++)
                {
                    int   idx = row * _cols + col;
                    float v   = (float)(row - wallTop) / Math.Max(1, wallBot - wallTop);
                    v = Math.Clamp(v, 0f, 1f);
                    var (ch, r, gv, b) = WallPixel(wallU, v, perpDist, isNS, cfg);
                    _chars[idx] = ch; _colorsR[idx] = r; _colorsG[idx] = gv; _colorsB[idx] = b;
                }
            }

            DrawBuffer(g);
        }

        private unsafe void DrawBuffer(Graphics g)
        {
            int W = _cols * _cw, H = _rows * _ch;
            var bmpData = _frameBmp.LockBits(
                new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            int   stride = bmpData.Stride;
            byte* scan0  = (byte*)bmpData.Scan0;
            bool[] empty = _glyphs.GetValueOrDefault(' ', new bool[_cw * _ch]);

            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    int    idx   = row * _cols + col;
                    char   c     = _chars[idx];
                    int    cr    = _colorsR[idx];
                    int    cg    = _colorsG[idx];
                    int    cb    = _colorsB[idx];
                    bool[] glyph = _glyphs.TryGetValue(c, out var gl) ? gl : empty;
                    int baseX = col * _cw, baseY = row * _ch;
                    for (int gy = 0; gy < _ch; gy++)
                    {
                        byte* ptr = scan0 + (baseY + gy) * stride + baseX * 4;
                        for (int gx = 0; gx < _cw; gx++)
                        {
                            if (glyph[gy * _cw + gx])
                            { ptr[0] = (byte)cb; ptr[1] = (byte)cg; ptr[2] = (byte)cr; ptr[3] = 255; }
                            else
                            { ptr[0] = 0; ptr[1] = 0; ptr[2] = 0; ptr[3] = 255; }
                            ptr += 4;
                        }
                    }
                }
            }
            _frameBmp.UnlockBits(bmpData);
            // Her zaman 1920x1080 alana ölçekle
            g.DrawImage(_frameBmp, 0, 0, 1920, 1080);
        }

        public void Dispose()
        {
            _frameBmp.Dispose();
            _font.Dispose();
        }
    }
}