// ============================================================
// Renderer.cs — Yoğun dokulu ASCII Raycasting render motoru
//
// DOKU MANTIĞI:
//   Duvarın hangi noktasına çarptığımızı (wallU) hesaplıyoruz.
//   wallU 0.0→1.0 arası yüzey koordinatı → farklı karakterler seçilir.
//   Böylece duvar yüzeyi boyunca taş/panel deseni oluşur.
//
// SAĞ KENAR DÜZELTMESİ:
//   Son sütun (col = COLS-1) özel olarak işleniyor.
//   DrawBuffer'da segment uzunluğu pencere dışına taşmıyor.
// ============================================================

using System.Drawing;
using System.Windows.Forms;
using G_1_A3D_f.World;

namespace G_1_A3D_f.Engine
{
    public class Renderer
    {
        private readonly int _cols;
        private readonly int _rows;
        private readonly int _cw;    // Hücre genişliği (piksel)
        private readonly int _ch;    // Hücre yüksekliği (piksel)

        private const float FOV       = MathF.PI / 3.0f;
        private const float MAX_DEPTH = 22.0f;
        private const float STEP      = 0.012f;   // Küçük adım = daha keskin doku kenarları

        private readonly char[]  _chars;
        private readonly Color[] _colors;
        private readonly Font    _font;

        // ── Doku karakter setleri ──
        // Her set bir "malzeme" temsil eder; mesafe arttıkça seyrekleşir
        // NS = Kuzey/Güney yüzü (sıcak turuncu ton)
        // EW = Doğu/Batı yüzü (soğuk mavi ton)

        private static readonly char[] NS_CHARS = { '█','▉','▊','▋','▌','▍','▎','▏','│','╎','╏','┊','┋','╵','╷' };
        private static readonly char[] EW_CHARS = { '█','▓','▒','░','▪','▫','·','∙','∶','∷','⁚','⁝','⁞','‥','…' };

        // ─────────────────────────────────────────────────────────
        // Doku seçimi
        // wallU  : Duvar yüzeyindeki yatay konum (0.0 - 1.0 tekrarlı)
        // dist   : Mesafe (karakter yoğunluğunu belirler)
        // isNS   : Kuzey/Güney mi, Doğu/Batı mi?
        // ─────────────────────────────────────────────────────────
        private static (char ch, Color color) GetWallPixel(float wallU, float dist, bool isNS)
        {
            // Kesirli kısım: 0.0-1.0 arasında yüzey koordinatı
            float u = wallU - MathF.Floor(wallU);

            // Yoğunluk indeksi: Mesafe arttıkça daha seyrek karakter
            // MAX_DEPTH'e doğru 0→14 arası indeks
            int density = (int)Math.Clamp((dist / MAX_DEPTH) * 14f, 0, 14);

            // Kenar tespiti: Duvar bloğunun sol/sağ kenarına yakınsa çizgi çiz
            bool isEdge = u < 0.035f || u > 0.965f;

            // Dikey bölüntü: Her 0.5 birimde "derz" çizgisi → taş blok hissi
            // u < 0.03 veya u > 0.47 ve u < 0.53 arası dar bant
            bool isGrout = (u > 0.47f && u < 0.53f);

            char ch;
            if (isEdge || isGrout)
            {
                // Kenar/derz: Her zaman düşey çizgi karakteri
                ch = isNS ? '│' : '┃';
                // Kenar rengi biraz daha koyu
                dist *= 1.4f;
            }
            else
            {
                char[] set = isNS ? NS_CHARS : EW_CHARS;
                ch = set[density];
            }

            // ── Renk ──
            // Mesafe faktörü: 0 = çok yakın (parlak), 1 = çok uzak (karanlık)
            float t = Math.Clamp(dist / MAX_DEPTH, 0f, 1f);
            float bright = 1f - t * 0.88f;   // 1.0 → 0.12 arası

            Color color;
            if (isNS)
            {
                // NS: Turuncu/altın tonları
                color = Color.FromArgb(
                    (int)Math.Clamp(240 * bright, 0, 255),
                    (int)Math.Clamp(155 * bright, 0, 255),
                    (int)Math.Clamp( 55 * bright, 0, 255));
            }
            else
            {
                // EW: Mavi/çelik tonları
                color = Color.FromArgb(
                    (int)Math.Clamp( 70 * bright, 0, 255),
                    (int)Math.Clamp(140 * bright, 0, 255),
                    (int)Math.Clamp(220 * bright, 0, 255));
            }

            return (ch, color);
        }

        // ── Tavan ──
        private static Color GetCeilingColor(int row, int rows, int pitchOffset)
        {
            // Ufuk çizgisine yakın = az mavi, tepede = siyah
            float t = Math.Clamp(1f - (float)row / (rows / 2f + pitchOffset + 1f), 0f, 1f);
            return Color.FromArgb((int)(8*t), (int)(8*t), (int)(28*t));
        }

