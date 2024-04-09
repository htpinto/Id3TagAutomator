using Id3;
using Id3.Frames;
using Id3TagAutomator.DataObjects;
using Newtonsoft.Json;

namespace Id3TagAutomator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            DirectoryInfo? di = default;
            if (args.Length == 0)
            {
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                di = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation));
            }
            else
            {
                di = new DirectoryInfo(args[0]);
            }

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

        private static void ListAll(DirectoryInfo di)
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

        private static byte[] GetImageBytes(DirectoryInfo di, PictureType type)
        {
            var imgPath = Path.Combine(di.FullName, $"{type}.jpg");


            byte[] pictureData = new byte[0];
            if (File.Exists(imgPath))
            {
                pictureData = File.ReadAllBytes(imgPath);
            }

            return pictureData;
        }

        private static void ChangeAll(DirectoryInfo di)
        {


            Album? album = null;

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
                    album = JsonConvert.DeserializeObject<Album>(sr.ReadToEnd());
                }
            }


            byte[] frontCover = GetImageBytes(di, PictureType.FrontCover);
            byte[] bandOrArtistLogotype = GetImageBytes(di, PictureType.BandOrArtistLogotype);



            var musicFiles = di.GetFiles("*.mp3");
            foreach (var musicFile in musicFiles)
            {
                string[] trackTitle = musicFile.Name[..musicFile.Name.LastIndexOf('.')].Split('-');
                string track = trackTitle[0];
                string title = trackTitle[2];


                using (var mp3 = new Mp3(musicFile, Mp3Permissions.ReadWrite))
                {
                    Id3Tag tag = new();

                    tag.Copyright = new CopyrightFrame();
                    tag.Copyright = $"{album?.Year} {album?.Name}";

                    tag.Artists = new ArtistsFrame();
                    tag.Artists.Value.Add(album?.Artist);
                    tag.Genre = album?.Genre;
                    tag.Band = album?.Artist;
                    tag.Year = DateTime.Now.Year;
                    tag.Album = album?.Name;
                    tag.Track = Convert.ToInt32(track);
                    tag.Title = title;


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
                        tag.Lyrics.Add(new LyricsFrame()
                        {
                            Lyrics = sr.ReadToEnd(),
                            EncodingType = Id3TextEncoding.Unicode,
                            Description = title,
                            Language = Id3Language.eng
                        });
                    }

 
                    if (frontCover.Length > 0)
                    {
                        tag.Pictures.Add(new PictureFrame()
                        {
                            Description = album?.Name,
                            MimeType = "image/jpeg",
                            PictureData = frontCover,
                            PictureType = PictureType.FrontCover
                        });
                    }

                    if (bandOrArtistLogotype.Length > 0)
                    {
                        tag.Pictures.Add(new PictureFrame()
                        {
                            Description = album?.Artist,
                            MimeType = "image/jpeg",
                            PictureData = bandOrArtistLogotype,
                            PictureType = PictureType.BandOrArtistLogotype
                        });
                    }

                    mp3.WriteTag(tag, Id3Version.V23);



                }


            }
        }
    }


}