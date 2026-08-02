using Godot;
using System.Collections.Generic;

namespace Mindani.World;

public partial class VoxelWorld : Node3D
{
    // ── Dimensions ────────────────────────────────────────────────────────
    public const int Height   = 80;   // taller for mountain peaks
    public const int SeaLevel = 22;
    const int CW         = 32;        // chunk width  X
    const int CD         = 32;        // chunk depth  Z
    const int RenderDist =  3;        // 7×7 = 49 active chunks

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
    public const byte Water  = 11;

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
    const int MatWater     = 11;
    const int MatCount     = 12;

    // ── Chunk system ──────────────────────────────────────────────────────
    // Data lives forever once generated; meshes are streamed in/out by distance.
    readonly Dictionary<(int, int), byte[,,]> _chunkData = new();

    struct ChunkObjs { public MeshInstance3D Mesh; public StaticBody3D Body; public CollisionShape3D Col; }
    readonly Dictionary<(int, int), ChunkObjs> _active = new();

    (int cx, int cz) _lastChunk = (-999, -999);
    CharacterBody3D? _player;

    readonly StandardMaterial3D[] _mats = new StandardMaterial3D[MatCount];
    public static readonly List<(string Name, Vector3 Position)> SpawnPoints = new();

    // ── Noise ─────────────────────────────────────────────────────────────
    FastNoiseLite _hillNoise     = null!;
    FastNoiseLite _mountainNoise = null!;
    FastNoiseLite _riverNoise    = null!;

    // ── Faces ─────────────────────────────────────────────────────────────
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

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public override void _Ready()
    {
        LoadMaterials();
        InitNoise();
        PlaceStructures();

        _player = GetNodeOrNull<CharacterBody3D>("/root/Main/Lindani");

        // Spawn is at world (256, 24, 256) → chunk (8, 8)
        _lastChunk = (256 / CW, 256 / CD);
        UpdateChunks(_lastChunk.cx, _lastChunk.cz);
    }

    public override void _Process(double _delta)
    {
        if (_player == null) return;
        int cx = FloorDiv((int)_player.GlobalPosition.X, CW);
        int cz = FloorDiv((int)_player.GlobalPosition.Z, CD);
        if ((cx, cz) == _lastChunk) return;
        _lastChunk = (cx, cz);
        UpdateChunks(cx, cz);
    }

    // ── Noise setup ───────────────────────────────────────────────────────

    void InitNoise()
    {
        // Smooth rolling hills — 6-octave FBM, medium frequency
        _hillNoise = new FastNoiseLite
        {
            Seed              = 42,
            NoiseType         = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency         = 0.004f,
            FractalType       = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves    = 6,
            FractalLacunarity = 2.0f,
            FractalGain       = 0.5f,
        };

        // Large-scale mountain ranges — low frequency, fewer octaves
        _mountainNoise = new FastNoiseLite
        {
            Seed           = 137,
            NoiseType      = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency      = 0.0018f,
            FractalType    = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
        };

        // River paths — single-octave, used for zero-crossing bands
        _riverNoise = new FastNoiseLite
        {
            Seed      = 999,
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.003f,
        };
    }

    // ── Terrain formula ───────────────────────────────────────────────────

