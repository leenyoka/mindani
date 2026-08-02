using Godot;
using System.Collections.Generic;

namespace Mindani.World;

public partial class VoxelWorld : Node3D
{
    // ── World dimensions ─────────────────────────────────────────────────
    public const int Width  = 64;
    public const int Height = 48;
    public const int Depth  = 64;

    // ── Block IDs ────────────────────────────────────────────────────────
    public const byte Air    = 0;
    public const byte Grass  = 1;
    public const byte Dirt   = 2;
    public const byte Stone  = 3;
    public const byte Sand   = 4;
    public const byte Wood   = 5;
    public const byte Leaves = 6;
    public const byte Flower = 7;
    public const byte Brick  = 8;
    public const byte Plank  = 9;
    public const byte Glass  = 10;

    // ── Material indices ──────────────────────────────────────────────────
    const int MatGrassTop  = 0;
    const int MatGrassSide = 1;
    const int MatDirt      = 2;
    const int MatStone     = 3;
    const int MatSand      = 4;
    const int MatWood      = 5;
    const int MatLeaves    = 6;
    const int MatFlower    = 7;
    const int MatBrick     = 8;
    const int MatPlank     = 9;
    const int MatGlass     = 10;
    const int MatCount     = 11;

    readonly byte[,,] _data = new byte[Width, Height, Depth];
    readonly System.Random _rng = new(42); // fixed seed = same world every run

    MeshInstance3D   _mesh     = null!;
    StaticBody3D     _body     = null!;
    CollisionShape3D _colShape = null!;

    readonly StandardMaterial3D[] _mats = new StandardMaterial3D[MatCount];

    static readonly (Vector3I dir, Vector3 normal, Vector3[] quad)[] Faces =
    [
        (new( 0, 1, 0), Vector3.Up,      [new(0,1,0), new(1,1,0), new(1,1,1), new(0,1,1)]),
        (new( 0,-1, 0), Vector3.Down,    [new(0,0,1), new(1,0,1), new(1,0,0), new(0,0,0)]),
        (new( 1, 0, 0), Vector3.Right,   [new(1,0,0), new(1,0,1), new(1,1,1), new(1,1,0)]),
        (new(-1, 0, 0), Vector3.Left,    [new(0,0,1), new(0,0,0), new(0,1,0), new(0,1,1)]),
        (new( 0, 0, 1), Vector3.Back,    [new(1,0,1), new(0,0,1), new(0,1,1), new(1,1,1)]),
        (new( 0, 0,-1), Vector3.Forward, [new(0,0,0), new(1,0,0), new(1,1,0), new(0,1,0)]),
    ];
    static readonly Vector2[] QuadUVs = [new(0,1), new(1,1), new(1,0), new(0,0)];

    // ── Setup ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _mesh = new MeshInstance3D();
        AddChild(_mesh);

        _body = new StaticBody3D();
        _body.AddToGroup("terrain");
        AddChild(_body);

        _colShape = new CollisionShape3D();
        _body.AddChild(_colShape);

