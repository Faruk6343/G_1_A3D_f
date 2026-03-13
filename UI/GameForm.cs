// ============================================================
// GameForm.cs — RenderConfig + SettingsForm entegreli
// F2 tuşu: Render ayarları penceresini aç/kapat
// ============================================================

using System.Windows.Forms;
using System.Drawing;
using G_1_A3D_f.Engine;
using G_1_A3D_f.World;
using G_1_A3D_f.Audio;

namespace G_1_A3D_f.UI
{
    public class GameForm : Form
    {
        private readonly RenderConfig  _cfg;
        private          Renderer      _renderer;
        private readonly HUD           _hud;
        private readonly Map           _map;
        private readonly bool[,]       _mapWalls;
        private          SettingsForm? _settingsForm;
        private          bool           _paused = false;
        private          DungeonAudio?  _audio;

        private const float ACCELERATION = 24.0f;
        private const float FRICTION     = 16.0f;
        private const float YAW_SMOOTH   = 22.0f;
        private const float PITCH_SMOOTH = 18.0f;
        private const float BOB_FREQ     =  9.0f;
        private const float BOB_AMP      =  1.5f;

        private float _px = 12.0f, _py = 12.0f;
        private float _vx = 0f,    _vy = 0f;
        private float _yaw = 0f,   _pitch = 0f;
        private float _targetYaw = 0f, _targetPitch = 0f;
        private float _bobPhase = 0f,  _bobOffset = 0f;

        private int _health = 100;
        private int _ammo   = 30;

        private readonly System.Windows.Forms.Timer _gameTimer;
        private DateTime _lastTick;
        private bool _skipMouse = false;
        private int  _mdx = 0, _mdy = 0;
        private readonly Dictionary<Keys, bool> _keys = new();

        public GameForm()
        {
            _cfg = RenderConfig.Load();

            Text            = "G_1_A3D_f — ASCII 3D Shooter  [F2: Ayarlar]";
            BackColor       = Color.Black;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            DoubleBuffered  = true;
            AutoScaleMode   = AutoScaleMode.None;
            StartPosition   = FormStartPosition.CenterScreen;

            _renderer = new Renderer(_cfg.Cols, _cfg.Rows, _cfg.CellW, _cfg.CellH, _cfg.FontSize);
            ClientSize = new Size(1920, 1080);   // Her zaman sabit

            _hud  = new HUD();
            _map  = BuildTestMap(out _mapWalls);

            // Ses motoru — başarısız olursa sessiz devam et
            _audio = new DungeonAudio();
            _audio.Enabled = _cfg.AudioEnabled;
            _audio.WetMix  = _cfg.AudioWetMix;
            _audio.Gain    = _cfg.AudioGain;
            _audio.Start();

            Cursor.Hide();
            CenterMouse();

            _gameTimer          = new System.Windows.Forms.Timer();
            _gameTimer.Interval = 4;
            _gameTimer.Tick    += Tick;
            _gameTimer.Start();
            _lastTick = DateTime.Now;
        }

        private void ApplyResolution()
        {
            _renderer.Dispose();
            _renderer  = new Renderer(_cfg.Cols, _cfg.Rows, _cfg.CellW, _cfg.CellH, _cfg.FontSize);
            // ClientSize değişmez — pencere her zaman 1920×1080
            _cfg.NeedsRestart = false;
        }