    (int surface, bool isRiver, float mtnFactor) ComputeColumn(int wx, int wz)
    {
        float hill = _hillNoise.GetNoise2D(wx, wz);     // −1..1  smooth hills
        float mtn  = _mountainNoise.GetNoise2D(wx, wz); // −1..1  mountain presence
        float rv   = _riverNoise.GetNoise2D(wx, wz);    // −1..1  river winding path

        // Gentle hills: ±10 blocks (much smoother than the old sin/cos approach)
        int surface = SeaLevel + (int)(hill * 10f);

        // Mountains: only rise where noise exceeds threshold; quadratic for
        // gradual foothills that steepen toward the peak
        float mtnFactor = Mathf.Max(0f, (mtn - 0.05f) / 0.95f); // 0–1
        surface += (int)(mtnFactor * mtnFactor * 50f);           // up to +50 blocks

        surface = Mathf.Clamp(surface, 3, Height - 5);

        // Rivers: narrow bands around zero-crossings of river noise.
        // Bank shaping: terrain gradually lowers as we approach the channel.
        float riverBand = 1.0f - Mathf.Abs(rv);
        float bankT     = Mathf.Clamp((riverBand - 0.80f) / 0.15f, 0f, 1f);

        if (bankT > 0f && mtnFactor < 0.15f)
        {
            int excess = surface - (SeaLevel + 3);
            if (excess > 0)
                surface -= (int)(excess * bankT * 0.75f); // slope bank down toward river
        }

        bool isRiver = riverBand > 0.95f && mtnFactor < 0.15f;
        if (isRiver) surface = SeaLevel - 2;

        return (surface, isRiver, mtnFactor);
    }

    // ── Chunk data generation ─────────────────────────────────────────────

    void GenerateChunkData(int cx, int cz)
    {
        if (_chunkData.ContainsKey((cx, cz))) return;

        var data = new byte[CW, Height, CD];
        _chunkData[(cx, cz)] = data; // register before filling to prevent re-entry

        int wx0 = cx * CW, wz0 = cz * CD;

        // Per-chunk seeded RNG so tree/flower layout is deterministic per chunk
        var rng = new System.Random(cx * 73856093 ^ cz * 19349663 ^ 42);

        for (int lx = 0; lx < CW; lx++)
        for (int lz = 0; lz < CD; lz++)
        {
            int wx = wx0 + lx, wz = wz0 + lz;
            var (surface, isRiver, _) = ComputeColumn(wx, wz);

            // Flat safe landing pad around the player spawn at (256, 24, 256)
            if (wx >= 246 && wx <= 266 && wz >= 246 && wz <= 266)
                { surface = SeaLevel; isRiver = false; }

            // Fill solid terrain
            for (int y = 0; y <= surface; y++)
                data[lx, y, lz] = y == surface    ? (isRiver ? Sand : Grass)
                                 : y > surface - 4 ? Dirt
                                 :                   Stone;

            // Fill river channel with water up to sea level
            if (isRiver)
                for (int y = surface + 1; y <= SeaLevel; y++)
                    data[lx, y, lz] = Water;

            // Trees and flowers — kept ≥2 blocks inset so leaf crowns stay in chunk
            if (!isRiver && surface < Height - 9
                && lx >= 2 && lx < CW - 2 && lz >= 2 && lz < CD - 2)
            {
                double roll = rng.NextDouble();
                if (roll < 0.04)
                    PlaceTreeLocal(data, lx, lz, surface);
                else if (roll < 0.08 && surface + 1 < Height)
                    data[lx, surface + 1, lz] = Flower;
            }
        }
    }

    // Trees are fully contained within the chunk (trunk ≥2 inset, crown ±2)
    void PlaceTreeLocal(byte[,,] data, int lx, int lz, int sy)
    {
        int h = 4 + (lx * 3 + lz * 7) % 3; // 4, 5, or 6 — deterministic per position

        for (int t = 1; t <= h && sy + t < Height; t++)
            data[lx, sy + t, lz] = Wood;

        int cy = sy + h;
        for (int dlx = -2; dlx <= 2; dlx++)
        for (int dly = -1; dly <= 2; dly++)
        for (int dlz = -2; dlz <= 2; dlz++)
        {
            if (dlx == 0 && dlz == 0 && dly < 1) continue;
            if (Mathf.Sqrt(dlx*dlx + dly*dly*0.6f + dlz*dlz) <= 2.1f)
            {
                int nx = lx + dlx, ny = cy + dly, nz = lz + dlz;
                if ((uint)nx < CW && (uint)ny < Height && (uint)nz < CD
                    && data[nx, ny, nz] == Air)
                    data[nx, ny, nz] = Leaves;
            }
        }
    }

    // ── Structure placement ───────────────────────────────────────────────

