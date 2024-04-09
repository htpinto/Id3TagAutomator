using Id3.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Id3TagAutomator.DataObjects
{
    public class Album
    {
        public string Artist { get; set; }
        public string Name { get; set; }

        public int Year { get; set; }
        public GenreFrame Genre { get; internal set; }
    }
}
