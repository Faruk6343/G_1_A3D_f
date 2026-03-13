// ============================================================
// SettingsForm.cs — Görüntü Ayarları Penceresi
//
// Slider sorunu çözümü:
//   TrackBar'lar scroll panel'e değil FlowLayoutPanel'e
//   eklenir. Her satır ayrı bir Panel içinde, bu sayede
//   scroll kırpması olmaz ve arka plan doğru görünür.
// ============================================================

using System.Drawing;
using System.Windows.Forms;
using G_1_A3D_f.Engine;

namespace G_1_A3D_f.UI
{
    public class SettingsForm : Form
    {
        private readonly RenderConfig _cfg;
        private bool _loading = true;

        public event Action? RestartRequested;
        public event Action? SettingsClosed;  // Sadece X butonu ile kapatılınca tetiklenir

        private Panel _scrollContent = null!;

        public SettingsForm(RenderConfig cfg)
        {
            _cfg = cfg;

            Text            = "G_1_A3D_f — Render Ayarları  [F2]";
            Size            = new Size(500, 750);
            MinimumSize     = new Size(500, 400);
            BackColor       = Color.FromArgb(20, 20, 25);
            ForeColor       = Color.FromArgb(210, 200, 180);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition   = FormStartPosition.Manual;
            ShowInTaskbar   = false;
            TopMost         = true;
            Font            = new Font("Consolas", 9f, FontStyle.Regular);

            Location = new Point(
                Screen.PrimaryScreen!.WorkingArea.Right - Width - 10,
                Screen.PrimaryScreen!.WorkingArea.Top   + 40);

            BuildUI();
            _loading = false;
        }

        // ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            // Alt buton paneli
            var btnPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 44,
                BackColor = Color.FromArgb(12, 12, 16),
            };
            Controls.Add(btnPanel);

            var btnReset = MakeButton("↺ Varsayılan",          Color.FromArgb(80,  55, 25));
            var btnApply = MakeButton("⟳ Çözünürlüğü Uygula", Color.FromArgb(25,  65, 45));
            btnReset.Location = new Point(8, 7);
            btnApply.Location = new Point(btnReset.Right + 8, 7);
            btnReset.Click += (_, _) => { _cfg.ResetToDefaults(); _cfg.Save(); Rebuild(); };
            btnApply.Click += (_, _) => { _cfg.NeedsRestart = true; RestartRequested?.Invoke(); };
            btnPanel.Controls.AddRange(new Control[] { btnReset, btnApply });

            // Scroll panel
            var scroll = new Panel
            {
                Dock       = DockStyle.Fill,
                AutoScroll = true,
                BackColor  = Color.FromArgb(20, 20, 25),
            };
            Controls.Add(scroll);

            // İçerik paneli (scroll edilebilir alan)
            _scrollContent = new Panel
            {
                AutoSize    = true,
                AutoSizeMode= AutoSizeMode.GrowOnly,
                Width       = 470,
                BackColor   = Color.FromArgb(20, 20, 25),
            };
            scroll.Controls.Add(_scrollContent);