    void PlaceStructures()
    {
        SpawnPoints.Clear();

        // Pre-generate chunks that structures touch (city + 4 platforms)
        int[][] regions =
        [
            [50,  50,  120, 120], // Sunrise Peak area
            [400, 50,  470, 120], // East Ridge area
            [50,  400, 120, 470], // Forest Watch area
            [400, 400, 470, 470], // Valley View area
            [370, 370, 415, 415], // City area
        ];
        foreach (var r in regions)
            for (int cx2 = FloorDiv(r[0], CW) - 1; cx2 <= FloorDiv(r[2], CW) + 1; cx2++)
            for (int cz2 = FloorDiv(r[1], CD) - 1; cz2 <= FloorDiv(r[3], CD) + 1; cz2++)
                GenerateChunkData(cx2, cz2);

        // City
        const int CX0 = 383, CZ0 = 383, CX1 = 406, CZ1 = 406;
        int cityY = SurfaceAt(394, 394);
        FlattenArea(CX0, CZ0, CX1, CZ1, cityY);
        BuildCity(CX0, CZ0, CX1, CZ1, cityY);

        // Platforms in each quadrant corner
        BuildViewPlatform( 80,  80, "Sunrise Peak");
        BuildViewPlatform(440,  80, "East Ridge");
        BuildViewPlatform( 80, 440, "Forest Watch");
        BuildViewPlatform(440, 440, "Valley View");
    }

    void FlattenArea(int wx0, int wz0, int wx1, int wz1, int targetY)
    {
        for (int wx = wx0; wx <= wx1; wx++)
        for (int wz = wz0; wz <= wz1; wz++)
        for (int y  = 0;   y  <  Height; y++)
            SetWorldBlock(wx, y, wz, y < targetY ? Stone : y == targetY ? Dirt : Air);
    }

    void BuildViewPlatform(int cx, int cz, string name)
    {
        const int   FlatR  = 4;
        const int   SlopeR = 10;
        const float Rate   = 5f / (SlopeR - FlatR);

        int groundY = SurfaceAt(cx, cz);
        int topY    = groundY + 5;

        for (int wx = cx - SlopeR; wx <= cx + SlopeR; wx++)
        for (int wz = cz - SlopeR; wz <= cz + SlopeR; wz++)
        {
            float dist = Mathf.Sqrt((float)((wx-cx)*(wx-cx) + (wz-cz)*(wz-cz)));
            if (dist > SlopeR) continue;

            bool isTop = dist <= FlatR;
            int  fillY = isTop
                ? topY
                : Mathf.Max(groundY, topY - (int)((dist - FlatR) * Rate));

            for (int y = 0; y < Height; y++)
                SetWorldBlock(wx, y, wz,
                    y < fillY  ? Stone
                  : y == fillY ? (isTop ? Plank : (dist < SlopeR - 2 ? Dirt : Grass))
                  :              Air);
        }

        for (int d = -FlatR; d <= FlatR; d++)
        {
            SetWorldBlock(cx + d, topY + 1, cz - FlatR, Glass);
            SetWorldBlock(cx + d, topY + 1, cz + FlatR, Glass);
            SetWorldBlock(cx - FlatR, topY + 1, cz + d, Glass);
            SetWorldBlock(cx + FlatR, topY + 1, cz + d, Glass);
        }

        SpawnPoints.Add((name, new Vector3(cx, topY + 1.5f, cz)));
    }

    int SurfaceAt(int wx, int wz)
    {
        int cx = FloorDiv(wx, CW), cz = FloorDiv(wz, CD);
        if (!_chunkData.TryGetValue((cx, cz), out var data)) return SeaLevel;
        int lx = wx - cx * CW, lz = wz - cz * CD;
        for (int y = Height - 1; y >= 0; y--)
            if (data[lx, y, lz] != Air) return y;
        return 0;
    }

