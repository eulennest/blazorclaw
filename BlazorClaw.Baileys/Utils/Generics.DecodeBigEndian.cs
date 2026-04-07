public static long DecodeBigEndian(byte[] data)
{
    long result = 0;
    for (int i = 0; i < data.Length; i++)
    {
        result <<= 8;
        result |= (data[i] & 0xFFL);
    }
    return result;
}
