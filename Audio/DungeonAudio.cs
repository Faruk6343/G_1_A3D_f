// ============================================================
// DungeonAudio.cs — Gerçek Zindan Akustik Motoru v3
//
// 3 KATMANLI AKUSTİK:
//
//   KATMAN 1 — ERKEn YANSILAMALAR (Early Reflections)
//     Duvara çarpan ilk 6 yankı: keskin, ayrı ayrı duyulur.
//     Gecikme: duvar_mesafesi × 2 / ses_hızı
//     (Gidip gelmesi = ×2)
//     Bu katman "mağarada yankı var" hissini verir.
//
//   KATMAN 2 — GEÇ REVERB (Late Reverb Tail)
//     Çok sayıda yansımanın üst üste binerek oluşturduğu
//     yoğun ses bulutu. RT60 = 1.5-3 saniye (mağara değerleri).
//     Freeverb mimarisi: 8 comb + 4 allpass.
//
//   KATMAN 3 — GERÇEK STEREO
//     Sol ve sağ kanal AYRI reverb zincirleri.
//     Sol: Standart Freeverb gecikmeleri
//     Sağ: %8 farklı gecikmeler → farklı oda hissi
//     Erken yansılamalar: Sol duvardakiler sola, sağ duvardakiler sağa
//
//   AGC: Düşük sesler için otomatik kazanç, fısıltıyı algılar
//   Noise Gate: -72 dBFS (0.00025f) — neredeyse sıfır eşik
// ============================================================

using NAudio.Wave;
using G_1_A3D_f.World;

namespace G_1_A3D_f.Audio
{
    public class DungeonAudio : IDisposable
    {
        private const int   SR             = 44100;   // Sample rate
        private const float SOUND_SPEED    = 343.0f;  // m/s
        private const float WORLD_TO_METER = 3.0f;    // 1 oyun birimi = 3 metre

        // ── Freeverb gecikme setleri (samples) ──────────────────
        // Sol ve sağ kanal farklı → gerçek stereo his
        private static readonly int[] COMB_L = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
        private static readonly int[] COMB_R = { 1687, 1751, 1617, 1538, 1381, 1468, 1286, 1208 }; // ~%8 farklı
        private static readonly int[] AP_L   = { 225, 556, 441, 341 };
        private static readonly int[] AP_R   = { 243, 601, 477, 369 };

        // ── NAudio nesneleri ────────────────────────────────────
        private WaveInEvent?          _waveIn;
        private WaveOutEvent?         _waveOut;
        private BufferedWaveProvider? _outBuf;
        private bool _ready = false, _disposed = false;

        // ── Sol/Sağ reverb zincirleri ────────────────────────────
        private CaveComb[]   _combL = null!, _combR = null!;
        private CaveAllpass[] _apL  = null!, _apR   = null!;

        // ── Erken yansımalar tamponu ─────────────────────────────
        // 6 yankı noktası (raycasting'den gelen duvar mesafeleri)
        private readonly EarlyReflection[] _erL = new EarlyReflection[6];
        private readonly EarlyReflection[] _erR = new EarlyReflection[6];

        // ── Pre-delay (doğrudan ses → reverb arasındaki boşluk) ─
        private CircularBuffer _preDelay = new(SR / 20); // 50ms max

        // ── AGC ─────────────────────────────────────────────────
        private float _agcEnv = 0.001f;

        // ── Oda durumu ──────────────────────────────────────────
        private float _decay   = 0.82f;  // Reverb uzunluğu (0.7=kısa, 0.9=uzun)
        private float _damp    = 0.3f;   // Tiz sönümü
        private float _preDlSp = 882;    // Pre-delay samples

        public bool  Enabled { get; set; } = true;
        public float WetMix  { get; set; } = 0.65f;
        public float Gain    { get; set; } = 1.0f;