        LoadMaterials();
        GenerateTerrain();
        RebuildMesh();
    }

    void LoadMaterials()
    {
        _mats[MatGrassTop]  = MakeTex("res://assets/blocks/grass_top.png");
        _mats[MatGrassSide] = MakeTex("res://assets/blocks/grass_side.png");
        _mats[MatDirt]      = MakeTex("res://assets/blocks/dirt.png");
        _mats[MatStone]     = MakeTex("res://assets/blocks/stone.png");
        _mats[MatSand]      = MakeTex("res://assets/blocks/sand.png");
        _mats[MatWood]      = MakeCol(new Color(0.38f, 0.24f, 0.10f));
        _mats[MatLeaves]    = MakeCol(new Color(0.15f, 0.52f, 0.10f));
        _mats[MatFlower]    = MakeCol(new Color(1.00f, 0.25f, 0.40f));
        _mats[MatBrick]     = MakeCol(new Color(0.52f, 0.24f, 0.14f));
        _mats[MatPlank]     = MakeCol(new Color(0.72f, 0.58f, 0.35f));
        _mats[MatGlass]     = MakeCol(new Color(0.70f, 0.88f, 1.00f));
    }

    static StandardMaterial3D MakeTex(string path) => new()
    {
        AlbedoTexture = GD.Load<Texture2D>(path),
        TextureFilter  = BaseMaterial3D.TextureFilterEnum.Nearest,
    };

    static StandardMaterial3D MakeCol(Color c) => new() { AlbedoColor = c };

    // ── Terrain + world generation ────────────────────────────────────────

    void GenerateTerrain()
    {
        const int SeaLevel = 20;

        // Base terrain
        for (int x = 0; x < Width;  x++)
        for (int z = 0; z < Depth;  z++)
        {
            float nx = x * 0.07f, nz = z * 0.07f;
            float h  = Mathf.Sin(nx) * Mathf.Cos(nz) * 5f
                     + Mathf.Sin(nx * 2.3f + 0.9f) * 2f;
            int surface = SeaLevel + Mathf.RoundToInt(h);

            for (int y = 0; y < Height; y++)
            {
                _data[x, y, z] =
                    y > surface      ? Air
                  : y == surface     ? Grass
                  : y > surface - 4  ? Dirt
                  :                    Stone;
            }
        }

        // City footprint (far corner, away from player spawn at 32,30,32)
        const int CX0 = 38, CZ0 = 38, CX1 = 61, CZ1 = 61;
        int cityY = SurfaceAt(50, 50);
        FlattenArea(CX0, CZ0, CX1, CZ1, cityY);

        // Trees and flowers (avoid city zone)
        for (int x = 2; x < Width  - 2; x++)
        for (int z = 2; z < Depth  - 2; z++)
        {
            if (x >= CX0 - 3 && x <= CX1 + 3 && z >= CZ0 - 3 && z <= CZ1 + 3) continue;
            int sy = SurfaceAt(x, z);
            if (_data[x, sy, z] != Grass) continue;

            double roll = _rng.NextDouble();
            if      (roll < 0.04) PlaceTree(x, sy, z);
            else if (roll < 0.08) PlaceFlower(x, sy, z);
        }

        // Build the city
        BuildCity(CX0, CZ0, CX1, CZ1, cityY);
    }

    int SurfaceAt(int x, int z)
    {
        if ((uint)x >= Width || (uint)z >= Depth) return 0;
        for (int y = Height - 1; y >= 0; y--)
            if (_data[x, y, z] != Air) return y;
        return 0;
    }

    void FlattenArea(int x0, int z0, int x1, int z1, int targetY)
    {
        for (int x = x0; x <= x1; x++)
        for (int z = z0; z <= z1; z++)
        for (int y = 0;  y <  Height; y++)
        {
            _data[x, y, z] =
                y < targetY  ? Stone
              : y == targetY ? Dirt
              :                Air;
        }
    }

    void PlaceTree(int x, int sy, int z)
    {
        int h = _rng.Next(4, 7);
        for (int t = 1; t <= h; t++)
            Set(x, sy + t, z, Wood);

        int cy = sy + h;
        for (int lx = -2; lx <= 2; lx++)
        for (int ly = -1; ly <= 2; ly++)
        for (int lz = -2; lz <= 2; lz++)
        {
            if (lx == 0 && lz == 0 && ly < 1) continue;
            if (Mathf.Sqrt(lx*lx + ly*ly*0.6f + lz*lz) <= 2.1f)
                Set(x + lx, cy + ly, z + lz, Leaves);
        }
    }

    void PlaceFlower(int x, int sy, int z) => Set(x, sy + 1, z, Flower);

    void BuildCity(int x0, int z0, int x1, int z1, int groundY)
    {
        int midX = (x0 + x1) / 2;
        int midZ = (z0 + z1) / 2;

        // Stone roads
        for (int x = x0; x <= x1; x++)
        {
            Set(x, groundY, midZ - 1, Stone);
            Set(x, groundY, midZ,     Stone);
            Set(x, groundY, midZ + 1, Stone);
        }
        for (int z = z0; z <= z1; z++)
        {
            Set(midX - 1, groundY, z, Stone);
            Set(midX,     groundY, z, Stone);
            Set(midX + 1, groundY, z, Stone);
        }

        // Four buildings in the quadrants
        PlaceBuilding(x0 + 1, groundY, z0 + 1,  8, 7, 5, Brick);
        PlaceBuilding(midX+3, groundY, z0 + 1,  7, 7, 8, Plank);
        PlaceBuilding(x0 + 1, groundY, midZ + 3, 8, 7, 6, Brick);
        PlaceBuilding(midX+3, groundY, midZ + 3, 7, 7, 9, Plank);

        // Decorative lamp-posts at road intersections
        int[] lpX = { midX - 6, midX + 6, midX - 6, midX + 6 };
        int[] lpZ = { midZ - 6, midZ - 6, midZ + 6, midZ + 6 };
        for (int i = 0; i < 4; i++)
        {
            for (int y = 1; y <= 4; y++) Set(lpX[i], groundY + y, lpZ[i], Wood);
            Set(lpX[i], groundY + 5, lpZ[i],     Leaves);
        }
    }

    void PlaceBuilding(int bx, int by, int bz, int w, int d, int h, byte wall)
    {
        for (int x = bx; x < bx + w; x++)
        for (int z = bz; z < bz + d; z++)
        {
            bool onX = x == bx || x == bx + w - 1;
            bool onZ = z == bz || z == bz + d - 1;
            bool isWall   = onX || onZ;
            bool isCorner = onX && onZ;

            Set(x, by + 1, z, Plank); // floor

            for (int y = by + 2; y <= by + h; y++)
            {
                if (y == by + h)
                {
                    Set(x, y, z, wall); // roof
                }
                else if (isWall)
                {
                    bool win = !isCorner
                             && (y - by - 2) % 3 == 1
                             && (onX ? (z - bz) % 3 == 1 : (x - bx) % 3 == 1);
                    Set(x, y, z, win ? Glass : wall);
                }
            }
        }
    }

    void Set(int x, int y, int z, byte block)
    {
        if ((uint)x < Width && (uint)y < Height && (uint)z < Depth)
            _data[x, y, z] = block;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public byte GetBlock(int x, int y, int z)
    {
        if ((uint)x >= Width || (uint)y >= Height || (uint)z >= Depth) return Air;
        return _data[x, y, z];
    }

    public void SetBlock(Vector3I pos, byte block)
    {
        if ((uint)pos.X >= Width || (uint)pos.Y >= Height || (uint)pos.Z >= Depth) return;
        _data[pos.X, pos.Y, pos.Z] = block;
        RebuildMesh();
    }

    // ── Mesh building ─────────────────────────────────────────────────────

    void RebuildMesh()
    {
        var verts  = new List<Vector3>[MatCount];
        var uvs    = new List<Vector2>[MatCount];
        var norms  = new List<Vector3>[MatCount];
        var tris   = new List<int>[MatCount];
        for (int i = 0; i < MatCount; i++) { verts[i]=[]; uvs[i]=[]; norms[i]=[]; tris[i]=[]; }

        for (int x = 0; x < Width;  x++)
        for (int y = 0; y < Height; y++)
        for (int z = 0; z < Depth;  z++)
        {
            byte block = _data[x, y, z];
            if (block == Air) continue;

            foreach (var (dir, norm, quad) in Faces)
            {
                byte nb = GetBlock(x + dir.X, y + dir.Y, z + dir.Z);
                if (nb != Air && nb != Leaves && nb != Glass) continue;
                if (nb == block) continue;

                int m = FaceMat(block, dir.Y);
                int b = verts[m].Count;

                var origin = new Vector3(x, y, z);
                foreach (var c in quad) verts[m].Add(origin + c);
                norms[m].AddRange([norm, norm, norm, norm]);
                uvs[m].AddRange(QuadUVs);
                tris[m].AddRange([b, b+1, b+2, b, b+2, b+3]);
            }
        }

        var arrayMesh = new ArrayMesh();
        for (int m = 0; m < MatCount; m++)
        {
            if (verts[m].Count == 0) continue;

            var arr = new Godot.Collections.Array();
            arr.Resize((int)Mesh.ArrayType.Max);
            arr[(int)Mesh.ArrayType.Vertex] = verts[m].ToArray();
            arr[(int)Mesh.ArrayType.Normal] = norms[m].ToArray();
            arr[(int)Mesh.ArrayType.TexUV]  = uvs[m].ToArray();
            arr[(int)Mesh.ArrayType.Index]  = tris[m].ToArray();

            int surf = arrayMesh.GetSurfaceCount();
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arr);
            arrayMesh.SurfaceSetMaterial(surf, _mats[m]);
        }

        _mesh.Mesh = arrayMesh;
        _colShape.Shape = arrayMesh.CreateTrimeshShape();
    }

    static int FaceMat(byte block, int dirY) => block switch
    {
        Grass  => dirY > 0 ? MatGrassTop : dirY < 0 ? MatDirt : MatGrassSide,
        Dirt   => MatDirt,
        Stone  => MatStone,
        Sand   => MatSand,
        Wood   => MatWood,
        Leaves => MatLeaves,
        Flower => MatFlower,
        Brick  => MatBrick,
        Plank  => MatPlank,
        Glass  => MatGlass,
        _      => MatDirt,
    };
}
