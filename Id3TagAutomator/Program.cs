using Id3;
using Id3.Frames;

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

        private static void ChangeAll(DirectoryInfo di)
        {
            string[] artistAlbum = di.Name.Split('-');
            string artist = artistAlbum[0];
            string album = artistAlbum[1];





            var musicFiles = di.GetFiles("*.mp3");
            foreach (var musicFile in musicFiles)
            {
                string[] trackTitle = musicFile.Name[..musicFile.Name.LastIndexOf('.')].Split('-');
                string track = trackTitle[0];
                string title = trackTitle[2];


                using (var mp3 = new Mp3(musicFile, Mp3Permissions.ReadWrite))
                {
                    Id3Tag tag = new();


                    int year = DateTime.Now.Year;

                    tag.Copyright = new CopyrightFrame();
                    tag.Copyright = $"{year} {artist}";

                    tag.Artists = new ArtistsFrame();
                    tag.Artists.Value.Add(artist);
                    tag.Genre = "Alternative Rock";
                    tag.Band = artist;
                    tag.Year = DateTime.Now.Year;
                    tag.Album = album;
                    tag.Track = Convert.ToInt32(track);
                    tag.Title = title;
                    mp3.WriteTag(tag, Id3Version.V23);



                }


            }
        }
    }


}