        // ─────────────────────────────────────────────────────────
        public DungeonAudio()
        {
            // Reverb zincirleri
            _combL = COMB_L.Select(d => new CaveComb(d, 0.82f, 0.3f)).ToArray();
            _combR = COMB_R.Select(d => new CaveComb(d, 0.82f, 0.3f)).ToArray();
            _apL   = AP_L.Select(d => new CaveAllpass(d)).ToArray();
            _apR   = AP_R.Select(d => new CaveAllpass(d)).ToArray();

            // Erken yansılamalar — başlangıçta 5-15m arası eşit aralıklı
            for (int i = 0; i < 6; i++)
            {
                float dist   = 5f + i * 2f;
                int   delay  = DistToSamples(dist);
                float gain   = 0.7f - i * 0.1f; // Uzak yankı daha sessiz
                _erL[i] = new EarlyReflection(delay, gain);
                _erR[i] = new EarlyReflection((int)(delay * 1.05f), gain * 0.9f);
            }
        }

        private static int DistToSamples(float worldDist)
        {
            // Yankı = gidip gelme (×2), ses hızı bölü
            float meters  = worldDist * WORLD_TO_METER;
            float seconds = (meters * 2f) / SOUND_SPEED;
            return Math.Clamp((int)(seconds * SR), 50, SR * 2);
        }

        // ─────────────────────────────────────────────────────────
        public bool Start()
        {
            if (_ready) return true;
            try
            {
                var fmtIn  = new WaveFormat(SR, 16, 1); // Mono giriş
                var fmtOut = new WaveFormat(SR, 16, 2); // Stereo çıkış

                _outBuf = new BufferedWaveProvider(fmtOut)
                {
                    BufferDuration          = TimeSpan.FromSeconds(4),
                    DiscardOnBufferOverflow = true,
                };

                _waveOut = new WaveOutEvent { DesiredLatency = 120 };
                _waveOut.Init(_outBuf);
                _waveOut.Play();

                _waveIn = new WaveInEvent { WaveFormat = fmtIn, BufferMilliseconds = 20 };
                _waveIn.DataAvailable    += OnMicData;
                _waveIn.RecordingStopped += (_, _) => { };
                _waveIn.StartRecording();

                _ready = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] {ex.Message}");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────
        // Ana ses işleme döngüsü
        // ─────────────────────────────────────────────────────────
        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            if (!Enabled || _outBuf == null) return;

            int    n      = e.BytesRecorded / 2;
            byte[] output = new byte[n * 4]; // Stereo 16-bit = 4 byte/frame

            for (int i = 0; i < n; i++)
            {
                // ── Giriş ──
                float dry = BitConverter.ToInt16(e.Buffer, i * 2) / 32768.0f;

                // ── AGC: Fısıltıyı bile yakala ──
                float absIn = MathF.Abs(dry);
                float agcAttack  = 0.002f;
                float agcRelease = 0.00005f;
                _agcEnv = absIn > _agcEnv
                    ? _agcEnv + (absIn - _agcEnv) * agcAttack
                    : _agcEnv * (1f - agcRelease);
                _agcEnv  = MathF.Max(_agcEnv, 0.0005f);
                float agcGain = MathF.Min(0.25f / (_agcEnv + 1e-7f), 50f);
                dry *= agcGain;

                // ── Noise Gate: -72 dBFS ──
                if (MathF.Abs(dry) < 0.00025f) dry = 0f;

                // ── Pre-delay ──
                float preDry = _preDelay.Read((int)_preDlSp);
                _preDelay.Write(dry);

                // ── KATMAN 1: Erken Yansımalar ──
                // Sol: Sol duvardaki yankılar daha güçlü
                // Sağ: Sağ duvardaki yankılar daha güçlü
                float erL = 0f, erR = 0f;
                for (int r = 0; r < 6; r++)
                {
                    erL += _erL[r].Process(preDry);
                    erR += _erR[r].Process(preDry);
                }
                erL *= 0.35f;
                erR *= 0.35f;

                // ── KATMAN 2: Geç Reverb (Freeverb) ──
                // Sol reverb zinciri
                float wetL = 0f;
                foreach (var c in _combL) wetL += c.Process(preDry + erL * 0.3f);
                wetL *= 0.125f;
                foreach (var a in _apL)  wetL  = a.Process(wetL);

                // Sağ reverb zinciri (bağımsız)
                float wetR = 0f;
                foreach (var c in _combR) wetR += c.Process(preDry + erR * 0.3f);
                wetR *= 0.125f;
                foreach (var a in _apR)  wetR  = a.Process(wetR);

                // ── KATMAN 3: Karıştır ──
                // Erken yansımalar + geç reverb birlikte "ıslak" sinyali oluşturur
                float totalWetL = erL + wetL;
                float totalWetR = erR + wetR;

                float outL = (dry * (1f - WetMix) + totalWetL * WetMix) * Gain;
                float outR = (dry * (1f - WetMix) + totalWetR * WetMix) * Gain;

                // ── Stereo görüntüleme ──
                // Mid-Side genişletme: L-R farkını artır
                float mid  = (outL + outR) * 0.5f;
                float side = (outL - outR) * 1.4f; // Side %40 artırıldı
                outL = mid + side;
                outR = mid - side;

                WriteS16(output, i * 4,     outL);
                WriteS16(output, i * 4 + 2, outR);
            }

            _outBuf.AddSamples(output, 0, output.Length);
        }

