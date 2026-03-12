// ============================================================
// Renderer.cs — Gelişmiş render: Işık + AO + Renk Varyasyonu
//
// YENİ ÖZELLİKLER:
//   1. Nokta Işık: Oyuncu etrafı parlak, mesafe arttıkça karardır
//      Formül: light = 1 / (1 + dist * FALLOFF)
//   2. Ambient Occlusion: Duvar köşelerinde ekstra kararma
//      Her duvar bloğunun komşu bloklara bakarak köşe tespiti
//   3. Renk Varyasyonu: Her tuğla bloğu ±tone fark
//      Hash tabanlı deterministik → her oyunda aynı ama blok blok farklı
// ============================================================

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using G_1_A3D_f.World;

namespace G_1_A3D_f.Engine
{
    public class Renderer
    {
        private readonly int _cols;
        private readonly int _rows;
        private readonly int _cw;
        private readonly int _ch;

        private const float FOV       = MathF.PI / 3.0f;
        private const float MAX_DEPTH = 22.0f;
        private const float STEP      = 0.012f;

        // Işık düşüş katsayısı: Büyük = daha hızlı kararma
        private const float LIGHT_FALLOFF = 0.18f;
        // Minimum ambient ışık: Karanlıkta bile bu kadar görünür
        private const float AMBIENT       = 0.15f;

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

