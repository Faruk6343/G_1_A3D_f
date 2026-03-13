// ============================================================
// Program.cs — DPI Aware başlangıç noktası
//
// SetProcessDpiAwarenessContext: Windows'a "Ben DPI'yı kendim
// yönetiyorum, senin ölçeklendirmene gerek yok" der.
// Bu olmadan %125/%150 DPI'da ClientSize yanlış hesaplanır,
// HUD koordinatları kayar.
// ============================================================

using System.Runtime.InteropServices;
using G_1_A3D_f.UI;

internal static class Program
{
    // Windows API: Process'i Per-Monitor V2 DPI aware yapar
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    [STAThread]
    static void Main()
    {
        // DPI ölçeklendirmesini devre dışı bırak — koordinatlar 1:1 piksel olur
        try { SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* Eski Windows sürümlerinde desteklenmeyebilir, devam et */ }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameForm());
    }
}