        private static void WriteS16(byte[] b, int off, float v)
        {
            short s = (short)Math.Clamp(v * 32767f, -32768f, 32767f);
            b[off]     = (byte)(s & 0xFF);
            b[off + 1] = (byte)((s >> 8) & 0xFF);
        }

        // ─────────────────────────────────────────────────────────
        // Raycasting → Akustik güncelle
        // ─────────────────────────────────────────────────────────
        public void UpdateAcoustics(Map map, float px, float py, float angle)
        {
            if (!_ready) return;

            // 8 yöne ışın
            float[] dists = new float[8];
            for (int d = 0; d < 8; d++)
            {
                float a = angle + d * MathF.PI / 4f;
                float ex = MathF.Sin(a), ey = MathF.Cos(a);
                float dist = 0.15f;
                while (dist < 22f)
                {
                    int mx = (int)(px + ex * dist), my = (int)(py + ey * dist);
                    if (mx < 0 || mx >= map.Width || my < 0 || my >= map.Height
                        || map.IsWall(mx, my)) break;
                    dist += 0.15f;
                }
                dists[d] = dist;
            }

            float avg = dists.Average();
            float max = dists.Max();

            // Oda boyutu → reverb decay (büyük alan = uzun yankı)
            // Mağara: decay 0.82-0.92 arası (RT60 ≈ 1.5-3s)
            float targetDecay = 0.78f + Math.Clamp(avg / 22f, 0f, 1f) * 0.14f;
            _decay += (targetDecay - _decay) * 0.015f;

            // Damping: Taş duvar = düşük damping (tizler uzun yaşar)
            float targetDamp = 0.15f + (1f - max / 22f) * 0.35f;
            _damp += (targetDamp - _damp) * 0.015f;

            // Pre-delay: 5ms (koridorda) → 40ms (büyük mağara)
            float preDlMs = 5f + Math.Clamp(avg / 22f, 0f, 1f) * 35f;
            _preDlSp = preDlMs / 1000f * SR;

            // Reverb parametrelerini güncelle
            for (int c = 0; c < _combL.Length; c++)
            {
                _combL[c].SetParams(_decay, _damp);
                _combR[c].SetParams(_decay, _damp);
            }

            // Erken yansılamaları duvar mesafelerine göre güncelle
            // Sol taraf (yönler 6,7,0,1) → sol kanal
            // Sağ taraf (yönler 2,3,4,5) → sağ kanal
            float[] leftDists  = new[] { dists[7], dists[0], dists[1], dists[6], dists[5], dists[2] };
            float[] rightDists = new[] { dists[3], dists[4], dists[5], dists[2], dists[1], dists[0] };

            for (int r = 0; r < 6; r++)
            {
                int   dlL = DistToSamples(leftDists[r]);
                int   dlR = DistToSamples(rightDists[r]);
                float gL  = Math.Clamp(0.75f - leftDists[r]  / 30f, 0.1f, 0.75f);
                float gR  = Math.Clamp(0.75f - rightDists[r] / 30f, 0.1f, 0.75f);
                _erL[r].Update(dlL, gL);
                _erR[r].Update(dlR, gR);
            }
        }