    void BuildCity(int x0, int z0, int x1, int z1, int groundY)
    {
        int midX = (x0 + x1) / 2, midZ = (z0 + z1) / 2;

        for (int wx = x0; wx <= x1; wx++)
        {
            SetWorldBlock(wx, groundY, midZ - 1, Stone);
            SetWorldBlock(wx, groundY, midZ,     Stone);
            SetWorldBlock(wx, groundY, midZ + 1, Stone);
        }
        for (int wz = z0; wz <= z1; wz++)
        {
            SetWorldBlock(midX - 1, groundY, wz, Stone);
            SetWorldBlock(midX,     groundY, wz, Stone);
            SetWorldBlock(midX + 1, groundY, wz, Stone);
        }

        PlaceBuilding(x0 + 1, groundY, z0 + 1,  8, 7, 5, Brick);
        PlaceBuilding(midX+3, groundY, z0 + 1,  7, 7, 8, Plank);
        PlaceBuilding(x0 + 1, groundY, midZ + 3, 8, 7, 6, Brick);
        PlaceBuilding(midX+3, groundY, midZ + 3, 7, 7, 9, Plank);

        int[] lpX = { midX-6, midX+6, midX-6, midX+6 };
        int[] lpZ = { midZ-6, midZ-6, midZ+6, midZ+6 };
        for (int i = 0; i < 4; i++)
        {
            for (int y = 1; y <= 4; y++) SetWorldBlock(lpX[i], groundY + y, lpZ[i], Wood);
            SetWorldBlock(lpX[i], groundY + 5, lpZ[i], Leaves);
        }
    }

    void PlaceBuilding(int bx, int by, int bz, int w, int d, int h, byte wall)
    {
        for (int x = bx; x < bx + w; x++)
        for (int z = bz; z < bz + d; z++)
        {
            bool onX = x == bx || x == bx + w - 1;
            bool onZ = z == bz || z == bz + d - 1;
            bool isCorner = onX && onZ;

            SetWorldBlock(x, by + 1, z, Plank);

            for (int y = by + 2; y <= by + h; y++)
            {
                if (y == by + h) { SetWorldBlock(x, y, z, wall); continue; }
                if (!onX && !onZ) continue;
                bool win = !isCorner
                         && (y - by - 2) % 3 == 1
                         && (onX ? (z - bz) % 3 == 1 : (x - bx) % 3 == 1);
                SetWorldBlock(x, y, z, win ? Glass : wall);
            }
        }
    }

    // ── Block access ──────────────────────────────────────────────────────

    public byte GetBlock(int wx, int wy, int wz)
    {
        if ((uint)wy >= Height) return Air;
        int cx = FloorDiv(wx, CW), cz = FloorDiv(wz, CD);
        if (!_chunkData.TryGetValue((cx, cz), out var data)) return Air;
        return data[wx - cx * CW, wy, wz - cz * CD];
    }

    void SetWorldBlock(int wx, int wy, int wz, byte block)
    {
        if ((uint)wy >= Height) return;
        int cx = FloorDiv(wx, CW), cz = FloorDiv(wz, CD);
        if (!_chunkData.TryGetValue((cx, cz), out var data)) return;
        data[wx - cx * CW, wy, wz - cz * CD] = block;
    }

    public void SetBlock(Vector3I pos, byte block)
    {
        SetWorldBlock(pos.X, pos.Y, pos.Z, block);

        int cx = FloorDiv(pos.X, CW), cz = FloorDiv(pos.Z, CD);
        RebuildChunkIfActive(cx, cz);

        int lx = pos.X - cx * CW, lz = pos.Z - cz * CD;
        if (lx == 0)      RebuildChunkIfActive(cx - 1, cz);
        if (lx == CW - 1) RebuildChunkIfActive(cx + 1, cz);
        if (lz == 0)      RebuildChunkIfActive(cx, cz - 1);
        if (lz == CD - 1) RebuildChunkIfActive(cx, cz + 1);
    }

    static int FloorDiv(int a, int b)
    {
        int q = a / b;
        return (a ^ b) < 0 && q * b != a ? q - 1 : q;
    }

    // ── Chunk streaming ───────────────────────────────────────────────────