        public Renderer(int cols, int rows, int cellW, int cellH)
        {
            _cols = cols; _rows = rows; _cw = cellW; _ch = cellH;
            int total = cols * rows;
            _chars   = new char[total];
            _colorsR = new int [total];
            _colorsG = new int [total];
            _colorsB = new int [total];
            _frameBmp = new Bitmap(cols * cellW, rows * cellH, PixelFormat.Format32bppArgb);

            try   { _font = new Font("Consolas",    8f, FontStyle.Regular, GraphicsUnit.Point); }
            catch { _font = new Font("Courier New", 8f, FontStyle.Regular, GraphicsUnit.Point); }

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

        // ─────────────────────────────────────────────────────────
        // IŞIK HESABI
        // pointLight: Mesafeye göre düşen ışık yoğunluğu
        //   1 / (1 + dist * falloff) → 0.0 - 1.0 arası
        //   dist=0: 1.0 (tam parlak), dist→∞: 0.0 (karanlık)
        // Sonuç AMBIENT ile kırpılır: Hiçbir yer sıfıra düşmez
        // ─────────────────────────────────────────────────────────
        private static float PointLight(float dist)
        {
            float light = 1.0f / (1.0f + dist * LIGHT_FALLOFF);
            return Math.Max(light, AMBIENT);
        }

        // ─────────────────────────────────────────────────────────
        // DUVAR DOKUSU — Işık + Renk Varyasyonu + AO
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) WallPixel(
            float u, float v, float dist, bool isNS)
        {
            const float BW = 0.85f, BH = 0.42f, GR = 0.055f;
            float vS   = v / BH;
            int   bRow = (int)MathF.Floor(vS);
            float fv   = vS - bRow;
            float uOff = (bRow % 2 == 0) ? 0f : BW * 0.5f;
            float uS   = (u + uOff) / BW;
            int   bCol = (int)MathF.Floor(uS);
            float fu   = uS - bCol;

            bool  hG    = fv < GR || fv > (1f - GR);
            bool  vG    = fu < GR || fu > (1f - GR);

            // Renk varyasyonu: Her tuğla bloğu için ±0.12 ton farkı
            // Hash deterministik → aynı blok her karede aynı tonda
            int   hash    = (bRow * 17 + bCol * 31) & 0xFF;
            float rough   = (hash & 0xF) / 24f;
            float toneVar = ((hash >> 4) / 15f) * 0.24f - 0.12f;  // -0.12 → +0.12

            // Işık hesabı
            float light = PointLight(dist);
            // NS yüzü daha aydınlık (güneş açısı simülasyonu)
            float faceMult = isNS ? 1.0f : 0.72f;
            float br = light * faceMult;

            // Ambient Occlusion: Köşelere yakın piksel karartması
            // Tuğla bloğunun köşelerine yaklaştıkça ekstra kararma
            float ao = 1.0f;
            float edgeDist = Math.Min(Math.Min(fu, 1f - fu), Math.Min(fv, 1f - fv));
            if (edgeDist < 0.15f)
                ao = 0.75f + (edgeDist / 0.15f) * 0.25f;   // 0.75 → 1.0 arası

            br *= ao;

            char ch; int cr, cg, cb;
            if (hG || vG)
            {
                ch = (hG && vG) ? '+' : hG ? '-' : '|';
                float moss = ((bRow * 3 + bCol * 5) & 0x7) / 14f;
                cr = (int)Math.Clamp((88  + moss * 20) * br, 0, 255);
                cg = (int)Math.Clamp((98  + moss * 55) * br, 0, 255);
                cb = (int)Math.Clamp((72  + moss * 15) * br, 0, 255);
            }
            else
            {
                bool m1 = fv > 0.68f && (fu < 0.10f || fu > 0.90f);
                bool m2 = fv < 0.18f && fu > 0.38f && fu < 0.62f;
                if (m1 || m2)
                {
                    ch = m2 ? ',' : ';';
                    cr = (int)Math.Clamp((28 + rough * 18) * br, 0, 255);
                    cg = (int)Math.Clamp((92 + rough * 38) * br, 0, 255);
                    cb = (int)Math.Clamp((18 + rough * 12) * br, 0, 255);
                }
                else
                {
                    // Renk varyasyonu burada uygulanır
                    float baseR = 118 + rough * 32 + toneVar * 40;
                    float baseG =  52 + rough * 18 + toneVar * 15;
                    float baseB =  30 + rough *  8 + toneVar *  8;

                    float d = dist / MAX_DEPTH + rough * 0.28f;
                    ch = d < 0.08f ? '#' : d < 0.20f ? '%' : d < 0.35f ? 'X' :
                         d < 0.50f ? 'x' : d < 0.65f ? ':' : d < 0.80f ? '.' : ' ';

                    cr = (int)Math.Clamp(baseR * br, 0, 255);
                    cg = (int)Math.Clamp(baseG * br, 0, 255);
                    cb = (int)Math.Clamp(baseB * br, 0, 255);
                }
            }
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // TAVAN DOKUSU — Işık entegreli
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) CeilingPixel(
            float wx, float wy, float rowDist)
        {
            const float BLOCK = 1.0f;
            float bx = wx / BLOCK, by = wy / BLOCK;
            float fx = bx - MathF.Floor(bx), fy = by - MathF.Floor(by);
            bool  gr = fx < 0.07f || fx > 0.93f || fy < 0.07f || fy > 0.93f;
            bool  co = (fx < 0.07f || fx > 0.93f) && (fy < 0.07f || fy > 0.93f);
            int   hash  = ((int)MathF.Floor(bx) * 7 ^ (int)MathF.Floor(by) * 13) & 0xFF;
            float noise = (hash & 0xF) / 20f;
            float tv    = ((hash >> 4) / 15f) * 0.18f - 0.09f;

            float light  = PointLight(rowDist);
            // Tavan: Daha fazla ambient (koyu değil ama loş)
            float bright = Math.Max(light * 0.70f, 0.30f);

            char ch;
            if (gr)
                ch = co ? '+' : (fx < 0.07f || fx > 0.93f) ? '|' : '-';
            else
            {
                float d = (rowDist / MAX_DEPTH) * 0.7f + noise * 0.3f;
                ch = d < 0.15f ? '#' : d < 0.30f ? '%' : d < 0.45f ? 'X' :
                     d < 0.60f ? 'x' : d < 0.75f ? ':' : '.';
            }

            int cr  = (int)Math.Clamp((68 + noise * 18 + tv * 30) * bright, 0, 255);
            int cg  = (int)Math.Clamp((48 + noise * 12 + tv * 20) * bright, 0, 255);
            int cb  = (int)Math.Clamp((30 + noise *  8 + tv * 10) * bright, 0, 255);
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // ZEMİN DOKUSU — Işık entegreli
        // ─────────────────────────────────────────────────────────
        private static (char ch, int r, int g, int b) FloorPixel(
            float wx, float wy, float rowDist)
        {
            const float TILE = 1.0f;
            float tx = wx / TILE, ty = wy / TILE;
            float fx = tx - MathF.Floor(tx), fy = ty - MathF.Floor(ty);
            bool  gr = fx < 0.07f || fx > 0.93f || fy < 0.07f || fy > 0.93f;
            bool  co = (fx < 0.07f || fx > 0.93f) && (fy < 0.07f || fy > 0.93f);
            int   hash  = ((int)MathF.Floor(tx) * 5 ^ (int)MathF.Floor(ty) * 11) & 0xFF;
            float noise = (hash & 0xF) / 20f;
            float tv    = ((hash >> 4) / 15f) * 0.20f - 0.10f;

            float light  = PointLight(rowDist);
            float bright = Math.Max(light * 0.85f, 0.25f);

            char ch;
            if (gr)
                ch = co ? '+' : (fx < 0.07f || fx > 0.93f) ? '|' : '-';
            else
            {
                float d = (rowDist / MAX_DEPTH) * 0.7f + noise * 0.3f;
                ch = d < 0.15f ? '#' : d < 0.30f ? '%' : d < 0.45f ? 'X' :
                     d < 0.60f ? 'x' : d < 0.75f ? ':' : '.';
            }

            int cr, cg, cb;
            if (gr)
            {
                cr = (int)Math.Clamp((52 + noise * 12) * bright, 0, 255);
                cg = (int)Math.Clamp((46 + noise * 16) * bright, 0, 255);
                cb = (int)Math.Clamp((28 + noise *  6) * bright, 0, 255);
            }
            else
            {
                cr = (int)Math.Clamp((92 + noise * 28 + tv * 35) * bright, 0, 255);
                cg = (int)Math.Clamp((66 + noise * 18 + tv * 22) * bright, 0, 255);
                cb = (int)Math.Clamp((40 + noise *  8 + tv * 12) * bright, 0, 255);
            }
            return (ch, cr, cg, cb);
        }

        // ─────────────────────────────────────────────────────────
        // Render — Zemin/Tavan önce, duvarlar üstüne
        // ─────────────────────────────────────────────────────────
        public void Render(Graphics g, float px, float py, float angle, float pitch, Map map)
        {
            int pitchOff = (int)pitch;
            int horizon  = Math.Clamp(_rows / 2 + pitchOff, 0, _rows - 1);

            float leftAngle  = angle - FOV * 0.5f;
            float rightAngle = angle + FOV * 0.5f;
            float lx = MathF.Sin(leftAngle),  ly = MathF.Cos(leftAngle);
            float rx = MathF.Sin(rightAngle), ry = MathF.Cos(rightAngle);

            // ── ADIM 1: Zemin + Tavan ──
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
                        var (ch, r, gv, b) = FloorPixel(wx, wy, rowDist);
                        _chars[idx] = ch; _colorsR[idx] = r; _colorsG[idx] = gv; _colorsB[idx] = b;
                    }
                    else
                    {
                        var (ch, r, gv, b) = CeilingPixel(wx, wy, rowDist);
                        _chars[idx] = ch; _colorsR[idx] = r; _colorsG[idx] = gv; _colorsB[idx] = b;
                    }
                    wx += sx; wy += sy;
                }
            }

