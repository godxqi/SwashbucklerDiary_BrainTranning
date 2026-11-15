using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 附件同步键与路径的互相转换。键格式：<c>{MediaResource}/{相对路径}</c>，
    /// 例如 <c>Image/abc123.jpg</c>；远端路径是 <c>assets/{键}</c>。
    /// </summary>
    public static class SyncAssetPath
    {
        public const string RemotePrefix = "assets/";

        public static string ToRemotePath(string bareKey) => RemotePrefix + bareKey;

        public static bool TryParse(string bareKey, out MediaResource kind, out string relative)
        {
            kind = MediaResource.Unknown;
            relative = string.Empty;

            int index = bareKey.IndexOf('/');
            if (index <= 0 || index == bareKey.Length - 1)
            {
                return false;
            }

            if (!Enum.TryParse(bareKey[..index], ignoreCase: true, out kind)
                || kind == MediaResource.Unknown)
            {
                return false;
            }

            relative = bareKey[(index + 1)..];

            // 远端键同样是外部输入。逐段校验，拒绝：
            // - 空段（Image//x 会让 Path.Combine 丢弃前缀）
            // - .. / .
            // - 反斜杠与冒号（Windows 上可构造 UNC / 盘符绝对路径）
            foreach (string segment in relative.Split('/'))
            {
                if (segment.Length == 0
                    || segment is "." or ".."
                    || segment.Contains('\\', StringComparison.Ordinal)
                    || segment.Contains(':', StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public static string? ResolveLocal(IMediaResourceManager manager, string bareKey)
        {
            if (!TryParse(bareKey, out var kind, out var relative))
            {
                return null;
            }

            if (!manager.MediaResourceDirectoryPaths.TryGetValue(kind, out string? directory)
                || string.IsNullOrEmpty(directory))
            {
                return null;
            }

            return Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string ToBareKey(MediaResource kind, string directory, string filePath)
        {
            string relative = Path.GetRelativePath(directory, filePath).Replace('\\', '/');
            return $"{kind}/{relative}";
        }

        /// <summary>
        /// 附件默认按内容 SHA256 命名时，文件名本身就是哈希，不必读整个文件。
        /// </summary>
        public static bool TryGetHashFromFileName(string filePath, out string hash)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            if (name.Length == 64 && name.All(Uri.IsHexDigit))
            {
                hash = name.ToLowerInvariant();
                return true;
            }

            hash = string.Empty;
            return false;
        }
    }
}
