using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace WordsToolkit.Common
{
    public class FCVocaPhraseList
    {
        public Phrases Phrases { get; set; }
    }

    public class Phrases : List<Phrase> { }
    
    public class Phrase
    {
        public string Eng { get; set; }
        public string Phonetic { get; set; }        
        public string Defi { get; set; }
        public string Date { get; set; }
        public string Note { get; set; }
    }
}
