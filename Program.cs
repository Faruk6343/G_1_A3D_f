// ============================================================
// Program.cs — Oyunun başlangıç noktası
//
// [STAThread]: Windows Forms için zorunlu öznitelik.
//   "Single Thread Apartment" modunu etkinleştirir.
//   Bu mod olmadan pencere ve fare olayları doğru çalışmaz.
//   ÖNEMLİ: Bu öznitelik sadece klasik Main() metoduna
//   uygulanabilir — top-level statements ile çalışmaz.
//
// Application.Run: GameForm penceresini açar ve pencere
//   kapanana kadar programı çalışır durumda tutar.
// ============================================================

using G_1_A3D_f.UI;

// Klasik Main metodu — [STAThread] özniteliği için gerekli
internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Visual Styles: Windows'un modern görsel temasını etkinleştirir
        Application.EnableVisualStyles();

        // Eski .NET 1.x uyumluluk modu kapalı — modern metin çizimi kullan
        Application.SetCompatibleTextRenderingDefault(false);

        // Oyun penceresini aç ve kapanana kadar çalış
        Application.Run(new GameForm());
    }
}