using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;
using System.IO;
using System.Threading;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using System.Linq;
using System.Windows;

namespace WordsToolkit.Common
{
    public class Storage
    {
        //private CancellationTokenSource cts;

        private static List<object> _data = new List<object>();
 
        public static List<object> Data
        {
            get { return _data; }
            set { _data = value; }
        }
 
        public static StorageFile file { get; set; }
 
        static async public Task Save<T>(string filename)
        {
            if (await DoesFileExistAsync(filename))
            {
                   await Windows.System.Threading.ThreadPool.RunAsync((sender) => Storage.SaveAsync<T>(filename).Wait(), Windows.System.Threading.WorkItemPriority.Normal); 
            }
            else
            {
                file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename,CreationCollisionOption.ReplaceExisting);
            }
        }
 
        static async public Task Restore<T>(string filename)
        {
            if (await DoesFileExistAsync(filename))
            {
                await Windows.System.Threading.ThreadPool.RunAsync((sender) => Storage.RestoreAsync<T>(filename).Wait(), Windows.System.Threading.WorkItemPriority.Normal);
            }
            else
            {
                file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename);
            }
        }
 
        static async Task<bool> DoesFileExistAsync(string fileName) 
        {
            try
            {
                await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                return true;
            } 
            catch
            {
                return false;
            }
        }
 
        static async private Task SaveAsync<T>(string filename)
        {
                StorageFile sessionFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                IRandomAccessStream sessionRandomAccess = await sessionFile.OpenAsync(FileAccessMode.ReadWrite);
                IOutputStream sessionOutputStream = sessionRandomAccess.GetOutputStreamAt(0);
                var serializer = new XmlSerializer(typeof (List<object>), new Type[] {typeof (T)});

                serializer.Serialize(sessionOutputStream.AsStreamForWrite(), _data);
                sessionRandomAccess.Dispose();
                await sessionOutputStream.FlushAsync();
                sessionOutputStream.Dispose();
        }

        static async private Task RestoreAsync<T>(string filename)
        {
            StorageFile sessionFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.OpenIfExists);
            if (sessionFile == null)
            {
                return;
            }
            IInputStream sessionInputStream = await sessionFile.OpenReadAsync();
 
            var serializer = new XmlSerializer(typeof(List<object>), new Type[] { typeof(T) });
            _data = (List<object>) serializer.Deserialize(sessionInputStream.AsStreamForRead());
            sessionInputStream.Dispose();
        }

        private void SetObjectList()
        {
            if (Storage.Data != null)
            {
                foreach (var item in Storage.Data)
                {
                    _objects.Add(item as Phrase);
                }
            }
        }

        private readonly ObservableCollection<Phrase> _objects = new ObservableCollection<Phrase>();

        public ObservableCollection<Phrase> Objects
        {
            get { return _objects; }
        }

        [System.Obsolete("use method: ReadBingDictPhraseList",true)]
        public async void GetPhraseList(string filename)
        {

            await Restore<Phrase>(filename);
            SetObjectList();
        }

        #region Read PhraseList from xml file which under local folder

        /// <summary>
        /// Win8 - 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public List<Phrase> ReadBingDictPhraseList(string filename)
        {
            _objects.Clear();

            List<Phrase> phrases = XElement.Load(filename).Descendants("Phrase").Select(
                p => new Phrase()
                {
                    Eng = p.Element("Eng").Value,
                    Phonetic = p.Element("Phonetic").Value,
                    Defi = p.Element("Defi").Value,
                    Note = p.Element("Note").Value,
                    Date = p.Element("Date").Value
                }).ToList();

            foreach (var p in phrases)
            {

                _objects.Add(p);
            }

            return phrases;
        }

        /// <summary>
        /// WinPhone
        /// </summary>
        /// <param name="sf"></param>
        /// <returns></returns>
        public async Task<List<Phrase>> ReadBingDictPhraseList(StorageFile sf)
        {
            _objects.Clear();

            var xmlfile = await sf.OpenAsync(FileAccessMode.Read);
            Stream stream = xmlfile.AsStreamForRead();

            List<Phrase> phrases = XElement.Load(stream).Descendants("Phrase").Select(
                p => new Phrase()
                {
                    Eng = p.Element("Eng").Value,
                    Phonetic = p.Element("Phonetic").Value,
                    Defi = p.Element("Defi").Value,
                    Note = p.Element("Note").Value,
                    Date = p.Element("Date").Value
                }).ToList();

            foreach (var p in phrases)
            {

                _objects.Add(p);
            }

            return phrases;
        }

        #endregion

        public async void addPhrase(string xmlFileName, Phrase p)
        {
            using (Stream phrase = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(xmlFileName, CreationCollisionOption.OpenIfExists))
            {
                phrase.Position = 0;
                var xmlDoc = XDocument.Load(phrase);

                XElement myPhrase = new XElement("Phrase", new XElement("Eng", p.Eng), 
                    new XElement("Phonetic",p.Phonetic),
                    new XElement("Defi",p.Defi),
                    new XElement("Date",p.Date),
                    new XElement("Note", p.Note));
                xmlDoc.Descendants("Phrases").FirstOrDefault().Add(myPhrase);

                //For XDocument, just 1 element named "FCVocaPhraseList"
                var result = xmlDoc.Nodes().ToList()[0].ToString().Contains(p.Eng);
                if (!result)
                {
                    xmlDoc.Descendants("Phrases").FirstOrDefault().Add(myPhrase);
                }

                phrase.Seek(0, SeekOrigin.Begin);
                phrase.SetLength(phrase.Position);
                xmlDoc.Save(phrase);
                
            }
        }

        public async void addPhrase(string xmlFileName, List<Phrase> plist)
        {
            using (Stream phrase = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(xmlFileName, CreationCollisionOption.OpenIfExists))
            {
                phrase.Position = 0;
                var xmlDoc = XDocument.Load(phrase);

                foreach (Phrase p in plist)
                {
                    XElement myPhrase = new XElement("Phrase", new XElement("Eng", p.Eng),
                        new XElement("Phonetic", p.Phonetic),
                        new XElement("Defi", p.Defi),
                        new XElement("Date", p.Date),
                        new XElement("Note", p.Note));

                    //For XDocument, just 1 element named "FCVocaPhraseList"
                    var result = xmlDoc.Nodes().ToList()[0].ToString().Contains(p.Eng);
                    if (!result)
                    {
                        xmlDoc.Descendants("Phrases").FirstOrDefault().Add(myPhrase);
                    }
                }
                phrase.Seek(0, SeekOrigin.Begin);
                phrase.SetLength(phrase.Position);
                xmlDoc.Save(phrase);
            }
        }

        public async void removePhrase(string xmlFileName, List<Phrase> plist)
        {
            using (Stream phrase = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(xmlFileName, CreationCollisionOption.OpenIfExists))
            {
                phrase.Position = 0;
                var xmlDoc = XDocument.Load(phrase);

                foreach (Phrase pr in plist)
                {
                    xmlDoc.Descendants("Phrase").Where(p => p.Element("Eng").Value.Equals(pr.Eng)).Remove();
                }

                phrase.Seek(0, SeekOrigin.Begin);
                phrase.SetLength(phrase.Position);
                xmlDoc.Save(phrase);
            }
        }

        static void updatePhrase(Phrase oldp, Phrase newp)
        {
            //addPhrase(newp);
            //removePhrase(new List<Phrase> { oldp });
        }

    }
}
