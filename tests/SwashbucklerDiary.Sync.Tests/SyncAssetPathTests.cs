using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using Xunit;

namespace SwashbucklerDiary.Sync.Tests
{
    public class SyncAssetPathTests
    {
        [Fact]
        public void TryParse_ValidKey()
        {
            Assert.True(SyncAssetPath.TryParse("Image/abc123.jpg", out var kind, out var relative));
            Assert.Equal(MediaResource.Image, kind);
            Assert.Equal("abc123.jpg", relative);
        }

        [Fact]
        public void TryParse_RejectsTraversal()
        {
            Assert.False(SyncAssetPath.TryParse("Image/../../evil.jpg", out _, out _));
        }

        [Fact]
        public void TryParse_RejectsUnknownKind()
        {
            Assert.False(SyncAssetPath.TryParse("Unknown/abc.jpg", out _, out _));
        }

        [Fact]
        public void TryParse_RejectsMissingRelative()
        {
            Assert.False(SyncAssetPath.TryParse("Image/", out _, out _));
        }

        [Fact]
        public void ToRemotePath_PrefixesAssets()
        {
            Assert.Equal("assets/Image/abc.jpg", SyncAssetPath.ToRemotePath("Image/abc.jpg"));
        }

        [Fact]
        public void TryGetHashFromFileName_Sha256Name()
        {
            string name = new string('a', 64) + ".jpg";

            Assert.True(SyncAssetPath.TryGetHashFromFileName(name, out string hash));
            Assert.Equal(new string('a', 64), hash);
        }

        [Fact]
        public void TryGetHashFromFileName_OriginalName()
        {
            Assert.False(SyncAssetPath.TryGetHashFromFileName("photo.jpg", out _));
        }

        [Theory]
        [InlineData("Image//etc/passwd")]
        [InlineData("Image/\\server\\share\\x")]
        [InlineData("Image/C:\\temp\\x")]
        [InlineData("Image/.")]
        [InlineData("Image/a/../b")]
        public void TryParse_RejectsUnsafeRelative(string key)
        {
            Assert.False(SyncAssetPath.TryParse(key, out _, out _));
        }
    }
}
