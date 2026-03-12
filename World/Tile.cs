// ============================================================
// Tile.cs — Haritadaki tek bir kareyi (hücreyi) tanımlayan dosya
// Harita, yan yana dizilmiş bu karelerden oluşur.
// Her kare bir türe sahiptir: duvar, boş alan, kapı, vb.
// ============================================================

namespace G_1_A3D_f.World
{
    // TileType: Bir karenin ne olduğunu belirten "etiket" listesi
    public enum TileType
    {
        Empty,   // Boş — oyuncu buradan geçebilir
        Wall,    // Duvar — geçilemez, ışın burada durur
        Door,    // Kapı — açılabilir geçiş noktası (ilerleyen aşamalarda kullanılacak)
        Start,   // Oyuncunun bölüme başladığı nokta
        Exit     // Bölümün çıkışı — bir sonraki bölüme geçiş
    }

    // Tile sınıfı: Haritadaki tek bir kareyi temsil eder
    public class Tile
    {
        // Bu karenin türü (yukarıdaki TileType listesinden biri)
        public TileType Type { get; set; }

        // Bu kare üzerinde yürünebilir mi?
        // Sadece Empty, Start ve Exit kareler yürünebilir.
        // => ifadesi: "şu an Type bunlardan biri mi?" sorusunu sorar ve true/false döner
        public bool IsWalkable => Type == TileType.Empty
                               || Type == TileType.Start
                               || Type == TileType.Exit;

        // Yeni kare oluştururken tür belirtilmezse varsayılan olarak Empty olur
        public Tile(TileType type = TileType.Empty)
        {
            Type = type;
        }
    }
}
