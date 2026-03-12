// ============================================================
// GameForm.cs — Ana oyun penceresi
//
// YENİ ÖZELLİKLER:
//   - Smooth hareket: Hız vektörü + ivme + sürtünme (eylemsizlik hissi)
//   - Smooth kamera: Hedef açıya yumuşak interpolasyon (Lerp)
//   - Sağ kenar düzeltmesi: Pencere ClientSize tam hücre katı olarak ayarlandı
// ============================================================

using System.Windows.Forms;
using System.Drawing;
using G_1_A3D_f.Engine;
using G_1_A3D_f.World;

namespace G_1_A3D_f.UI
{
    public class GameForm : Form
    {
        // ── Izgara boyutları ──
        private const int COLS   = 150;   // Yatay karakter sayısı
        private const int ROWS   = 42;    // Dikey karakter sayısı
        private const int CELL_W = 7;     // Bir karakterin piksel genişliği
        private const int CELL_H = 13;    // Bir karakterin piksel yüksekliği

        // ── Hareket parametreleri ──
        private const float MOVE_SPEED   = 6.0f;   // Maksimum hız (birim/saniye)
        private const float ACCELERATION = 24.0f;  // İvme katsayısı — hızlanma sertliği
        private const float FRICTION     = 16.0f;  // Sürtünme — duruş sertliği

        // ── Fare hassasiyeti ──
        private const float SENS_X = 0.0012f;  // Yatay dönüş
        private const float SENS_Y = 0.15f;    // Dikey bakış

        // ── Kamera yumuşatma (Lerp katsayısı) ──
        // Yüksek değer = daha ani, düşük değer = daha süzülen
        private const float YAW_SMOOTH   = 22.0f;
        private const float PITCH_SMOOTH = 18.0f;

        // ── Pitch sınırları (satır cinsinden) ──
        private const float PITCH_MAX =  14.0f;
        private const float PITCH_MIN = -14.0f;

        // ── Yürüyüş sarsıntısı ──
        private const float BOB_FREQ  = 9.0f;   // Frekans (Hz)
        private const float BOB_AMP   = 1.5f;   // Genlik (satır)

        // ── Oyun nesneleri ──
        private readonly Renderer _renderer;
        private readonly Map      _map;

        // ── Oyuncu konumu ──
        private float _px = 12.0f;   // Haritada X
        private float _py = 12.0f;   // Haritada Y

        // ── Hız vektörü (smooth hareket için) ──
        private float _vx = 0f;
        private float _vy = 0f;

        // ── Kamera anlık değerleri (render bu değerleri kullanır) ──
        private float _yaw   = 0f;   // Yatay bakış açısı (radyan)
        private float _pitch = 0f;   // Dikey bakış ofseti (satır)

        // ── Kamera hedef değerleri (fare inputu bunları günceller) ──
        private float _targetYaw   = 0f;
        private float _targetPitch = 0f;

        // ── Bob ──
        private float _bobPhase  = 0f;
        private float _bobOffset = 0f;

        // ── Zamanlama ──
        private readonly System.Windows.Forms.Timer _gameTimer;
        private DateTime _lastTick;

        // ── Fare girişi ──
        private bool _skipMouse = false;
        private int  _mdx = 0;   // Biriken yatay fare deltası
        private int  _mdy = 0;   // Biriken dikey fare deltası

        // ── Klavye ──
        private readonly Dictionary<Keys, bool> _keys = new();

        // ─────────────────────────────────────────────────────────
        public GameForm()
        {
            Text            = "G_1_A3D_f — ASCII 3D Shooter";
            BackColor       = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            DoubleBuffered  = true;

            // Pencere boyutu: tam hücre sayısı × hücre boyutu
            // Böylece sağ/alt kenarda yarım hücre kalmaz → bozukluk önlenir
            ClientSize    = new Size(COLS * CELL_W, ROWS * CELL_H);
            StartPosition = FormStartPosition.CenterScreen;

            _renderer = new Renderer(COLS, ROWS, CELL_W, CELL_H);
            _map      = BuildTestMap();

            Cursor.Hide();
            CenterMouse();

            _gameTimer          = new System.Windows.Forms.Timer();
            _gameTimer.Interval = 4;   // ~250 FPS hedef
            _gameTimer.Tick    += Tick;
            _gameTimer.Start();

            _lastTick = DateTime.Now;
        }

