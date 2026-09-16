using Id3;
using Id3.Frames;
using Id3TagAutomator.DataObjects;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Id3TagAutomator.Tests")]

namespace Id3TagAutomator
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            DirectoryInfo di = ResolveTargetDirectory(args);

            if (!di.Exists)
            {
                Console.WriteLine($"The directory {di.FullName} must exists.");
            }

            Console.WriteLine("Before:");
            ListAll(di);

            ChangeAll(di);
            ListAll(di);


            Console.WriteLine("Success!");
        }

        internal static DirectoryInfo ResolveTargetDirectory(string[] args)
        {
            return args.Length == 0
                ? new DirectoryInfo(AppContext.BaseDirectory)
                : new DirectoryInfo(args[0]);
        }

        internal static void ListAll(DirectoryInfo di)
        {
            var musicFiles = di.GetFiles("*.mp3");
            foreach (var musicFile in musicFiles)
            {

                using (var mp3 = new Mp3(musicFile))
                {
                    Id3Tag tag = mp3.GetTag(Id3TagFamily.Version2X);
                    if (tag != null)
                    {
                        Console.WriteLine("Title: {0}", tag.Title);
                        Console.WriteLine("Artist: {0}", tag.Artists);
                        Console.WriteLine("Album: {0}", tag.Album);
                    }

                }


            }
        }

        internal static byte[] GetImageBytes(DirectoryInfo di, PictureType type)
        {
            var imgPath = Path.Combine(di.FullName, $"{type}.jpg");

            byte[] pictureData = Array.Empty<byte>();
            if (File.Exists(imgPath))
            {
                pictureData = File.ReadAllBytes(imgPath);
            }

            return pictureData;
        }

        internal static Album LoadOrCreateAlbum(DirectoryInfo di)
        {
            Album album;

            FileInfo fi = new FileInfo(Path.Combine(di.FullName, "Album.json"));
            if (!fi.Exists)
            {
                string[] artistAlbum = di.Name.Split('-');
                album = new Album()
                {
                    Artist = artistAlbum[0],
                    Name = artistAlbum[1],
                    Year = DateTime.Now.Year,
                };

                using (StreamWriter sw = new StreamWriter(fi.FullName))
                {
                    sw.WriteLine(JsonConvert.SerializeObject(album));
                }
            }
            else
            {
                using (StreamReader sr = new StreamReader(fi.FullName))
                {
                    album = JsonConvert.DeserializeObject<Album>(sr.ReadToEnd())
                        ?? throw new InvalidOperationException($"Could not parse '{fi.FullName}'.");
                }
            }

            return album;
        }

        internal static (int Track, string Title) ParseTrackAndTitle(string fileNameWithoutExtension)
        {
            string[] trackTitle = fileNameWithoutExtension.Split('-');
            return (Convert.ToInt32(trackTitle[0]), trackTitle[2]);
        }

        internal static string EnsureLyrics(DirectoryInfo di, FileInfo musicFile)
        {
            FileInfo lyricsFile = new FileInfo(Path.Combine(di.FullName, musicFile.Name + "_lyrics.txt"));
            if (!File.Exists(lyricsFile.FullName))
            {
                using (StreamWriter sw = new StreamWriter(lyricsFile.FullName))
                {
                    sw.WriteLine("No Lyrics");
                }
            }

            using (StreamReader sr = new StreamReader(lyricsFile.FullName))
            {
                return sr.ReadToEnd();
            }
        }

        internal static Id3Tag BuildTag(Album album, int track, string title, string lyrics, byte[] frontCover, byte[] bandOrArtistLogotype)
        {
            Id3Tag tag = new();

            tag.Copyright = new CopyrightFrame();
            tag.Copyright = $"{album.Year} {album.Name}";

            tag.Artists = new ArtistsFrame();
            tag.Artists.Value.Add(album.Artist);
            tag.Genre = album.Genre;
            tag.Band = album.Artist;
            tag.Year = DateTime.Now.Year;
            tag.Album = album.Name;
            tag.Track = track;
            tag.Title = title;

            tag.Lyrics.Add(new LyricsFrame()
            {
                Lyrics = lyrics,
                EncodingType = Id3TextEncoding.Unicode,
                Description = title,
                Language = Id3Language.eng
            });

            if (frontCover.Length > 0)
            {
                tag.Pictures.Add(new PictureFrame()
                {
                    Description = album.Name,
                    MimeType = "image/jpeg",
                    PictureData = frontCover,
                    PictureType = PictureType.FrontCover
                });
            }

            if (bandOrArtistLogotype.Length > 0)
            {
                tag.Pictures.Add(new PictureFrame()
                {
                    Description = album.Artist,
                    MimeType = "image/jpeg",
                    PictureData = bandOrArtistLogotype,
                    PictureType = PictureType.BandOrArtistLogotype
                });
            }

            return tag;
        }

        internal static void ChangeAll(DirectoryInfo di)
        {
            Album album = LoadOrCreateAlbum(di);

            byte[] frontCover = GetImageBytes(di, PictureType.FrontCover);
            byte[] bandOrArtistLogotype = GetImageBytes(di, PictureType.BandOrArtistLogotype);

            var musicFiles = di.GetFiles("*.mp3");
            foreach (var musicFile in musicFiles)
            {
                string nameWithoutExtension = musicFile.Name[..musicFile.Name.LastIndexOf('.')];
                (int track, string title) = ParseTrackAndTitle(nameWithoutExtension);

                string lyrics = EnsureLyrics(di, musicFile);

                Id3Tag tag = BuildTag(album, track, title, lyrics, frontCover, bandOrArtistLogotype);

                using (var mp3 = new Mp3(musicFile, Mp3Permissions.ReadWrite))
                {
                    mp3.WriteTag(tag, Id3Version.V23);
                }
            }
        }
    }
}
