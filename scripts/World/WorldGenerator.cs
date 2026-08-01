using Godot;

namespace Mindani.World;

[GlobalClass]
[Tool]
public partial class WorldGenerator : VoxelGeneratorScript
{
    // Block IDs — must match VoxelBlockyLibrary slot order
    const int Air   = 0;
    const int Grass = 1;
    const int Dirt  = 2;
    const int Stone = 3;
    const int Sand  = 4;

    const int SeaLevel    = 64;
    const int GroundDepth = 4;
    const int StoneDepth  = 20;

    public override int _GetUsedChannels_Mask()
        => 1 << (int)VoxelBuffer.ChannelId.Type;

    public override void _Generate(VoxelBuffer buffer, Vector3I origin, int lod)
    {
        Vector3I size = buffer.GetSize();

        for (int x = 0; x < size.X; x++)
        for (int z = 0; z < size.Z; z++)
        {
            float wx = (origin.X + x) * 0.03f;
            float wz = (origin.Z + z) * 0.03f;
            float noise = Mathf.Sin(wx) * Mathf.Cos(wz) * 6f
                        + Mathf.Sin(wx * 2.7f + 0.5f) * 2f;

            int surfaceY = SeaLevel + Mathf.RoundToInt(noise) - origin.Y;

            for (int y = 0; y < size.Y; y++)
            {
                int worldY = origin.Y + y;
                int block;

                if (worldY > surfaceY)
                    block = Air;
                else if (worldY == surfaceY)
                    block = Grass;
                else if (worldY > surfaceY - GroundDepth)
                    block = Dirt;
                else
                    block = Stone;

                buffer.SetVoxel(block, x, y, z, VoxelBuffer.ChannelId.Type);
            }
        }
    }
}
