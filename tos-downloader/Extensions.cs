namespace tos_downloader;

public static class Extensions
{
    public static IEnumerable<List<T>> Chunk<T>(this List<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }
}