        // ─────────────────────────────────────────────────────────
        // Tick — Ana oyun döngüsü (4ms'de bir)
        // ─────────────────────────────────────────────────────────
        private void Tick(object? s, EventArgs e)
        {
            var   now = DateTime.Now;
            float dt  = Math.Clamp((float)(now - _lastTick).TotalSeconds, 0f, 0.05f);
            _lastTick = now;

            // ── 1. Fare → Hedef kamera açıları ──
            if (_mdx != 0)
            {
                _targetYaw += _mdx * SENS_X;
                _targetYaw %= MathF.PI * 2f;
                if (_targetYaw < 0) _targetYaw += MathF.PI * 2f;
                _mdx = 0;
            }
            if (_mdy != 0)
            {
                _targetPitch += _mdy * SENS_Y;
                _targetPitch  = Math.Clamp(_targetPitch, PITCH_MIN, PITCH_MAX);
                _mdy = 0;
            }

            // ── 2. Kamera Lerp (smooth) ──
            // Lerp: A + (B - A) * t → A'dan B'ye doğru t hızında yaklaş
            // dt * katsayı: Kare hızından bağımsız yumuşatma
            float yawFactor   = Math.Clamp(YAW_SMOOTH   * dt, 0f, 1f);
            float pitchFactor = Math.Clamp(PITCH_SMOOTH  * dt, 0f, 1f);

            // Yaw için kısa yol hesabı: 0→2π sınırında dönüş açısı doğru taraftan gitsin
            float yawDiff = _targetYaw - _yaw;
            if (yawDiff >  MathF.PI) yawDiff -= MathF.PI * 2f;
            if (yawDiff < -MathF.PI) yawDiff += MathF.PI * 2f;
            _yaw += yawDiff * yawFactor;
            _yaw %= MathF.PI * 2f;
            if (_yaw < 0) _yaw += MathF.PI * 2f;

            _pitch += (_targetPitch - _pitch) * pitchFactor;

            // ── 3. Hareket yön vektörü ──
            float dirX    =  MathF.Sin(_yaw);
            float dirY    =  MathF.Cos(_yaw);
            float strafeX =  dirY;
            float strafeY = -dirX;

            // Girişten hedef hız vektörü oluştur
            float wishX = 0f, wishY = 0f;
            if (IsDown(Keys.W)) { wishX += dirX;    wishY += dirY; }
            if (IsDown(Keys.S)) { wishX -= dirX;    wishY -= dirY; }
            if (IsDown(Keys.A)) { wishX -= strafeX; wishY -= strafeY; }
            if (IsDown(Keys.D)) { wishX += strafeX; wishY += strafeY; }

            // Normalize: Çapraz harekette hız artmasın
            float wishLen = MathF.Sqrt(wishX * wishX + wishY * wishY);
            if (wishLen > 0.001f) { wishX /= wishLen; wishY /= wishLen; }

            // ── 4. Hız vektörüne ivme + sürtünme uygula ──
            // İvme: Hedefe doğru yaklaş
            _vx += (wishX * MOVE_SPEED - _vx) * Math.Clamp(ACCELERATION * dt, 0f, 1f);
            _vy += (wishY * MOVE_SPEED - _vy) * Math.Clamp(ACCELERATION * dt, 0f, 1f);

            // Sürtünme: Tuşa basılı değilse yavaşla
            if (wishLen < 0.001f)
            {
                float friction = Math.Clamp(FRICTION * dt, 0f, 1f);
                _vx *= (1f - friction);
                _vy *= (1f - friction);
                // Çok küçükse sıfırla — titreme önle
                if (MathF.Abs(_vx) < 0.001f) _vx = 0f;
                if (MathF.Abs(_vy) < 0.001f) _vy = 0f;
            }

            // ── 5. Çarpışma kontrolü ile pozisyon güncelle ──
            if (_map.IsWalkable((int)(_px + _vx * dt), (int)_py)) _px += _vx * dt;
            if (_map.IsWalkable((int)_px, (int)(_py + _vy * dt))) _py += _vy * dt;

            // ── 6. Yürüyüş sarsıntısı ──
            bool moving = wishLen > 0.001f;
            if (moving)
            {
                _bobPhase  += BOB_FREQ * dt;
                if (_bobPhase > MathF.PI * 2f) _bobPhase -= MathF.PI * 2f;
                _bobOffset  = MathF.Sin(_bobPhase) * BOB_AMP;
            }
            else
            {
                _bobOffset *= (1f - Math.Clamp(12f * dt, 0f, 1f));
                if (MathF.Abs(_bobOffset) < 0.01f) { _bobOffset = 0f; _bobPhase = 0f; }
            }

            if (IsDown(Keys.Escape)) Close();

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            _renderer.Render(e.Graphics, _px, _py, _yaw, _pitch + _bobOffset, _map);
        }

        protected override void OnKeyDown(KeyEventArgs e) => _keys[e.KeyCode] = true;
        protected override void OnKeyUp(KeyEventArgs e)   => _keys[e.KeyCode] = false;
        private bool IsDown(Keys k) => _keys.TryGetValue(k, out bool v) && v;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_skipMouse) { _skipMouse = false; return; }
            int cx = ClientSize.Width  / 2;
            int cy = ClientSize.Height / 2;
            _mdx += e.X - cx;
            _mdy += e.Y - cy;
            _skipMouse = true;
            Cursor.Position = PointToScreen(new Point(cx, cy));
        }

        private void CenterMouse()
        {
            _skipMouse = true;
            Cursor.Position = PointToScreen(new Point(ClientSize.Width / 2, ClientSize.Height / 2));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _gameTimer.Stop();
            Cursor.Show();
            base.OnFormClosed(e);
        }

        // ── Test haritası ──
        private static Map BuildTestMap()
        {
            var map = new Map(24, 24);
            for (int i = 0; i < 24; i++)
            {
                map.SetTile(i, 0,  TileType.Wall);
                map.SetTile(i, 23, TileType.Wall);
                map.SetTile(0,  i, TileType.Wall);
                map.SetTile(23, i, TileType.Wall);
            }
            for (int i = 3; i <= 7;  i++) map.SetTile(i, 3,  TileType.Wall);
            for (int i = 5; i <= 10; i++) map.SetTile(10, i, TileType.Wall);
            for (int i = 14; i <= 18; i++) map.SetTile(i, 8,  TileType.Wall);
            for (int i = 8;  i <= 13; i++) map.SetTile(14, i, TileType.Wall);
            for (int i = 8;  i <= 13; i++) map.SetTile(18, i, TileType.Wall);
            map.SetTile(5,  14, TileType.Wall);
            map.SetTile(8,  17, TileType.Wall);
            map.SetTile(20,  5, TileType.Wall);
            map.SetTile(20,  6, TileType.Wall);
            return map;
        }
    }
}