            PopulateRows();
        }

        private void Rebuild()
        {
            _loading = true;
            _scrollContent.Controls.Clear();
            PopulateRows();
            _loading = false;
        }

        // ─────────────────────────────────────────────────────────
        private void PopulateRows()
        {
            int y = 6;

            Sec("ÇÖZÜNÜRLÜK  (* = Uygula gerektirir)", ref y);
            Row("Sütun sayısı",     ref y, 160, 640, 1,   _cfg.Cols,              v => _cfg.Cols      = (int)v, restart: true);
            Row("Satır sayısı",     ref y,  60, 200, 1,   _cfg.Rows,              v => _cfg.Rows      = (int)v, restart: true);
            Row("Hücre gen.",       ref y,   2,  12, 1,   _cfg.CellW,             v => _cfg.CellW     = (int)v, restart: true);
            Row("Hücre yük.",       ref y,   2,  20, 1,   _cfg.CellH,             v => _cfg.CellH     = (int)v, restart: true);
            Row("Font boyutu ×10",  ref y,  20, 150, 1,   _cfg.FontSize * 10f,    v => _cfg.FontSize  = v / 10f, restart: true);

            Sec("KAMERA / IŞIN", ref y);
            Row("FOV (derece)",     ref y,  30, 120, 1,   _cfg.Fov,               v => _cfg.Fov       = v);
            Row("Max derinlik",     ref y,   5,  40, 1,   _cfg.MaxDepth,          v => _cfg.MaxDepth  = v);
            Row("Pitch limiti",     ref y,   5,  40, 1,   _cfg.PitchMax,          v => _cfg.PitchMax  = v);
            Row("Fare has. ×100",   ref y,   1, 100, 1,   _cfg.Sensitivity * 100f,v => _cfg.Sensitivity = v / 100f);
            Row("Hareket hızı ×10", ref y,  10, 150, 1,   _cfg.MoveSpeed * 10f,  v => _cfg.MoveSpeed = v / 10f);

            Sec("IŞIKLANDIRMA", ref y);
            Row("Işık düşüşü ×100", ref y,  1, 100, 1,   _cfg.LightFalloff * 100f, v => _cfg.LightFalloff = v / 100f);
            Row("Ambient ×100",     ref y,  0,  80, 1,   _cfg.Ambient * 100f,    v => _cfg.Ambient    = v / 100f);

            Sec("DUVAR", ref y);
            Row("R",                ref y,  0, 255, 1,   _cfg.WallBaseR,         v => _cfg.WallBaseR  = v);
            Row("G",                ref y,  0, 255, 1,   _cfg.WallBaseG,         v => _cfg.WallBaseG  = v);
            Row("B",                ref y,  0, 255, 1,   _cfg.WallBaseB,         v => _cfg.WallBaseB  = v);
            Row("NS çarpan ×100",   ref y, 40, 150, 1,   _cfg.WallNSMult * 100f, v => _cfg.WallNSMult = v / 100f);
            Row("EW çarpan ×100",   ref y, 40, 150, 1,   _cfg.WallEWMult * 100f, v => _cfg.WallEWMult = v / 100f);
            Row("Yoğunluk ×100",    ref y, 10, 100, 1,   _cfg.WallDensity * 100f,v => _cfg.WallDensity= v / 100f);

            Sec("TUĞLA DOKU", ref y);
            Row("Gen. ×100",        ref y, 20, 200, 1,   _cfg.BrickW * 100f,     v => _cfg.BrickW     = v / 100f);
            Row("Yük. ×100",        ref y, 10, 150, 1,   _cfg.BrickH * 100f,     v => _cfg.BrickH     = v / 100f);
            Row("Derz ×1000",       ref y,  1,  20, 1,   _cfg.GroutW * 1000f,    v => _cfg.GroutW     = v / 1000f);

            Sec("SES / AKUSTİK", ref y);
            // Checkbox: Ses aktif/pasif
            var chkAudio = new CheckBox
            {
                Text      = "Mikrofon Reverb Aktif",
                Checked   = _cfg.AudioEnabled,
                ForeColor = Color.FromArgb(185, 180, 165),
                Font      = new Font("Consolas", 8.5f, FontStyle.Regular),
                Bounds    = new Rectangle(8, y + 4, 250, 20),
                BackColor = Color.FromArgb(26, 26, 32),
            };
            chkAudio.CheckedChanged += (_, _) => _cfg.AudioEnabled = chkAudio.Checked;
            var chkPanel = new Panel { Bounds = new Rectangle(0, y, 470, 28), BackColor = Color.FromArgb(26, 26, 32) };
            chkPanel.Controls.Add(chkAudio);
            _scrollContent.Controls.Add(chkPanel);
            y += 30;
            Row("Reverb Miktarı ×100", ref y, 0, 100, 1, _cfg.AudioWetMix * 100f, v => _cfg.AudioWetMix = v / 100f);
            Row("Ses Kazancı ×100",    ref y, 0, 300, 1, _cfg.AudioGain * 100f,   v => _cfg.AudioGain   = v / 100f);

            Sec("ZEMİN / TAVAN DOKU", ref y);
            Row("Zemin tile ölç.",  ref y,  5, 200, 1,   _cfg.FloorTileScale*10f,v => _cfg.FloorTileScale = v/10f);
            Row("Tavan tile ölç.",  ref y,  5, 200, 1,   _cfg.CeilTileScale*10f, v => _cfg.CeilTileScale  = v/10f);
            Row("Zemin derz ×1000", ref y,  1,  80, 1,   _cfg.FloorGrout*1000f,  v => _cfg.FloorGrout    = v/1000f);
            Row("Tavan derz ×1000", ref y,  1,  80, 1,   _cfg.CeilGrout*1000f,   v => _cfg.CeilGrout     = v/1000f);

            Sec("TAVAN", ref y);
            Row("R",                ref y,  0, 255, 1,   _cfg.CeilBaseR,         v => _cfg.CeilBaseR  = v);
            Row("G",                ref y,  0, 255, 1,   _cfg.CeilBaseG,         v => _cfg.CeilBaseG  = v);
            Row("B",                ref y,  0, 255, 1,   _cfg.CeilBaseB,         v => _cfg.CeilBaseB  = v);
            Row("Parlaklık ×100",   ref y, 10, 100, 1,   _cfg.CeilBright * 100f, v => _cfg.CeilBright = v / 100f);
            Row("Kararma ×100",     ref y,  0,  80, 1,   _cfg.CeilFade * 100f,   v => _cfg.CeilFade   = v / 100f);
            Row("Yoğunluk ×100",    ref y, 10, 100, 1,   _cfg.CeilDensity * 100f,v => _cfg.CeilDensity= v / 100f);

            Sec("ZEMİN", ref y);
            Row("R",                ref y,  0, 255, 1,   _cfg.FloorBaseR,        v => _cfg.FloorBaseR = v);
            Row("G",                ref y,  0, 255, 1,   _cfg.FloorBaseG,        v => _cfg.FloorBaseG = v);
            Row("B",                ref y,  0, 255, 1,   _cfg.FloorBaseB,        v => _cfg.FloorBaseB = v);
            Row("Parlaklık ×100",   ref y, 10, 100, 1,   _cfg.FloorBright * 100f,v => _cfg.FloorBright= v / 100f);
            Row("Kararma ×100",     ref y,  0,  80, 1,   _cfg.FloorFade * 100f,  v => _cfg.FloorFade  = v / 100f);
            Row("Yoğunluk ×100",    ref y, 10, 100, 1,   _cfg.FloorDensity*100f, v => _cfg.FloorDensity=v / 100f);

            _scrollContent.Height = y + 10;
        }

        // ── Bölüm başlığı ─────────────────────────────────────
        private void Sec(string title, ref int y)
        {
            var lbl = new Label
            {
                Text      = $"─── {title}",
                ForeColor = Color.FromArgb(190, 160, 70),
                Font      = new Font("Consolas", 8.5f, FontStyle.Bold),
                AutoSize  = false,
                Bounds    = new Rectangle(4, y + 6, 460, 18),
                BackColor = Color.Transparent,
            };
            _scrollContent.Controls.Add(lbl);
            y += 28;
        }

        // ── Satır: Label + TrackBar + NumericUpDown ────────────
        private void Row(
            string label, ref int y,
            float min, float max, float step,
            float current,
            Action<float> onChange,
            bool restart = false)
        {
            // Her satır kendi arka plan panelinde — scroll kırpmasını önler
            var row = new Panel
            {
                Bounds    = new Rectangle(0, y, 470, 28),
                BackColor = Color.FromArgb(26, 26, 32),
            };
            _scrollContent.Controls.Add(row);

            // Etiket
            var lbl = new Label
            {
                Text      = restart ? label + " *" : label,
                ForeColor = restart
                    ? Color.FromArgb(200, 170, 80)
                    : Color.FromArgb(185, 180, 165),
                AutoSize  = false,
                Bounds    = new Rectangle(6, 4, 130, 20),
                BackColor = Color.Transparent,
                Font      = new Font("Consolas", 8.5f, FontStyle.Regular),
            };
            row.Controls.Add(lbl);

            int iMin = (int)Math.Round(min  / step);
            int iMax = (int)Math.Round(max  / step);
            int iVal = Math.Clamp((int)Math.Round(current / step), iMin, iMax);

            // TrackBar — System renkleri kullan (dark tema trackbar'ı gizler)
            var track = new TrackBar
            {
                Minimum   = iMin,
                Maximum   = iMax,
                Value     = iVal,
                TickStyle = TickStyle.None,
                Bounds    = new Rectangle(138, 0, 220, 28),
                // BackColor'u AYARLAMA — sistem teması kullanılsın, görünür olsun
            };

            // NumericUpDown
            float dispVal = Math.Clamp(current, min, max);
            var num = new NumericUpDown
            {
                Minimum       = (decimal)min,
                Maximum       = (decimal)max,
                Value         = (decimal)dispVal,
                DecimalPlaces = step < 1f ? 1 : 0,
                Increment     = (decimal)step,
                Bounds        = new Rectangle(362, 4, 90, 20),
                BackColor     = Color.FromArgb(35, 35, 42),
                ForeColor     = Color.FromArgb(220, 210, 190),
                BorderStyle   = BorderStyle.FixedSingle,
                TextAlign     = HorizontalAlignment.Center,
                Font          = new Font("Consolas", 8.5f, FontStyle.Regular),
            };

            row.Controls.Add(track);
            row.Controls.Add(num);

            // Event bağlantısı
            bool busy = false;
            track.ValueChanged += (_, _) =>
            {
                if (_loading || busy) return;
                busy = true;
                float v = track.Value * step;
                num.Value = (decimal)Math.Clamp(v, min, max);
                onChange(v);
                busy = false;
            };
            num.ValueChanged += (_, _) =>
            {
                if (_loading || busy) return;
                busy = true;
                float v = (float)num.Value;
                track.Value = Math.Clamp((int)Math.Round(v / step), iMin, iMax);
                onChange(v);
                busy = false;
            };

            y += 30;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                SettingsClosed?.Invoke();
            }
            else base.OnFormClosing(e);
        }

        // OnVisibleChanged kaldırıldı — SettingsClosed sadece GameForm'dan çağrılır

        // ─────────────────────────────────────────────────────────
        private static Button MakeButton(string text, Color bg) => new Button
        {
            Text      = text,
            BackColor = bg,
            ForeColor = Color.FromArgb(220, 210, 180),
            FlatStyle = FlatStyle.Flat,
            Height    = 30,
            AutoSize  = true,
            Padding   = new Padding(10, 0, 10, 0),
            Font      = new Font("Consolas", 9f, FontStyle.Regular),
            Cursor    = Cursors.Hand,
        };
    }
}