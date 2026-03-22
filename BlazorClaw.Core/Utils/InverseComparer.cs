namespace BlazorClaw.Core.Utils
{
    public class InverseComparer<T>(IComparer<T> comparer) : IComparer<T>
    {
        public IComparer<T> Comparer { get; private set; } = comparer;

        public int Compare(T? x, T? y)
        {
            return Comparer.Compare(y, x);
        }
    }
}