            // ── ADIM 2: Duvarlar (üstüne yazar) ──
            for (int col = 0; col < _cols; col++)
            {
                float rayAngle = (angle - FOV * 0.5f) + ((float)col / (_cols - 1)) * FOV;
                float ex = MathF.Sin(rayAngle), ey = MathF.Cos(rayAngle);

                float dist = 0f; bool hit = false; bool isNS = false; float wallU = 0f;
                while (!hit && dist < MAX_DEPTH)
                {
                    dist += STEP;
                    int mx = (int)(px + ex * dist), my = (int)(py + ey * dist);
                    if (mx < 0 || mx >= map.Width || my < 0 || my >= map.Height)
                    { hit = true; dist = MAX_DEPTH; }
                    else if (map.IsWall(mx, my))
                    {
                        hit  = true;
                        isNS = MathF.Abs(ey) > MathF.Abs(ex);
                        wallU = isNS ? (px + ex * dist) : (py + ey * dist);
                    }
                }

                if (dist >= MAX_DEPTH) continue;

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
                    float wh  = Math.Max(1, drawBot - drawTop);
                    float v   = (row - drawTop) / wh;
                    var (ch, r, gv, b) = WallPixel(wallU, v, perpDist, isNS);
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
            g.DrawImageUnscaled(_frameBmp, 0, 0);
        }
    }
}