        // ── Zemin ──
        // b: 0 = uzak, 1 = yakın
        private static (char ch, Color color) GetFloorPixel(float b)
        {
            // Zemin dokusu: Yakındaki yüzey daha ayrıntılı
            char ch;
            if      (b < 0.12f) ch = ' ';
            else if (b < 0.25f) ch = '·';
            else if (b < 0.40f) ch = '∙';
            else if (b < 0.55f) ch = ',';
            else if (b < 0.70f) ch = 'o';
            else if (b < 0.85f) ch = 'O';
            else                ch = '#';

            // Zemin rengi: Koyu yeşil → parlak yeşil
            int r = (int)Math.Clamp(10  + 30  * b, 0, 255);
            int g = (int)Math.Clamp(45  + 130 * b, 0, 255);
            int bv = (int)Math.Clamp(8   + 20  * b, 0, 255);
            return (ch, Color.FromArgb(r, g, bv));
        }

        // ─────────────────────────────────────────────────────────
        // Kurucu
        // ─────────────────────────────────────────────────────────
        public Renderer(int cols, int rows, int cellW, int cellH)
        {
            _cols = cols; _rows = rows; _cw = cellW; _ch = cellH;
            _chars  = new char [cols * rows];
            _colors = new Color[cols * rows];

            try   { _font = new Font("Consolas",   9f, FontStyle.Regular, GraphicsUnit.Point); }
            catch { _font = new Font("Courier New", 9f, FontStyle.Regular, GraphicsUnit.Point); }
        }

        // ─────────────────────────────────────────────────────────
        // Render
        // ─────────────────────────────────────────────────────────
        public void Render(Graphics g, float px, float py, float angle, float pitch, Map map)
        {
            int pitchOff = (int)pitch;

            for (int col = 0; col < _cols; col++)
            {
                // Işın açısı
                float rayAngle = (angle - FOV * 0.5f) + ((float)col / (_cols - 1)) * FOV;
                float ex = MathF.Sin(rayAngle);
                float ey = MathF.Cos(rayAngle);

                float dist    = 0f;
                bool  hit     = false;
                bool  isNS    = false;
                float wallU   = 0f;

                while (!hit && dist < MAX_DEPTH)
                {
                    dist += STEP;
                    int mx = (int)(px + ex * dist);
                    int my = (int)(py + ey * dist);

                    if (mx < 0 || mx >= map.Width || my < 0 || my >= map.Height)
                    {
                        hit = true; dist = MAX_DEPTH;
                    }
                    else if (map.IsWall(mx, my))
                    {
                        hit = true;
                        float hx = px + ex * dist;
                        float hy = py + ey * dist;
                        // Hangi yüzey? Açının bileşenlerine göre NS veya EW
                        if (MathF.Abs(ey) > MathF.Abs(ex))
                        { isNS = true;  wallU = hx; }
                        else
                        { isNS = false; wallU = hy; }
                    }
                }

                // Duvar yüksekliği (pitch ofseti ile ufuk kayar)
                int wallH   = dist > 0f ? (int)(_rows / dist) : _rows;
                int ceiling = Math.Max(0,         _rows / 2 - wallH / 2 - pitchOff);
                int floor   = Math.Min(_rows - 1, _rows / 2 + wallH / 2 - pitchOff);

                for (int row = 0; row < _rows; row++)
                {
                    int idx = row * _cols + col;

                    if (row < ceiling)
                    {
                        _chars[idx]  = ' ';
                        _colors[idx] = GetCeilingColor(row, _rows, pitchOff);
                    }
                    else if (row <= floor)
                    {
                        var (ch, color) = GetWallPixel(wallU, dist, isNS);
                        _chars[idx]  = ch;
                        _colors[idx] = color;
                    }
                    else
                    {
                        // Zemin uzaklık faktörü
                        float floorRow = row - (_rows / 2f - pitchOff);
                        float b = Math.Clamp(1f - floorRow / (_rows / 2f), 0f, 1f);
                        var (ch, color) = GetFloorPixel(b);
                        _chars[idx]  = ch;
                        _colors[idx] = color;
                    }
                }
            }

            DrawBuffer(g);
        }

        // ─────────────────────────────────────────────────────────
        // DrawBuffer — Tamponu ekrana yaz
        //
        // SAĞ KENAR DÜZELTMESİ:
        //   Her satırda son segment COLS'a tam eşit olacak şekilde
        //   kırpılır. TextRenderer satır başından itibaren tam
        //   hücre sayısı kadar karakter yazar, fazlasını çizmez.
        // ─────────────────────────────────────────────────────────
        private void DrawBuffer(Graphics g)
        {
            g.Clear(Color.Black);

            for (int row = 0; row < _rows; row++)
            {
                int   base0    = row * _cols;
                int   segStart = 0;
                Color segColor = _colors[base0];

                for (int col = 1; col <= _cols; col++)
                {
                    bool  end   = (col == _cols);
                    Color next  = end ? Color.Empty : _colors[base0 + col];

                    if (end || next != segColor)
                    {
                        int    len = col - segStart;
                        string seg = new string(_chars, base0 + segStart, len);

                        // X konumunu tam piksel olarak hesapla
                        // segStart * _cw: Sütun indeksi × hücre genişliği
                        TextRenderer.DrawText(
                            g, seg, _font,
                            new Point(segStart * _cw, row * _ch),
                            segColor,
                            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

                        segStart = col;
                        segColor = next;
                    }
                }
            }
        }
    }
}