        private void Tick(object? s, EventArgs e)
        {
            if (_cfg.NeedsRestart) ApplyResolution();

            if (_paused) return;   // Settings açıkken oyun durur

            var   now = DateTime.Now;
            float dt  = Math.Clamp((float)(now - _lastTick).TotalSeconds, 0f, 0.05f);
            _lastTick = now;

            if (_mdx != 0)
            {
                _targetYaw += _mdx * _cfg.Sensitivity * 0.006f;
                _targetYaw %= MathF.PI * 2f;
                if (_targetYaw < 0) _targetYaw += MathF.PI * 2f;
                _mdx = 0;
            }
            if (_mdy != 0)
            {
                _targetPitch -= _mdy * _cfg.Sensitivity;
                _targetPitch  = Math.Clamp(_targetPitch, -_cfg.PitchMax, _cfg.PitchMax);
                _mdy = 0;
            }

            float yf = Math.Clamp(YAW_SMOOTH   * dt, 0f, 1f);
            float pf = Math.Clamp(PITCH_SMOOTH  * dt, 0f, 1f);
            float yd = _targetYaw - _yaw;
            if (yd >  MathF.PI) yd -= MathF.PI * 2f;
            if (yd < -MathF.PI) yd += MathF.PI * 2f;
            _yaw += yd * yf;
            _yaw %= MathF.PI * 2f;
            if (_yaw < 0) _yaw += MathF.PI * 2f;
            _pitch += (_targetPitch - _pitch) * pf;

            float dx = MathF.Sin(_yaw), dy = MathF.Cos(_yaw);
            float sx = dy, sy = -dx;
            float wx = 0f, wy = 0f;
            if (IsDown(Keys.W)) { wx += dx; wy += dy; }
            if (IsDown(Keys.S)) { wx -= dx; wy -= dy; }
            if (IsDown(Keys.A)) { wx -= sx; wy -= sy; }
            if (IsDown(Keys.D)) { wx += sx; wy += sy; }

            float wlen = MathF.Sqrt(wx * wx + wy * wy);
            if (wlen > 0.001f) { wx /= wlen; wy /= wlen; }

            _vx += (wx * _cfg.MoveSpeed - _vx) * Math.Clamp(ACCELERATION * dt, 0f, 1f);
            _vy += (wy * _cfg.MoveSpeed - _vy) * Math.Clamp(ACCELERATION * dt, 0f, 1f);

            if (wlen < 0.001f)
            {
                float fr = Math.Clamp(FRICTION * dt, 0f, 1f);
                _vx *= (1f - fr); _vy *= (1f - fr);
                if (MathF.Abs(_vx) < 0.001f) _vx = 0f;
                if (MathF.Abs(_vy) < 0.001f) _vy = 0f;
            }

            if (_map.IsWalkable((int)(_px + _vx * dt), (int)_py)) _px += _vx * dt;
            if (_map.IsWalkable((int)_px, (int)(_py + _vy * dt))) _py += _vy * dt;

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

            // Ses parametreleri config'den senkronize et
            if (_audio != null)
            {
                _audio.Enabled = _cfg.AudioEnabled;
                _audio.WetMix  = _cfg.AudioWetMix;
                _audio.Gain    = _cfg.AudioGain;
                // Raycasting ile oda akustiğini güncelle
                _audio.UpdateAcoustics(_map, _px, _py, _yaw);
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            int sw = ClientSize.Width, sh = ClientSize.Height;
            _renderer.Render(e.Graphics, _px, _py, _yaw, _pitch + _bobOffset, _map, _cfg);
            _hud.DrawAll(e.Graphics, sw, sh, _bobOffset, _health, _ammo, _mapWalls, _px, _py, _yaw);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            _keys[e.KeyCode] = true;

            if (e.KeyCode == Keys.F2)
            {
                if (_settingsForm == null || _settingsForm.IsDisposed)
                {
                    _settingsForm = new SettingsForm(_cfg) { Owner = this };
                    _settingsForm.RestartRequested += ApplyResolution;
                    // SettingsClosed: Kullanıcı X butonuyla kapatınca tetiklenir
                    _settingsForm.SettingsClosed += CloseSettings;
                }
                if (_settingsForm.Visible) CloseSettings();
                else                       OpenSettings();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)   => _keys[e.KeyCode] = false;
        private bool IsDown(Keys k) => _keys.TryGetValue(k, out bool v) && v;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_paused) return;   // Ayarlar açıkken fareyi kilitleme
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

        private void OpenSettings()
        {
            if (_paused) return;            // Guard: Zaten açıksa tekrar açma
            _paused = true;
            _mdx = 0; _mdy = 0;            // Birikmiş fare hareketini sıfırla
            Cursor.Show();
            _settingsForm?.Show();
        }

        private void CloseSettings()
        {
            if (!_paused) return;           // Guard: Zaten kapalıysa tekrar kapatma
            _settingsForm?.Hide();          // F2 ile kapatıldıysa gizle (X'ten kapatıldıysa no-op)
            _paused = false;
            _mdx = 0; _mdy = 0;            // Birikmiş fare hareketini sıfırla
            _lastTick = DateTime.Now;       // dt sıfırla — duraklatma sonrası zıplama önlenir
            // Ses motoru — başarısız olursa sessiz devam et
            _audio = new DungeonAudio();
            _audio.Enabled = _cfg.AudioEnabled;
            _audio.WetMix  = _cfg.AudioWetMix;
            _audio.Gain    = _cfg.AudioGain;
            _audio.Start();

            Cursor.Hide();
            CenterMouse();
            _skipMouse = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _gameTimer.Stop();
            _audio?.Stop();
            _audio?.Dispose();
            _renderer.Dispose();
            _settingsForm?.Dispose();
            _cfg.Save();
            Cursor.Show();
            base.OnFormClosed(e);
        }

        private static Map BuildTestMap(out bool[,] walls)
        {
            int w = 24, h = 24;
            var map = new Map(w, h);
            var localWalls = new bool[w, h];
            void SetWall(int x, int y)
            { map.SetTile(x, y, TileType.Wall); localWalls[x, y] = true; }
            for (int i = 0; i < w; i++)
            { SetWall(i, 0); SetWall(i, h-1); SetWall(0, i); SetWall(w-1, i); }
            for (int i = 3;  i <= 7;  i++) SetWall(i,  3);
            for (int i = 5;  i <= 10; i++) SetWall(10, i);
            for (int i = 14; i <= 18; i++) SetWall(i,  8);
            for (int i = 8;  i <= 13; i++) SetWall(14, i);
            for (int i = 8;  i <= 13; i++) SetWall(18, i);
            SetWall(5, 14); SetWall(8, 17); SetWall(20, 5); SetWall(20, 6);
            walls = localWalls;
            return map;
        }
    }
}