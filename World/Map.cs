// ============================================================
// Map.cs — Oyun haritasını tutan ve yöneten sınıf
// Harita, 2 boyutlu bir Tile (kare) dizisidir.
// X = yatay konum (sütun), Y = dikey konum (satır)
// ============================================================

namespace G_1_A3D_f.World
{
    public class Map
    {
        // _tiles: Tüm harita karelerini tutan 2 boyutlu dizi
        // _tiles[x, y] → X sütunundaki, Y satırındaki kareyi verir
        private Tile[,] _tiles;

        // Haritanın yatay boyutu (kaç sütun var)
        public int Width { get; private set; }

        // Haritanın dikey boyutu (kaç satır var)
        public int Height { get; private set; }

        // Haritayı oluştururken boyutunu belirtiriz, tüm kareler başlangıçta boş olur
        public Map(int width, int height)
        {
            Width = width;
            Height = height;

            // 2 boyutlu diziyi oluştur
            _tiles = new Tile[width, height];

            // Her kareyi başlangıçta "Empty" (boş) olarak ayarla
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    _tiles[x, y] = new Tile(TileType.Empty);
        }

        // Belirtilen koordinattaki kareyi getir (okuma)
        public Tile GetTile(int x, int y) => _tiles[x, y];

        // Belirtilen koordinata yeni bir kare türü yerleştir (yazma)
        public void SetTile(int x, int y, TileType type) => _tiles[x, y] = new Tile(type);

        // Belirtilen koordinat duvar mı? (Renderer bu fonksiyonu kullanır)
        public bool IsWall(int x, int y)
        {
            // Önce harita sınırları dışında mı kontrol et
            if (x < 0 || x >= Width || y < 0 || y >= Height) return true;
            return _tiles[x, y].Type == TileType.Wall;
        }

        // Belirtilen koordinata yürünebilir mi? (Oyuncu hareketi bu fonksiyona bakır)
        public bool IsWalkable(int x, int y)
        {
            // Sınır dışıysa yürünemez
            if (x < 0 || x >= Width || y < 0 || y >= Height) return false;
            return _tiles[x, y].IsWalkable;
        }
    }
}
