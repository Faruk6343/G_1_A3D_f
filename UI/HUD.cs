// ============================================================
// HUD.cs — Oyun Arayüzü: Silah + Minimap + Can/Mermi
//
// İÇERİK:
//   1. Silah ASCII çizimi — Ekranın alt ortasında
//      Bob efektiyle hareket ederken sallanır
//   2. Minimap — Sol üst köşede, haritayı küçük gösterir
//   3. Can + Mermi göstergesi — Alt sol
//
// Tüm HUD renderer'ın üstüne çizilir (en son katman)
// ============================================================

using System.Drawing;
using System.Windows.Forms;

namespace G_1_A3D_f.UI
{
    public class HUD
    {
        private readonly int _screenW;
        private readonly int _screenH;
        private readonly Font _fontLarge;
        private readonly Font _fontSmall;
        private readonly Font _fontMono;

        // Silah ASCII çizimi — her satır bir dizi karakter
        // Basit bir tabanca görünümü
        private static readonly string[] WEAPON_LINES =
        {
            @"        ________        ",
            @"       |   ___  |       ",
            @"       |  |   | |___    ",
            @"  _____|  |___| |___|   ",
            @" |___________________|  ",
            @"    |___|               ",
        };

        // Nişangah (crosshair) — ekranın tam ortasında
        private static readonly string[] CROSSHAIR =
        {
            @"  |  ",
            @"--+--",
            @"  |  ",
        };

