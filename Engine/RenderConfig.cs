// ============================================================
// RenderConfig.cs — Tüm görüntü parametrelerini tutan sınıf
//
// KULLANIM:
//   Oyun ve SettingsForm aynı RenderConfig örneğini paylaşır.
//   SettingsForm bir değeri değiştirince, Renderer bir sonraki
//   karede otomatik olarak yeni değeri kullanır.
//
//   Yeniden başlatma gerektiren ayarlar (çözünürlük, font) için
//   NeedsRestart = true set edilir, GameForm bunu kontrol eder.
// ============================================================

namespace G_1_A3D_f.Engine
{
    public class RenderConfig
    {
        // ── Çözünürlük (değişince NeedsRestart = true) ──────────
        public int  Cols     { get; set; } = 480;
        public int  Rows     { get; set; } = 135;
        public int  CellW    { get; set; } = 4;
        public int  CellH    { get; set; } = 8;
        public float FontSize { get; set; } = 5.5f;

        // ── Kamera / Işın ───────────────────────────────────────
        public float Fov      { get; set; } = 60f;   // Derece cinsinden
        public float MaxDepth { get; set; } = 22f;
        public float RayStep  { get; set; } = 0.012f;

        // ── Işıklandırma ────────────────────────────────────────
        public float LightFalloff { get; set; } = 0.18f;  // Büyük = hızlı kararma
        public float Ambient      { get; set; } = 0.15f;  // Minimum ışık

        // ── Duvar rengi temel değerleri ─────────────────────────
        public float WallBaseR  { get; set; } = 118f;  // Tuğla kırmızısı
        public float WallBaseG  { get; set; } =  52f;
        public float WallBaseB  { get; set; } =  30f;
        public float WallNSMult { get; set; } = 1.00f;  // NS yüz çarpanı
        public float WallEWMult { get; set; } = 0.72f;  // EW yüz çarpanı

        // ── Tavan rengi ─────────────────────────────────────────
        public float CeilBaseR   { get; set; } = 68f;
        public float CeilBaseG   { get; set; } = 48f;
        public float CeilBaseB   { get; set; } = 30f;
        public float CeilBright  { get; set; } = 0.55f;  // Başlangıç parlaklığı
        public float CeilFade    { get; set; } = 0.25f;  // Mesafeyle kararma

        // ── Zemin rengi ─────────────────────────────────────────
        public float FloorBaseR  { get; set; } = 92f;
        public float FloorBaseG  { get; set; } = 66f;
        public float FloorBaseB  { get; set; } = 40f;
        public float FloorBright { get; set; } = 0.60f;
        public float FloorFade   { get; set; } = 0.35f;

        // ── Doku yoğunluğu ──────────────────────────────────────
        // 0.0 = çok seyrek (az karakter), 1.0 = çok yoğun (blok karakter)
        public float WallDensity  { get; set; } = 0.55f;
        public float CeilDensity  { get; set; } = 0.60f;
        public float FloorDensity { get; set; } = 0.60f;

        // ── Tuğla doku ──────────────────────────────────────────
        public float BrickW  { get; set; } = 0.85f;  // Tuğla genişliği
        public float BrickH  { get; set; } = 0.42f;  // Tuğla yüksekliği
        public float GroutW  { get; set; } = 0.055f; // Derz kalınlığı

        // ── Zemin / Tavan doku ──────────────────────────────────
        // Tile ölçeği: Büyük = daha az derz çizgisi (daha büyük kareler)
        public float FloorTileScale { get; set; } = 2.0f;   // Zemin kare boyutu
        public float CeilTileScale  { get; set; } = 2.0f;   // Tavan kare boyutu
        // Derz eşiği: Küçük = daha ince çizgi (0.02 = tile'ın %4'ü)
        public float FloorGrout     { get; set; } = 0.025f;
        public float CeilGrout      { get; set; } = 0.025f;

        // ── Oyuncu / Kamera ─────────────────────────────────────
        public float PitchMax    { get; set; } = 18f;
        public float Sensitivity { get; set; } = 0.20f;
        public float MoveSpeed   { get; set; } = 6.0f;

        // ── Ses / Akustik ────────────────────────────────────────
        public bool  AudioEnabled { get; set; } = true;
        public float AudioWetMix  { get; set; } = 0.55f;  // 0=kuru, 1=tam reverb
        public float AudioGain    { get; set; } = 1.0f;   // Çıkış ses seviyesi

        // ── Durum bayrakları ────────────────────────────────────
        // Çözünürlük veya font değişince true — GameForm yeniden başlatır
        // JSON'a kaydedilmez: Açılışta her zaman false başlamalı
        [System.Text.Json.Serialization.JsonIgnore]
        public bool NeedsRestart { get; set; } = false;

        // ── Kaydetme / Yükleme ──────────────────────────────────
        // Ayar dosyası: Oyun exe'sinin yanında "render.cfg.json"
        private static string ConfigPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "render.cfg.json");

        // Mevcut ayarları diske yazar
        public void Save()
        {
            try
            {
                var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(this, opts);
                File.WriteAllText(ConfigPath, json);
            }
            catch { /* Yazma hatası sessizce görmezden gel */ }
        }

        // Diskten yükler; dosya yoksa ya da bozuksa varsayılanları korur
        public static RenderConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<RenderConfig>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch { /* Okuma/parse hatası → varsayılan döner */ }
            return new RenderConfig();
        }

        // ── Varsayılanlara sıfırla ──────────────────────────────
        public void ResetToDefaults()
        {
            Cols = 480; Rows = 135; CellW = 4; CellH = 8; FontSize = 5.5f;
            Fov = 60f; MaxDepth = 22f; RayStep = 0.012f;
            LightFalloff = 0.18f; Ambient = 0.15f;
            WallBaseR = 118f; WallBaseG = 52f; WallBaseB = 30f;
            WallNSMult = 1.00f; WallEWMult = 0.72f;
            CeilBaseR = 68f; CeilBaseG = 48f; CeilBaseB = 30f;
            CeilBright = 0.55f; CeilFade = 0.25f;
            FloorBaseR = 92f; FloorBaseG = 66f; FloorBaseB = 40f;
            FloorBright = 0.60f; FloorFade = 0.35f;
            WallDensity = 0.55f; CeilDensity = 0.60f; FloorDensity = 0.60f;
            BrickW = 0.85f; BrickH = 0.42f; GroutW = 0.055f;
            FloorTileScale = 2.0f; CeilTileScale = 2.0f;
            FloorGrout = 0.025f; CeilGrout = 0.025f;
            PitchMax = 18f; Sensitivity = 0.20f; MoveSpeed = 6.0f;
            AudioEnabled = true; AudioWetMix = 0.55f; AudioGain = 1.0f;
            NeedsRestart = false;
        }
    }
}