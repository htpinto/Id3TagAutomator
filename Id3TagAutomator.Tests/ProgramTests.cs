using Id3;
using Id3.Frames;
using Id3TagAutomator.DataObjects;
using Newtonsoft.Json;

namespace Id3TagAutomator.Tests;

public class ProgramTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramTests()
    {
        _tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Id3TagAutomatorTests_" + Guid.NewGuid())).FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static void CreateEmptyMp3(string path) => File.WriteAllBytes(path, Array.Empty<byte>());

    // ---- ResolveTargetDirectory ----

    [Fact]
    public void ResolveTargetDirectory_NoArgs_ReturnsAppBaseDirectory()
    {
        DirectoryInfo di = Program.ResolveTargetDirectory(Array.Empty<string>());

        Assert.Equal(
            new DirectoryInfo(AppContext.BaseDirectory).FullName,
            di.FullName);
    }

    [Fact]
    public void ResolveTargetDirectory_WithArg_ReturnsThatDirectory()
    {
        DirectoryInfo di = Program.ResolveTargetDirectory(new[] { _tempDir });

        Assert.Equal(new DirectoryInfo(_tempDir).FullName, di.FullName);
    }

    // ---- GetImageBytes ----

    [Fact]
    public void GetImageBytes_FileMissing_ReturnsEmptyArray()
    {
        byte[] bytes = Program.GetImageBytes(new DirectoryInfo(_tempDir), PictureType.FrontCover);

        Assert.Empty(bytes);
    }

    [Fact]
    public void GetImageBytes_FileExists_ReturnsFileContents()
    {
        byte[] expected = { 1, 2, 3, 4 };
        File.WriteAllBytes(Path.Combine(_tempDir, "FrontCover.jpg"), expected);

        byte[] bytes = Program.GetImageBytes(new DirectoryInfo(_tempDir), PictureType.FrontCover);

        Assert.Equal(expected, bytes);
    }

    // ---- LoadOrCreateAlbum ----

    [Fact]
    public void LoadOrCreateAlbum_NoAlbumJson_DerivesFromDirectoryNameAndPersistsFile()
    {
        string albumDir = Directory.CreateDirectory(Path.Combine(_tempDir, "SomeArtist-SomeAlbum")).FullName;

        Album album = Program.LoadOrCreateAlbum(new DirectoryInfo(albumDir));

        Assert.Equal("SomeArtist", album.Artist);
        Assert.Equal("SomeAlbum", album.Name);
        Assert.Equal(DateTime.Now.Year, album.Year);

        string albumJsonPath = Path.Combine(albumDir, "Album.json");
        Assert.True(File.Exists(albumJsonPath));

        Album persisted = JsonConvert.DeserializeObject<Album>(File.ReadAllText(albumJsonPath))!;
        Assert.Equal(album.Artist, persisted.Artist);
        Assert.Equal(album.Name, persisted.Name);
    }

    [Fact]
    public void LoadOrCreateAlbum_DirectoryNameWithoutDash_Throws()
    {
        string albumDir = Directory.CreateDirectory(Path.Combine(_tempDir, "NoDashHere")).FullName;

        Assert.Throws<IndexOutOfRangeException>(() => Program.LoadOrCreateAlbum(new DirectoryInfo(albumDir)));
    }

    [Fact]
    public void LoadOrCreateAlbum_AlbumJsonIsNullLiteral_ThrowsInvalidOperationException()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Album.json"), "null");

        Assert.Throws<InvalidOperationException>(() => Program.LoadOrCreateAlbum(new DirectoryInfo(_tempDir)));
    }

    [Fact]
    public void LoadOrCreateAlbum_AlbumJsonExists_ReadsFromFile()
    {
        var existing = new Album { Artist = "Existing Artist", Name = "Existing Album", Year = 1999 };
        File.WriteAllText(Path.Combine(_tempDir, "Album.json"), JsonConvert.SerializeObject(existing));

        Album album = Program.LoadOrCreateAlbum(new DirectoryInfo(_tempDir));

        Assert.Equal("Existing Artist", album.Artist);
        Assert.Equal("Existing Album", album.Name);
        Assert.Equal(1999, album.Year);
    }

    // ---- ParseTrackAndTitle ----

    [Fact]
    public void ParseTrackAndTitle_ParsesTrackNumberAndThirdSegment()
    {
        (int track, string title) = Program.ParseTrackAndTitle("03-Ignored-My Song Title");

        Assert.Equal(3, track);
        Assert.Equal("My Song Title", title);
    }

    [Fact]
    public void ParseTrackAndTitle_NonNumericTrack_Throws()
    {
        Assert.Throws<FormatException>(() => Program.ParseTrackAndTitle("aa-Ignored-Title"));
    }

    [Fact]
    public void ParseTrackAndTitle_TooFewSegments_Throws()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Program.ParseTrackAndTitle("01-OnlyTwoSegments"));
    }

    // ---- EnsureLyrics ----

    [Fact]
    public void EnsureLyrics_FileMissing_CreatesPlaceholderAndReturnsIt()
    {
        var musicFile = new FileInfo(Path.Combine(_tempDir, "01-Artist-Song.mp3"));
        CreateEmptyMp3(musicFile.FullName);

        string lyrics = Program.EnsureLyrics(new DirectoryInfo(_tempDir), musicFile);

        Assert.Contains("No Lyrics", lyrics);
        Assert.True(File.Exists(Path.Combine(_tempDir, musicFile.Name + "_lyrics.txt")));
    }

    [Fact]
    public void EnsureLyrics_FileExists_ReturnsExistingContent()
    {
        var musicFile = new FileInfo(Path.Combine(_tempDir, "01-Artist-Song.mp3"));
        CreateEmptyMp3(musicFile.FullName);
        File.WriteAllText(Path.Combine(_tempDir, musicFile.Name + "_lyrics.txt"), "Real lyrics here");

        string lyrics = Program.EnsureLyrics(new DirectoryInfo(_tempDir), musicFile);

        Assert.Equal("Real lyrics here", lyrics);
    }

    // ---- BuildTag ----

    [Fact]
    public void BuildTag_WithoutImages_SetsCoreFieldsAndNoPictures()
    {
        var album = new Album { Artist = "The Artist", Name = "The Album", Year = 2020, Genre = new GenreFrame() };

        Id3Tag tag = Program.BuildTag(album, 5, "Track Title", "Some Lyrics", Array.Empty<byte>(), Array.Empty<byte>());

        Assert.Equal("2020 The Album", tag.Copyright.Value);
        Assert.Contains("The Artist", tag.Artists.Value);
        Assert.Equal("The Artist", tag.Band.Value);
        Assert.Equal(DateTime.Now.Year, tag.Year.Value.GetValueOrDefault());
        Assert.Equal("The Album", tag.Album.Value);
        Assert.Equal(5, tag.Track.Value);
        Assert.Equal("Track Title", tag.Title.Value);
        Assert.Single(tag.Lyrics);
        Assert.Equal("Some Lyrics", tag.Lyrics[0].Lyrics);
        Assert.Empty(tag.Pictures);
    }

    [Fact]
    public void BuildTag_WithFrontCoverOnly_AddsFrontCoverPicture()
    {
        var album = new Album { Artist = "A", Name = "B", Year = 2021 };
        byte[] cover = { 9, 9, 9 };

        Id3Tag tag = Program.BuildTag(album, 1, "T", "L", cover, Array.Empty<byte>());

        PictureFrame picture = Assert.Single(tag.Pictures);
        Assert.Equal(PictureType.FrontCover, picture.PictureType);
        Assert.Equal(cover, picture.PictureData);
    }

    [Fact]
    public void BuildTag_WithBandLogoOnly_AddsBandLogoPicture()
    {
        var album = new Album { Artist = "A", Name = "B", Year = 2021 };
        byte[] logo = { 7, 7, 7 };

        Id3Tag tag = Program.BuildTag(album, 1, "T", "L", Array.Empty<byte>(), logo);

        PictureFrame picture = Assert.Single(tag.Pictures);
        Assert.Equal(PictureType.BandOrArtistLogotype, picture.PictureType);
        Assert.Equal(logo, picture.PictureData);
    }

    [Fact]
    public void BuildTag_WithBothImages_AddsBothPictures()
    {
        var album = new Album { Artist = "A", Name = "B", Year = 2021 };

        Id3Tag tag = Program.BuildTag(album, 1, "T", "L", new byte[] { 1 }, new byte[] { 2 });

        Assert.Equal(2, tag.Pictures.Count);
    }

    // ---- ListAll ----

    [Fact]
    public void ListAll_NoTagPresent_PrintsNothingAndDoesNotThrow()
    {
        CreateEmptyMp3(Path.Combine(_tempDir, "01-Artist-Song.mp3"));

        var writer = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Program.ListAll(new DirectoryInfo(_tempDir));
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public void ListAll_TagPresent_PrintsTitleArtistAlbum()
    {
        string mp3Path = Path.Combine(_tempDir, "01-Artist-Song.mp3");
        CreateEmptyMp3(mp3Path);
        var album = new Album { Artist = "Artist", Name = "Album", Year = 2020 };
        Id3Tag tag = Program.BuildTag(album, 1, "Song", "Lyrics", Array.Empty<byte>(), Array.Empty<byte>());
        using (var mp3 = new Mp3(new FileInfo(mp3Path), Mp3Permissions.ReadWrite))
        {
            mp3.WriteTag(tag, Id3Version.V23);
        }

        var writer = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Program.ListAll(new DirectoryInfo(_tempDir));
        }
        finally
        {
            Console.SetOut(original);
        }

        string output = writer.ToString();
        Assert.Contains("Title: Song", output);
        Assert.Contains("Artist:", output);
        Assert.Contains("Album: Album", output);
    }

    // ---- ChangeAll ----

    [Fact]
    public void ChangeAll_WritesTagsForEveryMp3UsingAlbumAndImages()
    {
        string albumDir = Directory.CreateDirectory(Path.Combine(_tempDir, "MyArtist-MyAlbum")).FullName;
        CreateEmptyMp3(Path.Combine(albumDir, "01-Ignored-First Song.mp3"));
        CreateEmptyMp3(Path.Combine(albumDir, "02-Ignored-Second Song.mp3"));
        File.WriteAllBytes(Path.Combine(albumDir, "FrontCover.jpg"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(albumDir, "BandOrArtistLogotype.jpg"), new byte[] { 4, 5, 6 });

        var di = new DirectoryInfo(albumDir);
        Program.ChangeAll(di);

        Assert.True(File.Exists(Path.Combine(albumDir, "Album.json")));

        foreach (var (fileName, expectedTitle, expectedTrack) in new[]
                 {
                     ("01-Ignored-First Song.mp3", "First Song", 1),
                     ("02-Ignored-Second Song.mp3", "Second Song", 2),
                 })
        {
            using var mp3 = new Mp3(new FileInfo(Path.Combine(albumDir, fileName)));
            Id3Tag tag = mp3.GetTag(Id3TagFamily.Version2X);
            Assert.NotNull(tag);
            Assert.Equal(expectedTitle, tag!.Title.Value);
            Assert.Equal(expectedTrack, tag.Track.Value);
            Assert.Equal("MyAlbum", tag.Album.Value);
            Assert.Equal(2, tag.Pictures.Count);

            Assert.True(File.Exists(Path.Combine(albumDir, fileName + "_lyrics.txt")));
        }
    }

    [Fact]
    public void ChangeAll_NoMp3Files_StillLoadsOrCreatesAlbum()
    {
        string albumDir = Directory.CreateDirectory(Path.Combine(_tempDir, "SoloArtist-EmptyAlbum")).FullName;

        Program.ChangeAll(new DirectoryInfo(albumDir));

        Assert.True(File.Exists(Path.Combine(albumDir, "Album.json")));
    }

    // ---- Main (full pipeline) ----

    [Fact]
    public void Main_ValidAlbumDirectory_TagsAllTracksAndPrintsBeforeAfterSuccess()
    {
        string albumDir = Directory.CreateDirectory(Path.Combine(_tempDir, "MainArtist-MainAlbum")).FullName;
        CreateEmptyMp3(Path.Combine(albumDir, "01-Ignored-Only Song.mp3"));

        var writer = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Program.Main(new[] { albumDir });
        }
        finally
        {
            Console.SetOut(original);
        }

        string output = writer.ToString();
        Assert.Contains("Before:", output);
        Assert.Contains("Title: Only Song", output);
        Assert.Contains("Success!", output);
    }

    [Fact]
    public void Main_NonExistentDirectory_PrintsMustExistMessageThenThrows()
    {
        string missingDir = Path.Combine(_tempDir, "DoesNotExist");

        var writer = new StringWriter();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        try
        {
            Assert.Throws<DirectoryNotFoundException>(() => Program.Main(new[] { missingDir }));
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("must exists", writer.ToString());
    }
}
