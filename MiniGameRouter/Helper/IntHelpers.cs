namespace MiniGameRouter.Helper;

public static unsafe class IntHelpers
{
    public static ulong RotateLeft(this ulong original, int bits)
    {
        return (original << bits) | (original >> (64 - bits));
    }

    public static ulong RotateRight(this ulong original, int bits)
    {
        return (original >> bits) | (original << (64 - bits));
    }

    public static ulong GetUInt64(this byte[] bb, int pos)
    {
        // we only read aligned longs, so a simple casting is enough
        fixed (byte* pByte = &bb[pos])
        {
            return *(ulong*)pByte;
        }
    }
}