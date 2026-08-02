using Godot;
using System.Collections.Generic;

namespace Mindani.World;

public partial class VoxelWorld : Node3D
{
    // ── World dimensions ──────────────────────────────────────────────────
    public const int Width  = 512;
    public const int Height = 64;
    public const int Depth  = 512;

    const int CW         = 32; // chunk width  (X)
    const int CD         = 32; // chunk depth  (Z)
    const int RenderDist =  3; // chunks in each direction → 7×7 = 49 active

    // ── Block IDs ─────────────────────────────────────────────────────────
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

    // ── Data ──────────────────────────────────────────────────────────────
    readonly byte[,,] _data = new byte[Width, Height, Depth]; // 16 MB, Air = 0 by default
    readonly System.Random _rng = new(42);

    public static readonly List<(string Name, Vector3 Position)> SpawnPoints = new();

    // ── Chunk system ──────────────────────────────────────────────────────
    struct ChunkObjs
    {
        public MeshInstance3D   Mesh;
        public StaticBody3D     Body;
        public CollisionShape3D Col;
    }

    readonly Dictionary<(int, int), ChunkObjs> _active   = new();
    (int cx, int cz)                             _lastChunk = (-999, -999);
    CharacterBody3D?                             _player;

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
        LoadMaterials();
        GenerateTerrain();

        _player = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");