        public void Stop()
        {
            try { _waveIn?.StopRecording(); _waveOut?.Stop(); } catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            Thread.Sleep(80);
            _waveIn?.Dispose();
            _waveOut?.Dispose();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Dairesel tampon — Gecikme hatları için
    // ─────────────────────────────────────────────────────────────
    internal class CircularBuffer
    {
        private readonly float[] _buf;
        private int _wp = 0;
        public CircularBuffer(int size) => _buf = new float[Math.Max(size, 1)];
        public void Write(float v) { _buf[_wp] = v; _wp = (_wp + 1) % _buf.Length; }
        public float Read(int delay)
        {
            int idx = ((_wp - delay) % _buf.Length + _buf.Length) % _buf.Length;
            return _buf[idx];
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Erken Yansıma — Duvardan gelen ilk keskin yankı
    // ─────────────────────────────────────────────────────────────
    internal class EarlyReflection
    {
        private float[] _buf;
        private int     _size, _pos;
        private float   _gain;

        public EarlyReflection(int delay, float gain)
        {
            _size = Math.Max(delay, 1);
            _buf  = new float[_size];
            _gain = gain;
        }

        public float Process(float input)
        {
            float out_ = _buf[_pos] * _gain;
            _buf[_pos] = input;
            _pos       = (_pos + 1) % _size;
            return out_;
        }

        public void Update(int delay, float gain)
        {
            _gain = _gain * 0.97f + gain * 0.03f; // Smooth
            int newSize = Math.Max(delay, 1);
            if (Math.Abs(newSize - _size) > _size / 10) // %10'dan fazla değişince yeniden boyutlandır
            {
                _size = newSize;
                _buf  = new float[_size];
                _pos  = 0;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Freeverb Comb — Damping ile
    // ─────────────────────────────────────────────────────────────
    internal class CaveComb
    {
        private float[] _buf;
        private int     _pos, _size;
        private float   _feedback, _damp, _store;

        public CaveComb(int size, float feedback, float damp)
        {
            _size     = size;
            _buf      = new float[size];
            _feedback = feedback;
            _damp     = damp;
        }

        public float Process(float inp)
        {
            float out_ = _buf[_pos];
            _store     = out_ * (1f - _damp) + _store * _damp;
            _buf[_pos] = inp + _store * _feedback;
            _pos       = (_pos + 1) % _size;
            return out_;
        }

        public void SetParams(float feedback, float damp)
        {
            _feedback += (feedback - _feedback) * 0.03f;
            _damp     += (damp     - _damp)     * 0.03f;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Freeverb Allpass
    // ─────────────────────────────────────────────────────────────
    internal class CaveAllpass
    {
        private float[] _buf;
        private int     _pos, _size;
        private const float GAIN = 0.5f;

        public CaveAllpass(int size) { _size = size; _buf = new float[size]; }

        public float Process(float inp)
        {
            float bufOut = _buf[_pos];
            _buf[_pos]   = inp + bufOut * GAIN;
            _pos         = (_pos + 1) % _size;
            return bufOut - inp;
        }
    }
}