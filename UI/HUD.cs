// ============================================================
// HUD.cs — Gerçek ekran boyutuyla her karede çalışan HUD
//
// Constructor artık ekran boyutu almıyor.
// DrawAll her karede sw/sh (gerçek ClientSize) alır.
// Bu sayede DPI ölçeklendirmesi, pencere boyutu farkı
// gibi durumlar otomatik düzeltilir.
// ============================================================

using System.Drawing;
using System.Windows.Forms;

namespace G_1_A3D_f.UI
{
    public class HUD : IDisposable
    {
        private readonly Font _fontUI;
        private readonly Font _fontLabel;

        // Ölçülmüş boyutlar — 400×100 bitmap ile güvenli ölçüm
        private readonly int _uiCharH;
        private readonly int _lblCharH;


        public HUD()
        {
            _fontUI     = new Font("Consolas", 12f, FontStyle.Bold,    GraphicsUnit.Point);
            _fontLabel  = new Font("Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point);

            // Yeterince büyük bitmap ile güvenli ölçüm
            using var bmp = new Bitmap(400, 100);
            using var g   = Graphics.FromImage(bmp);

            var uiSz  = TextRenderer.MeasureText(g, "HP  100%", _fontUI,
                new Size(400, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            var lblSz = TextRenderer.MeasureText(g, "W", _fontLabel,
                new Size(400, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Fallback: 96DPI'da Consolas 12pt ≈ 9×18px
            _uiCharH  = uiSz.Height  > 6 ? uiSz.Height  : 18;
            _lblCharH = lblSz.Height > 6 ? lblSz.Height : 15;
        }

        // sw, sh: Her karede GameForm.ClientSize'dan gelen gerçek piksel boyutu
        public void DrawAll(
            Graphics g,
            int      sw,
            int      sh,
            float    _bobOffset,
            int      health,
            int      ammo,
            bool[,]  mapData,
            float    px,
            float    py,
            float    angle)
        {
            DrawCrosshair(g, sw, sh);
            DrawStats(g, sh, health, ammo);
            DrawMinimap(g, sw, sh, mapData, px, py, angle);
        }

        // ── Nişangah ──────────────────────────────────────────
        private static void DrawCrosshair(Graphics g, int sw, int sh)
        {
            int cx = sw / 2;
            int cy = sh / 2;
            const int GAP = 5;
            const int LEN = 14;

            // Gölge katmanı
            using var shadow = new Pen(Color.FromArgb(170, 0, 0, 0), 3.5f);
            g.DrawLine(shadow, cx - LEN - GAP, cy, cx - GAP,        cy);
            g.DrawLine(shadow, cx + GAP,        cy, cx + LEN + GAP, cy);
            g.DrawLine(shadow, cx, cy - LEN - GAP, cx, cy - GAP);
            g.DrawLine(shadow, cx, cy + GAP,        cx, cy + LEN + GAP);

            // Ana renk
            using var pen = new Pen(Color.FromArgb(245, 55, 255, 115), 1.5f);
            g.DrawLine(pen, cx - LEN - GAP, cy, cx - GAP,        cy);
            g.DrawLine(pen, cx + GAP,        cy, cx + LEN + GAP, cy);
            g.DrawLine(pen, cx, cy - LEN - GAP, cx, cy - GAP);
            g.DrawLine(pen, cx, cy + GAP,        cx, cy + LEN + GAP);

            // Merkez nokta
            using var dot = new SolidBrush(Color.FromArgb(245, 55, 255, 115));
            g.FillRectangle(dot, cx - 1, cy - 1, 3, 3);
        }

        // ── Can + Mermi ────────────────────────────────────────
        private void DrawStats(Graphics g, int sh, int health, int ammo)
        {
            const int OX    = 20;
            const int BAR_W = 180;
            const int BAR_H = 12;
            const int PAD   = 8;

            int panelH = _uiCharH + BAR_H + _uiCharH + PAD * 3;

            // sh'ye göre alt köşe
            int oy = sh - panelH - 20;

            // Panel arka plan
            using var panelBg = new SolidBrush(Color.FromArgb(145, 8, 8, 8));
            g.FillRectangle(panelBg, OX - 8, oy - 8, BAR_W + 30, panelH + 16);

            // Sol kenar accent
            using var accent = new Pen(Color.FromArgb(170, 110, 75, 40), 2f);
            g.DrawLine(accent, OX - 8, oy - 8, OX - 8, oy - 8 + panelH + 16);

            // Can rengi
            Color hpCol = health > 60
                ? Color.FromArgb(60, 215, 60)
                : health > 30
                    ? Color.FromArgb(215, 190, 40)
                    : Color.FromArgb(215, 50, 50);

            TextRenderer.DrawText(g, $"HP  {health,3}%", _fontUI,
                new Point(OX, oy), hpCol,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Can barı
            int barY   = oy + _uiCharH + 4;
            int filled = (int)(health / 100f * BAR_W);

            using var barBg = new SolidBrush(Color.FromArgb(80, 50, 50, 50));
            g.FillRectangle(barBg, OX, barY, BAR_W, BAR_H);

            using var barFg = new SolidBrush(hpCol);
            if (filled > 0) g.FillRectangle(barFg, OX, barY, filled, BAR_H);

            // Segment çizgileri
            using var segPen = new Pen(Color.FromArgb(55, 0, 0, 0), 1f);
            for (int s = 1; s < 10; s++)
                g.DrawLine(segPen,
                    OX + s * BAR_W / 10, barY,
                    OX + s * BAR_W / 10, barY + BAR_H);

            using var barBorder = new Pen(Color.FromArgb(110, 90, 90, 90), 1f);
            g.DrawRectangle(barBorder, OX, barY, BAR_W, BAR_H);

            // Mermi
            Color ammoCol = ammo > 10
                ? Color.FromArgb(210, 195, 105)
                : Color.FromArgb(210, 100, 50);

            TextRenderer.DrawText(g, $"AMM {ammo,3}", _fontUI,
                new Point(OX, barY + BAR_H + PAD), ammoCol,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        // ── Minimap ────────────────────────────────────────────
        private void DrawMinimap(
            Graphics g, int sw, int sh,
            bool[,] map,
            float px, float py, float angle)
        {
            const int CELL   = 5;
            const int MARGIN = 15;

            int mw = map.GetLength(0);
            int mh = map.GetLength(1);
            int pw = mw * CELL;
            int ph = mh * CELL;

            // sw'ye göre sağ üst köşe
            // MAP etiketi için üstte 20px boşluk bırakılır
            const int LABEL_H = 20;
            int ox = sw - pw - MARGIN;
            int oy = MARGIN + LABEL_H;   // Etiket alanı kadar aşağı

            // Dış gölge (etiket alanı dahil)
            using var outerBg = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(outerBg, ox - 4, oy - LABEL_H - 4, pw + 8, ph + LABEL_H + 8);

            // Harita zemin
            using var mapBg = new SolidBrush(Color.FromArgb(195, 12, 12, 12));
            g.FillRectangle(mapBg, ox, oy, pw, ph);

            // Hücreler
            using var wallBr  = new SolidBrush(Color.FromArgb(220, 140, 105, 70));
            using var floorBr = new SolidBrush(Color.FromArgb(220, 28, 28, 28));

            for (int x = 0; x < mw; x++)
                for (int y = 0; y < mh; y++)
                    g.FillRectangle(
                        map[x, y] ? wallBr : floorBr,
                        ox + x * CELL, oy + y * CELL, CELL - 1, CELL - 1);

            // FOV koni
            int   ppx  = ox + (int)(px * CELL);
            int   ppy  = oy + (int)(py * CELL);
            float fov  = MathF.PI / 3f;
            float flen = CELL * 4.5f;

            int lx1 = ppx + (int)(MathF.Sin(angle - fov / 2) * flen);
            int ly1 = ppy + (int)(MathF.Cos(angle - fov / 2) * flen);
            int lx2 = ppx + (int)(MathF.Sin(angle + fov / 2) * flen);
            int ly2 = ppy + (int)(MathF.Cos(angle + fov / 2) * flen);

            using var foniBr = new SolidBrush(Color.FromArgb(38, 255, 220, 0));
            g.FillPolygon(foniBr, new Point[]
                { new(ppx, ppy), new(lx1, ly1), new(lx2, ly2) });

            // Bakış yönü
            int fx = ppx + (int)(MathF.Sin(angle) * flen * 0.85f);
            int fy = ppy + (int)(MathF.Cos(angle) * flen * 0.85f);
            using var dirPen = new Pen(Color.FromArgb(200, 255, 220, 0), 1f);
            g.DrawLine(dirPen, ppx, ppy, fx, fy);

            // Oyuncu noktası
            using var playerBr = new SolidBrush(Color.FromArgb(255, 255, 220, 0));
            g.FillEllipse(playerBr, ppx - 3, ppy - 3, 6, 6);

            // Çerçeve
            using var border = new Pen(Color.FromArgb(180, 120, 90, 55), 1f);
            g.DrawRectangle(border, ox - 1, oy - 1, pw + 1, ph + 1);

            // MAP etiketi — haritanın hemen üstünde ortalı, ekran içinde
            var mapLabelSz = TextRenderer.MeasureText(g, "MAP", _fontLabel,
                new Size(200, 40), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int labelX = ox + (pw - mapLabelSz.Width) / 2;
            int labelY = oy - mapLabelSz.Height - 2;   // oy zaten LABEL_H kadar aşağıda
            TextRenderer.DrawText(g, "MAP", _fontLabel,
                new Point(labelX, labelY),
                Color.FromArgb(220, 200, 170, 110),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public void Dispose()
        {
            _fontUI.Dispose();
            _fontLabel.Dispose();
        }
    }
}