    void UpdateChunks(int pcx, int pcz)
    {
        int x0 = pcx - RenderDist, x1 = pcx + RenderDist;
        int z0 = pcz - RenderDist, z1 = pcz + RenderDist;

        var remove = new List<(int, int)>();
        foreach (var key in _active.Keys)
        {
            var (kx, kz) = key;
            if (kx < x0 || kx > x1 || kz < z0 || kz > z1)
                remove.Add(key);
        }
        foreach (var k in remove) UnloadChunk(k.Item1, k.Item2);

        for (int cx = x0; cx <= x1; cx++)
        for (int cz = z0; cz <= z1; cz++)
            if (!_active.ContainsKey((cx, cz)))
                LoadChunk(cx, cz);
    }

    void LoadChunk(int cx, int cz)
    {
        // Generate data for this chunk and all face-adjacent neighbors so boundary
        // face culling sees correct blocks in every direction.
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
            GenerateChunkData(cx + dx, cz + dz);

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
            var tri = arrayMesh.CreateTrimeshShape();
            tri.BackfaceCollision = true;
            col.Shape = tri;
        }

        _active[(cx, cz)] = new ChunkObjs { Mesh = mesh, Body = body, Col = col };

        // Rebuild neighboring active chunks whose boundary faces may have changed
        RebuildChunkIfActive(cx - 1, cz);
        RebuildChunkIfActive(cx + 1, cz);
        RebuildChunkIfActive(cx, cz - 1);
        RebuildChunkIfActive(cx, cz + 1);
    }

    void UnloadChunk(int cx, int cz)
    {
        if (!_active.TryGetValue((cx, cz), out var objs)) return;
        objs.Mesh.QueueFree();
        objs.Body.QueueFree();
        _active.Remove((cx, cz));
    }

    void RebuildChunkIfActive(int cx, int cz)
    {
        if (!_active.TryGetValue((cx, cz), out var objs)) return;
        var arrayMesh = BuildChunkMesh(cx, cz);
        objs.Mesh.Mesh = arrayMesh;
        if (arrayMesh.GetSurfaceCount() > 0)
        {
            var tri = arrayMesh.CreateTrimeshShape();
            tri.BackfaceCollision = true;
            objs.Col.Shape = tri;
        }
    }

    // ── Mesh building ─────────────────────────────────────────────────────

    ArrayMesh BuildChunkMesh(int cx, int cz)
    {
        int wx0 = cx * CW, wz0 = cz * CD;

        var verts = new List<Vector3>[MatCount];
        var uvs   = new List<Vector2>[MatCount];
        var norms = new List<Vector3>[MatCount];
        var tris  = new List<int>[MatCount];
        for (int i = 0; i < MatCount; i++) { verts[i]=[]; uvs[i]=[]; norms[i]=[]; tris[i]=[]; }

        for (int lx = 0; lx < CW;     lx++)
        for (int y  = 0; y  < Height; y++)
        for (int lz = 0; lz < CD;     lz++)
        {
            byte block = GetBlock(wx0 + lx, y, wz0 + lz);
            if (block == Air) continue;

            foreach (var (dir, norm, quad) in Faces)
            {
                byte nb = GetBlock(wx0 + lx + dir.X, y + dir.Y, wz0 + lz + dir.Z);
                // Treat Water as transparent so river beds are visible through water
                if (nb != Air && nb != Leaves && nb != Glass && nb != Water) continue;
                if (nb == block) continue;

                int m = FaceMat(block, dir.Y);
                int b = verts[m].Count;
                var origin = new Vector3(wx0 + lx, y, wz0 + lz);
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
        _mats[MatWater]     = MakeCol(new Color(0.22f, 0.52f, 0.82f));
    }

    static StandardMaterial3D MakeTex(string path) => new()
    {
        AlbedoTexture = GD.Load<Texture2D>(path),
        TextureFilter  = BaseMaterial3D.TextureFilterEnum.Nearest,
    };

    static StandardMaterial3D MakeCol(Color c) => new() { AlbedoColor = c };

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
        Water  => MatWater,
        _      => MatDirt,
    };
}
