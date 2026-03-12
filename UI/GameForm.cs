// ============================================================
// GameForm.cs — HUD entegreli ana pencere
// ============================================================

using System.Windows.Forms;
using System.Drawing;
using G_1_A3D_f.Engine;
using G_1_A3D_f.World;

namespace G_1_A3D_f.UI
{
    public class GameForm : Form
    {
        private const int COLS   = 320;
        private const int ROWS   = 90;
        private const int CELL_W = 6;
        private const int CELL_H = 12;

        private const float MOVE_SPEED   = 6.0f;
        private const float ACCELERATION = 24.0f;
        private const float FRICTION     = 16.0f;
        private const float SENS_X       = 0.0012f;
        private const float SENS_Y       = 0.20f;
        private const float YAW_SMOOTH   = 22.0f;
        private const float PITCH_SMOOTH = 18.0f;
        private const float PITCH_MAX    =  30.0f;
        private const float PITCH_MIN    = -30.0f;
        private const float BOB_FREQ     =  9.0f;
        private const float BOB_AMP      =  1.5f;

        private readonly Renderer _renderer;
        private readonly HUD      _hud;
        private readonly Map      _map;
        private          bool[,]  _mapWalls;   // Minimap için duvar verisi

        private float _px = 12.0f, _py = 12.0f;
        private float _vx = 0f,    _vy = 0f;
        private float _yaw = 0f,   _pitch = 0f;
        private float _targetYaw = 0f, _targetPitch = 0f;
        private float _bobPhase = 0f,  _bobOffset = 0f;

        // Oyuncu istatistikleri (şimdilik sabit, ileride oyun sistemiyle bağlanacak)
        private int _health = 100;
        private int _ammo   = 30;

        private readonly System.Windows.Forms.Timer _gameTimer;
        private DateTime _lastTick;
        private bool _skipMouse = false;
        private int  _mdx = 0, _mdy = 0;
        private readonly Dictionary<Keys, bool> _keys = new();

        public GameForm()
        {
            Text            = "G_1_A3D_f — ASCII 3D Shooter";
            BackColor       = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            DoubleBuffered  = true;
            ClientSize      = new Size(COLS * CELL_W, ROWS * CELL_H);
            StartPosition   = FormStartPosition.CenterScreen;

            _renderer = new Renderer(COLS, ROWS, CELL_W, CELL_H);
            _hud      = new HUD(COLS * CELL_W, ROWS * CELL_H);
            _map      = BuildTestMap(out _mapWalls);

            Cursor.Hide();
            CenterMouse();

            _gameTimer          = new System.Windows.Forms.Timer();
            _gameTimer.Interval = 4;
            _gameTimer.Tick    += Tick;
            _gameTimer.Start();
            _lastTick = DateTime.Now;
        }

