using InvitationPlatform.Api.Services.Media;
using InvitationPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace InvitationPlatform.Tests;

public class MediaValidationTests
{
    [Theory]
    [InlineData("image/png", MediaKind.Image)]
    [InlineData("image/jpeg", MediaKind.Image)]
    [InlineData("image/webp", MediaKind.Image)]
    [InlineData("video/mp4", MediaKind.Video)]
    [InlineData("audio/mpeg", MediaKind.Audio)]
    [InlineData("audio/mp4; codecs=mp4a", MediaKind.Audio)]   // parameters tolerated
    public void ResolveKind_accepts_whitelisted_types(string ct, MediaKind expected)
        => Assert.Equal(expected, MediaValidation.ResolveKind(ct));

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")]   // SVG excluded on purpose (XSS vector)
    [InlineData("")]
    [InlineData(null)]
    public void ResolveKind_rejects_everything_else(string? ct)
        => Assert.Null(MediaValidation.ResolveKind(ct));

    [Fact]
    public void Validate_rejects_oversize_image()
    {
        var opt = new MediaOptions { MaxImageBytes = 100 };
        var error = MediaValidation.Validate("image/png", 101, opt, out _);
        Assert.NotNull(error);
        Assert.Contains("too large", error!);
    }

    [Fact]
    public void Validate_accepts_within_limit()
    {
        var error = MediaValidation.Validate("image/png", 50, new MediaOptions { MaxImageBytes = 100 }, out var kind);
        Assert.Null(error);
        Assert.Equal(MediaKind.Image, kind);
    }
}

public class MediaServiceTests
{
    private static byte[] Png(int w = 16, int h = 10)
    {
        using var img = new Image<Rgba32>(w, h);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Upload_image_is_re_encoded_to_webp_and_stored()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var storage = new FakeFileStorage();
        var svc = TestSupport.NewMediaService(db, storage);

        var png = Png();
        var dto = await svc.UploadAsync(inv.Id, new MemoryStream(png), "cover.png", "image/png", png.Length);

        Assert.Equal("Image", dto.Kind);
        Assert.Equal("image/webp", dto.ContentType);          // optimised
        Assert.StartsWith("/api/public/media/", dto.Url);
        Assert.Equal(16, dto.Width);
        Assert.Equal(10, dto.Height);
        Assert.Equal(1, storage.Count);
        Assert.Equal(1, await db.UserMedia.CountAsync());
    }

    [Fact]
    public async Task Upload_same_bytes_twice_is_deduplicated()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var storage = new FakeFileStorage();
        var svc = TestSupport.NewMediaService(db, storage);
        var png = Png();

        var a = await svc.UploadAsync(inv.Id, new MemoryStream(png), "a.png", "image/png", png.Length);
        var b = await svc.UploadAsync(inv.Id, new MemoryStream(png), "b.png", "image/png", png.Length);

        Assert.Equal(a.Id, b.Id);                              // same row reused
        Assert.Equal(1, await db.UserMedia.CountAsync());
        Assert.Equal(1, storage.Count);
    }

    [Fact]
    public async Task Delete_removes_row_and_file()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var storage = new FakeFileStorage();
        var svc = TestSupport.NewMediaService(db, storage);
        var png = Png();
        var dto = await svc.UploadAsync(inv.Id, new MemoryStream(png), "a.png", "image/png", png.Length);

        var ok = await svc.DeleteAsync(inv.Id, dto.Id);

        Assert.True(ok);
        Assert.Equal(0, await db.UserMedia.CountAsync());
        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public async Task Delete_is_scoped_to_the_owning_invitation()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db, "wedding-a");
        var other = TestSupport.SeedInvitation(db, "wedding-b");
        var svc = TestSupport.NewMediaService(db, new FakeFileStorage());
        var png = Png();
        var dto = await svc.UploadAsync(inv.Id, new MemoryStream(png), "a.png", "image/png", png.Length);

        // Another invitation cannot delete this media.
        Assert.False(await svc.DeleteAsync(other.Id, dto.Id));
        Assert.Equal(1, await db.UserMedia.CountAsync());
    }

    [Fact]
    public async Task Upload_audio_is_stored_as_is()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var svc = TestSupport.NewMediaService(db, new FakeFileStorage());
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        var dto = await svc.UploadAsync(inv.Id, new MemoryStream(bytes), "song.mp3", "audio/mpeg", bytes.Length);

        Assert.Equal("Audio", dto.Kind);
        Assert.Equal("audio/mpeg", dto.ContentType);
        Assert.Null(dto.Width);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_type()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var svc = TestSupport.NewMediaService(db, new FakeFileStorage());
        var bytes = new byte[] { 1, 2, 3 };

        await Assert.ThrowsAsync<MediaException>(() =>
            svc.UploadAsync(inv.Id, new MemoryStream(bytes), "x.txt", "text/plain", bytes.Length));
    }

    [Fact]
    public async Task Upload_rejects_oversize()
    {
        using var db = TestSupport.NewDb();
        var inv = TestSupport.SeedInvitation(db);
        var svc = TestSupport.NewMediaService(db, new FakeFileStorage());
        var png = Png();
        // Claim a length beyond the 8 MB image cap → rejected before any processing.
        await Assert.ThrowsAsync<MediaException>(() =>
            svc.UploadAsync(inv.Id, new MemoryStream(png), "big.png", "image/png", 9L * 1024 * 1024));
    }
}