        public HUD(int screenW, int screenH)
        {
            _screenW = screenW;
            _screenH = screenH;

            try
            {
                _fontLarge = new Font("Consolas", 14f, FontStyle.Bold,   GraphicsUnit.Point);
                _fontSmall = new Font("Consolas",  9f, FontStyle.Regular, GraphicsUnit.Point);
                _fontMono  = new Font("Consolas",  8f, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                _fontLarge = new Font("Courier New", 14f, FontStyle.Bold,   GraphicsUnit.Point);
                _fontSmall = new Font("Courier New",  9f, FontStyle.Regular, GraphicsUnit.Point);
                _fontMono  = new Font("Courier New",  8f, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        // ─────────────────────────────────────────────────────────
        // DrawAll — Tüm HUD elemanlarını çiz
        //
        // bobOffset : Yürüyüş sarsıntısı (silah bob için)
        // health    : Oyuncu canı (0-100)
        // ammo      : Mermi sayısı
        // mapData   : Minimap için harita verisi (bool[,])
        // playerX/Y : Oyuncunun haritadaki konumu
        // playerAng : Oyuncunun bakış açısı
        // ─────────────────────────────────────────────────────────
        public void DrawAll(
            Graphics g,
            float bobOffset,
            int health,
            int ammo,
            bool[,] mapData,
            float playerX,
            float playerY,
            float playerAngle)
        {
            DrawWeapon(g, bobOffset);
            DrawCrosshair(g);
            DrawStats(g, health, ammo);
            DrawMinimap(g, mapData, playerX, playerY, playerAngle);
        }

        // ─────────────────────────────────────────────────────────
        // Silah çizimi — Alt orta, bobOffset ile dikey kayma
        // ─────────────────────────────────────────────────────────
        private void DrawWeapon(Graphics g, float bobOffset)
        {
            // Silah boyutu (piksel)
            int charW = 10;
            int charH = 18;

            // Silahın başlangıç X konumu: Ekranın sağ orta-altı
            int startX = _screenW / 2 + 60;

            // Bob etkisi: bobOffset satır kadar kaydır
            int baseY  = _screenH - WEAPON_LINES.Length * charH - 10;
            int startY = baseY + (int)(bobOffset * 4f);

            // Hafif yarı saydam arka plan
            using var bgBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            int bgW = WEAPON_LINES.Max(l => l.Length) * charW + 10;
            int bgH = WEAPON_LINES.Length * charH + 6;
            g.FillRectangle(bgBrush, startX - 5, startY - 3, bgW, bgH);

            // Silah çizimi
            Color weaponColor = Color.FromArgb(200, 180, 160);
            for (int i = 0; i < WEAPON_LINES.Length; i++)
            {
                TextRenderer.DrawText(g, WEAPON_LINES[i], _fontLarge,
                    new Point(startX, startY + i * charH),
                    weaponColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }

        // ─────────────────────────────────────────────────────────
        // Nişangah — Ekranın tam ortasında
        // ─────────────────────────────────────────────────────────
        private void DrawCrosshair(Graphics g)
        {
            int cw  = 8, ch = 14;
            int cx  = _screenW / 2 - (CROSSHAIR[0].Length * cw) / 2;
            int cy  = _screenH / 2 - (CROSSHAIR.Length * ch) / 2;

            // Nişangah rengi: Parlak yeşil, hafif transparan
            Color crossColor = Color.FromArgb(220, 0, 255, 80);

            for (int i = 0; i < CROSSHAIR.Length; i++)
            {
                TextRenderer.DrawText(g, CROSSHAIR[i], _fontMono,
                    new Point(cx, cy + i * ch),
                    crossColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        }

        // ─────────────────────────────────────────────────────────
        // Can + Mermi göstergesi — Sol alt köşe
        // ─────────────────────────────────────────────────────────
        private void DrawStats(Graphics g, int health, int ammo)
        {
            int x = 20;
            int y = _screenH - 70;

            // Arka plan şeridi
            using var bg = new SolidBrush(Color.FromArgb(120, 0, 0, 0));
            g.FillRectangle(bg, x - 5, y - 5, 260, 65);

            // CAN göstergesi
            // Renk: Yüksek can = yeşil, orta = sarı, düşük = kırmızı
            Color hpColor = health > 60
                ? Color.FromArgb(80, 220, 80)
                : health > 30
                    ? Color.FromArgb(220, 200, 60)
                    : Color.FromArgb(220, 60, 60);

            // Can bar: █ karakterleriyle doldurulan çubuk
            int   barLen    = 20;
            int   filled    = (int)(health / 100f * barLen);
            string hpBar   = new string('█', filled) + new string('░', barLen - filled);

            TextRenderer.DrawText(g, $"♥ {health:D3}%", _fontSmall,
                new Point(x, y), hpColor,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, $"[{hpBar}]", _fontMono,
                new Point(x, y + 22), hpColor,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

            // Mermi göstergesi
            Color ammoColor = ammo > 10
                ? Color.FromArgb(200, 200, 120)
                : Color.FromArgb(220, 100, 60);

            TextRenderer.DrawText(g, $"◆ {ammo:D3}", _fontSmall,
                new Point(x + 140, y), ammoColor,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        // ─────────────────────────────────────────────────────────
        // Minimap — Sağ üst köşe
        // Her hücre MAP_CELL piksel büyüklüğünde
        // ─────────────────────────────────────────────────────────
        private void DrawMinimap(
            Graphics g,
            bool[,] mapData,
            float playerX,
            float playerY,
            float playerAngle)
        {
            const int MAP_CELL   = 6;    // Her harita hücresi kaç piksel
            const int MAP_MARGIN = 15;   // Köşeden mesafe

            int mapW = mapData.GetLength(0);
            int mapH = mapData.GetLength(1);

            int mmW = mapW * MAP_CELL;
            int mmH = mapH * MAP_CELL;

            // Sağ üst köşeye yerleştir
            int ox = _screenW - mmW - MAP_MARGIN;
            int oy = MAP_MARGIN;

            // Arka plan
            using var bgBrush = new SolidBrush(Color.FromArgb(160, 10, 10, 10));
            g.FillRectangle(bgBrush, ox - 2, oy - 2, mmW + 4, mmH + 4);

            // Harita hücreleri
            using var wallBrush  = new SolidBrush(Color.FromArgb(200, 140, 100, 70));
            using var floorBrush = new SolidBrush(Color.FromArgb(200, 35, 35, 35));

            for (int x = 0; x < mapW; x++)
            {
                for (int y = 0; y < mapH; y++)
                {
                    var brush = mapData[x, y] ? wallBrush : floorBrush;
                    g.FillRectangle(brush,
                        ox + x * MAP_CELL,
                        oy + y * MAP_CELL,
                        MAP_CELL - 1,
                        MAP_CELL - 1);
                }
            }

            // Oyuncu noktası — parlak sarı üçgen (bakış yönüne göre)
            int ppx = ox + (int)(playerX * MAP_CELL);
            int ppy = oy + (int)(playerY * MAP_CELL);

            // Bakış yönü çizgisi
            float lookLen = MAP_CELL * 2.5f;
            int   lx2     = ppx + (int)(MathF.Sin(playerAngle) * lookLen);
            int   ly2     = ppy + (int)(MathF.Cos(playerAngle) * lookLen);

            using var dirPen = new Pen(Color.FromArgb(255, 255, 220, 0), 1.5f);
            g.DrawLine(dirPen, ppx, ppy, lx2, ly2);

            // Oyuncu noktası
            using var playerBrush = new SolidBrush(Color.FromArgb(255, 255, 220, 0));
            g.FillEllipse(playerBrush, ppx - 3, ppy - 3, 6, 6);

            // Çerçeve
            using var borderPen = new Pen(Color.FromArgb(180, 100, 80, 60), 1f);
            g.DrawRectangle(borderPen, ox - 2, oy - 2, mmW + 3, mmH + 3);
        }
    }
}