        private void Tick(object? s, EventArgs e)
        {
            var   now = DateTime.Now;
            float dt  = Math.Clamp((float)(now - _lastTick).TotalSeconds, 0f, 0.05f);
            _lastTick = now;

            // Fare → hedef açılar
            if (_mdx != 0)
            {
                _targetYaw += _mdx * SENS_X;
                _targetYaw %= MathF.PI * 2f;
                if (_targetYaw < 0) _targetYaw += MathF.PI * 2f;
                _mdx = 0;
            }
            if (_mdy != 0)
            {
                _targetPitch -= _mdy * SENS_Y;
                _targetPitch  = Math.Clamp(_targetPitch, PITCH_MIN, PITCH_MAX);
                _mdy = 0;
            }

            // Kamera Lerp
            float yf = Math.Clamp(YAW_SMOOTH   * dt, 0f, 1f);
            float pf = Math.Clamp(PITCH_SMOOTH  * dt, 0f, 1f);
            float yd = _targetYaw - _yaw;
            if (yd >  MathF.PI) yd -= MathF.PI * 2f;
            if (yd < -MathF.PI) yd += MathF.PI * 2f;
            _yaw += yd * yf;
            _yaw %= MathF.PI * 2f;
            if (_yaw < 0) _yaw += MathF.PI * 2f;
            _pitch += (_targetPitch - _pitch) * pf;

            // Hareket
            float dx = MathF.Sin(_yaw), dy = MathF.Cos(_yaw);
            float sx = dy, sy = -dx;
            float wx = 0f, wy = 0f;
            if (IsDown(Keys.W)) { wx += dx; wy += dy; }
            if (IsDown(Keys.S)) { wx -= dx; wy -= dy; }
            if (IsDown(Keys.A)) { wx -= sx; wy -= sy; }
            if (IsDown(Keys.D)) { wx += sx; wy += sy; }

            float wlen = MathF.Sqrt(wx * wx + wy * wy);
            if (wlen > 0.001f) { wx /= wlen; wy /= wlen; }

            _vx += (wx * MOVE_SPEED - _vx) * Math.Clamp(ACCELERATION * dt, 0f, 1f);
            _vy += (wy * MOVE_SPEED - _vy) * Math.Clamp(ACCELERATION * dt, 0f, 1f);

            if (wlen < 0.001f)
            {
                float fr = Math.Clamp(FRICTION * dt, 0f, 1f);
                _vx *= (1f - fr); _vy *= (1f - fr);
                if (MathF.Abs(_vx) < 0.001f) _vx = 0f;
                if (MathF.Abs(_vy) < 0.001f) _vy = 0f;
            }

            if (_map.IsWalkable((int)(_px + _vx * dt), (int)_py)) _px += _vx * dt;
            if (_map.IsWalkable((int)_px, (int)(_py + _vy * dt))) _py += _vy * dt;

            // Bob
            if (wlen > 0.001f)
            {
                _bobPhase += BOB_FREQ * dt;
                if (_bobPhase > MathF.PI * 2f) _bobPhase -= MathF.PI * 2f;
                _bobOffset = MathF.Sin(_bobPhase) * BOB_AMP;
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
            // Önce 3D dünya
            _renderer.Render(e.Graphics, _px, _py, _yaw, _pitch + _bobOffset, _map);

            // Sonra HUD (üstüne)
            _hud.DrawAll(e.Graphics, _bobOffset, _health, _ammo, _mapWalls, _px, _py, _yaw);
        }

        protected override void OnKeyDown(KeyEventArgs e) => _keys[e.KeyCode] = true;
        protected override void OnKeyUp(KeyEventArgs e)   => _keys[e.KeyCode] = false;
        private bool IsDown(Keys k) => _keys.TryGetValue(k, out bool v) && v;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_skipMouse) { _skipMouse = false; return; }
            int cx = ClientSize.Width / 2, cy = ClientSize.Height / 2;
            _mdx += e.X - cx; _mdy += e.Y - cy;
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
            _gameTimer.Stop(); Cursor.Show(); base.OnFormClosed(e);
        }

        // Test haritası — bool[,] minimap için de döner
        private static Map BuildTestMap(out bool[,] walls)
        {
            int w = 24, h = 24;
            var map  = new Map(w, h);
            // out parametresi local function içinde kullanılamaz → ayrı local array
            var localWalls = new bool[w, h];

            void SetWall(int x, int y)
            {
                map.SetTile(x, y, TileType.Wall);
                localWalls[x, y] = true;
            }

            for (int i = 0; i < w; i++)
            { SetWall(i, 0); SetWall(i, h-1); SetWall(0, i); SetWall(w-1, i); }

            for (int i = 3; i <= 7;  i++) SetWall(i,  3);
            for (int i = 5; i <= 10; i++) SetWall(10, i);
            for (int i = 14; i <= 18; i++) SetWall(i,  8);
            for (int i = 8;  i <= 13; i++) SetWall(14, i);
            for (int i = 8;  i <= 13; i++) SetWall(18, i);
            SetWall(5, 14); SetWall(8, 17); SetWall(20, 5); SetWall(20, 6);

            walls = localWalls;
            return map;
        }
    }
}