        // Spawn starts at world position (32,24,32) → chunk (1,1)
        const int startCX = 1, startCZ = 1;
        _lastChunk = (startCX, startCZ);
        UpdateChunks(startCX, startCZ);
    }

    public override void _Process(double _delta)
    {
        if (_player == null) return;

        int cx = Mathf.Clamp((int)(_player.GlobalPosition.X / CW), 0, Width  / CW - 1);
        int cz = Mathf.Clamp((int)(_player.GlobalPosition.Z / CD), 0, Depth  / CD - 1);

        if ((cx, cz) == _lastChunk) return;
        _lastChunk = (cx, cz);
        UpdateChunks(cx, cz);
    }

    // ── Chunk management ──────────────────────────────────────────────────

    void UpdateChunks(int pcx, int pcz)
    {
        int x0 = Mathf.Max(0, pcx - RenderDist);
        int x1 = Mathf.Min(Width  / CW - 1, pcx + RenderDist);
        int z0 = Mathf.Max(0, pcz - RenderDist);
        int z1 = Mathf.Min(Depth  / CD - 1, pcz + RenderDist);

        // Unload chunks that scrolled out of range
        var remove = new List<(int, int)>();
        foreach (var key in _active.Keys)
        {
            var (kx, kz) = key;
            if (kx < x0 || kx > x1 || kz < z0 || kz > z1)
                remove.Add(key);
        }
        foreach (var k in remove) UnloadChunk(k.Item1, k.Item2);

        // Load newly visible chunks
        for (int cx = x0; cx <= x1; cx++)
        for (int cz = z0; cz <= z1; cz++)
            if (!_active.ContainsKey((cx, cz)))
                LoadChunk(cx, cz);
    }

    void LoadChunk(int cx, int cz)
    {
        var arrayMesh = BuildChunkMesh(cx, cz);

        var mesh = new MeshInstance3D { Mesh = arrayMesh };
        AddChild(mesh);

        var body = new StaticBody3D();
        body.AddToGroup("terrain");
        AddChild(body);

        var col = new CollisionShape3D();
        body.AddChild(col);

        if (arrayMesh.GetSurfaceCount() > 0)
        {
            var triShape = arrayMesh.CreateTrimeshShape();
            triShape.BackfaceCollision = true;
            col.Shape = triShape;
        }

        _active[(cx, cz)] = new ChunkObjs { Mesh = mesh, Body = body, Col = col };
    }

    void UnloadChunk(int cx, int cz)
    {
        if (!_active.TryGetValue((cx, cz), out var objs)) return;
        objs.Mesh.QueueFree();
        objs.Body.QueueFree();
        _active.Remove((cx, cz));
    }

    ArrayMesh BuildChunkMesh(int cx, int cz)
    {
        int wx0 = cx * CW, wx1 = wx0 + CW;
        int wz0 = cz * CD, wz1 = wz0 + CD;

        var verts = new List<Vector3>[MatCount];
        var uvs   = new List<Vector2>[MatCount];
        var norms = new List<Vector3>[MatCount];
        var tris  = new List<int>[MatCount];
        for (int i = 0; i < MatCount; i++) { verts[i]=[]; uvs[i]=[]; norms[i]=[]; tris[i]=[]; }

        for (int x = wx0; x < wx1;   x++)
        for (int y = 0;   y < Height; y++)
        for (int z = wz0; z < wz1;   z++)
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

        return arrayMesh;
    }

    // ── Materials ─────────────────────────────────────────────────────────

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

    // ── Terrain generation ────────────────────────────────────────────────

    void GenerateTerrain()
    {
        const int SeaLevel = 22;
        SpawnPoints.Clear();

        // Base terrain — low-frequency for large-scale hills
        for (int x = 0; x < Width; x++)
        for (int z = 0; z < Depth; z++)
        {
            float nx = x * 0.025f, nz = z * 0.025f;
            float h = Mathf.Sin(nx)              * Mathf.Cos(nz)             * 10f
                    + Mathf.Sin(nx * 2.5f + 0.7f) * Mathf.Cos(nz * 2.3f)    *  5f
                    + Mathf.Sin(nx * 0.4f)         * Mathf.Cos(nz * 0.5f)    * 14f;
            int surface = Mathf.Clamp(SeaLevel + Mathf.RoundToInt(h), 4, Height - 10);

            // _data is zero-initialized (Air = 0); only write solid blocks
            for (int y = 0; y <= surface; y++)
            {
                _data[x, y, z] = y == surface    ? Grass
                                : y > surface - 4 ? Dirt
                                :                   Stone;
            }
        }

        // Flat safe landing zone around the player spawn point
        FlattenArea(22, 22, 42, 42, SeaLevel);

        // City footprint in the far corner
        const int CX0 = 300, CZ0 = 300, CX1 = 323, CZ1 = 323;
        int cityY = SurfaceAt(311, 311);
        FlattenArea(CX0, CZ0, CX1, CZ1, cityY);

        // Platforms must be placed before trees so Plank decks skip the Grass check
        BuildViewPlatform( 50,  50, "Sunrise Peak");
        BuildViewPlatform(460,  50, "East Ridge");
        BuildViewPlatform( 50, 460, "Forest Watch");
        BuildViewPlatform(255, 255, "Valley View");

        // Trees and flowers — skip spawn zone and city buffer
        for (int x = 2; x < Width - 2; x++)
        for (int z = 2; z < Depth - 2; z++)
        {
            if (x >= 18 && x <= 46 && z >= 18 && z <= 46) continue; // spawn area
            if (x >= CX0 - 3 && x <= CX1 + 3 && z >= CZ0 - 3 && z <= CZ1 + 3) continue;

            int sy = SurfaceAt(x, z);
            if (_data[x, sy, z] != Grass) continue;

            double roll = _rng.NextDouble();
            if      (roll < 0.04) PlaceTree(x, sy, z);
            else if (roll < 0.08) PlaceFlower(x, sy, z);
        }

        BuildCity(CX0, CZ0, CX1, CZ1, cityY);
    }

    void BuildViewPlatform(int cx, int cz, string name)
    {
        const int   FlatR  = 4;
        const int   SlopeR = 10;
        const float Rate   = 5f / (SlopeR - FlatR); // ~40° slope — walkable

        int groundY = SurfaceAt(cx, cz);
        int topY    = groundY + 5;

        for (int x = cx - SlopeR; x <= cx + SlopeR; x++)
        for (int z = cz - SlopeR; z <= cz + SlopeR; z++)
        {
            if ((uint)x >= Width || (uint)z >= Depth) continue;

            float dist = Mathf.Sqrt((float)((x - cx) * (x - cx) + (z - cz) * (z - cz)));
            if (dist > SlopeR) continue;

            bool isTop = dist <= FlatR;
            int  fillY = isTop
                ? topY
                : Mathf.Max(groundY, topY - (int)((dist - FlatR) * Rate));

            for (int y = 0; y < Height; y++)
                _data[x, y, z] =
                    y < fillY  ? Stone
                  : y == fillY ? (isTop ? Plank : (dist < SlopeR - 2 ? Dirt : Grass))
                  :              Air;
        }

        // Glass railing around the flat deck perimeter
        for (int d = -FlatR; d <= FlatR; d++)
        {
            Set(cx + d, topY + 1, cz - FlatR, Glass);
            Set(cx + d, topY + 1, cz + FlatR, Glass);
            Set(cx - FlatR, topY + 1, cz + d, Glass);
            Set(cx + FlatR, topY + 1, cz + d, Glass);
        }

        SpawnPoints.Add((name, new Vector3(cx, topY + 1.5f, cz)));
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
        for (int y = 0;  y < Height; y++)
        {
            _data[x, y, z] = y < targetY ? Stone : y == targetY ? Dirt : Air;
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

        PlaceBuilding(x0 + 1, groundY, z0 + 1,  8, 7, 5, Brick);
        PlaceBuilding(midX+3, groundY, z0 + 1,  7, 7, 8, Plank);
        PlaceBuilding(x0 + 1, groundY, midZ + 3, 8, 7, 6, Brick);
        PlaceBuilding(midX+3, groundY, midZ + 3, 7, 7, 9, Plank);

        int[] lpX = { midX - 6, midX + 6, midX - 6, midX + 6 };
        int[] lpZ = { midZ - 6, midZ - 6, midZ + 6, midZ + 6 };
        for (int i = 0; i < 4; i++)
        {
            for (int y = 1; y <= 4; y++) Set(lpX[i], groundY + y, lpZ[i], Wood);
            Set(lpX[i], groundY + 5, lpZ[i], Leaves);
        }
    }

    void PlaceBuilding(int bx, int by, int bz, int w, int d, int h, byte wall)
    {
        for (int x = bx; x < bx + w; x++)
        for (int z = bz; z < bz + d; z++)
        {
            bool onX      = x == bx || x == bx + w - 1;
            bool onZ      = z == bz || z == bz + d - 1;
            bool isWall   = onX || onZ;
            bool isCorner = onX && onZ;

            Set(x, by + 1, z, Plank);

            for (int y = by + 2; y <= by + h; y++)
            {
                if (y == by + h)
                {
                    Set(x, y, z, wall);
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

        int cx = pos.X / CW;
        int cz = pos.Z / CD;
        RebuildChunkIfActive(cx, cz);

        // Rebuild adjacent chunks when the block is on a boundary
        if (pos.X % CW == 0        && cx > 0)           RebuildChunkIfActive(cx - 1, cz);
        if (pos.X % CW == CW - 1   && cx < Width/CW-1)  RebuildChunkIfActive(cx + 1, cz);
        if (pos.Z % CD == 0        && cz > 0)           RebuildChunkIfActive(cx, cz - 1);
        if (pos.Z % CD == CD - 1   && cz < Depth/CD-1)  RebuildChunkIfActive(cx, cz + 1);
    }

    void RebuildChunkIfActive(int cx, int cz)
    {
        if (!_active.TryGetValue((cx, cz), out var objs)) return;

        var arrayMesh = BuildChunkMesh(cx, cz);
        objs.Mesh.Mesh = arrayMesh;

        if (arrayMesh.GetSurfaceCount() > 0)
        {
            var triShape = arrayMesh.CreateTrimeshShape();
            triShape.BackfaceCollision = true;
            objs.Col.Shape = triShape;
        }
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
