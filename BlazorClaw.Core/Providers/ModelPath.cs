namespace BlazorClaw.Core.Providers
{
    public static class ModelPath
    {
        public static string Head(string path)
            => path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries)[0];

        public static string Tail(string path)
        {
            var parts = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1] : string.Empty;
        }

        public static bool TryDecompose(string path, out string head, out string tail)
        {
            head = string.Empty;
            tail = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            var parts = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            head = parts[0];
            tail = parts.Length > 1 ? parts[1] : string.Empty;
            return true;
        }
    }
}
