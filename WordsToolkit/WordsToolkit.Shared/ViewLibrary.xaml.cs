using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Web;
using WordsToolkit.Common;
using System.Linq;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace WordsToolkit
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
#if WINDOWS_PHONE_APP
    public sealed partial class ViewLibrary : Page, IDisposable, IFileOpenPickerContinuable
#else 
    public sealed partial class ViewLibrary : Page, IDisposable
#endif
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        public static MainPage rootPage = MainPage.Current;

        private static string fileName = "mylist.xml";

        private CancellationTokenSource cts;

        public WordsToolkit.Common.Storage storage = new Storage();

        private const int maxUploadFileSize = 100 * 1024 * 1024; // 100 MB (arbitrary limit chosen for this sample)

        private readonly ObservableCollection<Phrase> _objects = new ObservableCollection<Phrase>();

        public ObservableCollection<Phrase> Objects
        {
            get { return _objects; }
        }

        public async void LoadPhraseList() {

            try
            {
                IReadOnlyList<StorageFile> storageFileList = await ApplicationData.Current.LocalFolder.GetFilesAsync();
                StorageFile storageFile = (from StorageFile s in storageFileList
                                           where s.Name == fileName
                                           select s).FirstOrDefault();
                if (storageFile != null)
                {

#if WINDOWS_PHONE_APP
                    await storage.ReadBingDictPhraseList(storageFile);
#else
                    storage.ReadBingDictPhraseList(storageFile.Path);
#endif
                    PhraseList.ItemsSource = storage.Objects;
                }
                else
                {
                    // File does not exist.
                    Trace.LogStatus(rootPage, "No File, Please Import", NotifyType.ErrorMessage);
                }
            }
            catch (Exception ex) {

                Trace.LogStatus(rootPage, ex.Message,NotifyType.ErrorMessage);
            }
        }

        public ViewLibrary()
        {
            cts = new CancellationTokenSource();
            this.InitializeComponent();

            // obsolate for phrases
            //storage.GetPhraseList("mylist.xml");

            LoadPhraseList();
        }

        /// <summary>
        /// Import the xml file which export from Bing Dictionary
        /// Merge the new phrases if the mylist already exists
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnImportPhraselist_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");

            picker.CommitButtonText = "Select Files";
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

#if WINDOWS_PHONE_APP
            //Method ContinueFileOpenPicker will processed while the file selected
            picker.PickSingleFileAndContinue();
#else
            SaveSingleFileToLocalFolder(picker);
#endif
        }

        #region Upload/Download network - remove this feature from UI currently

        private void btnUploadWords_Click(object sender, RoutedEventArgs e)
        {
            // Validating the URI is required since it was received from an untrusted source (user input).
            // The URI is validated by calling Uri.TryCreate() that will return 'false' for strings that are not valid URIs.
            // Note that when enabling the text box users may provide URIs to machines on the intrAnet that require
            // the "Home or Work Networking" capability.
            Uri uri;
            if (!Uri.TryCreate(serverAddressField.Text.Trim(), UriKind.Absolute, out uri))
            {
                rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return;
            }

            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");

#if WINDOWS_PHONE_APP
            picker.ContinuationData.Add("uri", uri.OriginalString);
            picker.PickSingleFileAndContinue();
#else
            StartSingleFileUpload(picker, uri);
#endif
        }

        private void btnDownloadWords_Click(object sender, RoutedEventArgs e)
        {
#if WINDOWS_PHONE_APP
#else
            StartDownload(@"http://localhost/BackgroundTransferSample/data/复习单词-20130403.xml");
#endif
        }

#if WINDOWS_PHONE_APP

        public void ContinueFileOpenPicker(Windows.ApplicationModel.Activation.FileOpenPickerContinuationEventArgs args)
        {
            try
            {
                if (args.Files.Count == 1)
                {

                    SaveSingleFileToLocalFolder(args.Files[0]);
                    LoadPhraseList();
                }
                else
                {

                }

            }
            catch (Exception ex)
            {

                Trace.LogStatus(rootPage, ex.Message, NotifyType.ErrorMessage);
            }
        }

#else
        private async void StartSingleFileUpload(FileOpenPicker picker, Uri uri)
        {
            StorageFile file = await picker.PickSingleFileAsync();
            UploadSingleFile(uri, file);
        }

        public async void StartDownload(string serverAddress)
        {
            
                if (string.IsNullOrWhiteSpace(serverAddress)) return;
                //Select the location for file
                FileSavePicker picker = new FileSavePicker();

                picker.FileTypeChoices.Add("xml文件", new string[] { ".xml" });
                StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    BackgroundDownloader downloader = new BackgroundDownloader();
                    DownloadOperation operation = downloader.CreateDownload(new Uri(serverAddress), file);
                    
                    Progress<DownloadOperation> progressDown = new Progress<DownloadOperation>(new ViewLibrary().ProgressChanged);
                    
                    var opresult = await operation.StartAsync().AsTask(progressDown);
                    try
                    {
                        ResponseInformation response = operation.GetResponseInformation();

                        Trace.LogStatus(rootPage, String.Format(CultureInfo.CurrentCulture, "Completed: {0}, Status Code: {1}", operation.Guid,
                            response.StatusCode), NotifyType.StatusMessage);
                    }
                    catch (TaskCanceledException)
                    {
                        Trace.LogStatus(rootPage, "Canceled: " + operation.Guid, NotifyType.StatusMessage);
                    }
                    catch (Exception ex)
                    {
                        if (!IsExceptionHandled("Error", ex, operation))
                        {
                            throw;
                        } 
                    }
                }
           
        }
#endif

        private async void UploadSingleFile(Uri uri, StorageFile file)
        {
            if (file == null)
            {
                rootPage.NotifyUser("No file selected.", NotifyType.ErrorMessage);
                return;
            }

            BasicProperties properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > maxUploadFileSize)
            {
                rootPage.NotifyUser(String.Format(CultureInfo.CurrentCulture,
                    "Selected file exceeds max. upload file size ({0} MB).", maxUploadFileSize / (1024 * 1024)),
                    NotifyType.ErrorMessage);
                return;
            }

            BackgroundUploader uploader = new BackgroundUploader();
            uploader.SetRequestHeader("Filename", file.Name);

            UploadOperation upload = uploader.CreateUpload(uri, file);
            Trace.Log(String.Format(CultureInfo.CurrentCulture, "Uploading {0} to {1}, {2}", file.Name, uri.AbsoluteUri,
                upload.Guid));

            // Attach progress and completion handlers.
            await HandleUploadAsync(upload, true);
        }

        private async Task HandleUploadAsync(UploadOperation upload, bool start)
        {
            try
            {
                Trace.LogStatus(rootPage, "Running: " + upload.Guid, NotifyType.StatusMessage);

                Progress<UploadOperation> progressCallback = new Progress<UploadOperation>(UploadProgress);
                if (start)
                {
                    // Start the upload and attach a progress handler.
                    await upload.StartAsync().AsTask(cts.Token, progressCallback);
                }
                else
                {
                    // The upload was already running when the application started, re-attach the progress handler.
                    await upload.AttachAsync().AsTask(cts.Token, progressCallback);
                }

                ResponseInformation response = upload.GetResponseInformation();

                Trace.LogStatus(rootPage, String.Format(CultureInfo.CurrentCulture, "Completed: {0}, Status Code: {1}", upload.Guid,
                    response.StatusCode), NotifyType.StatusMessage);
            }
            catch (TaskCanceledException)
            {
                Trace.LogStatus(rootPage, "Canceled: " + upload.Guid, NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                if (!IsExceptionHandled("Error", ex, upload))
                {
                    throw;
                }
            }
        }

        // Note that this event is invoked on a background thread, so we cannot access the UI directly.
        private void UploadProgress(UploadOperation upload)
        {
            MarshalLog(String.Format(CultureInfo.CurrentCulture, "Progress: {0}, Status: {1}", upload.Guid,
                upload.Progress.Status));

            BackgroundUploadProgress progress = upload.Progress;

            double percentSent = 100;
            if (progress.TotalBytesToSend > 0)
            {
                percentSent = progress.BytesSent * 100 / progress.TotalBytesToSend;
            }

            MarshalLog(String.Format(CultureInfo.CurrentCulture,
                " - Sent bytes: {0} of {1} ({2}%), Received bytes: {3} of {4}", progress.BytesSent,
                progress.TotalBytesToSend, percentSent, progress.BytesReceived, progress.TotalBytesToReceive));

            if (progress.HasRestarted)
            {
                MarshalLog(" - Upload restarted");
            }

            if (progress.HasResponseChanged)
            {
                // We've received new response headers from the server.
                MarshalLog(" - Response updated; Header count: " + upload.GetResponseInformation().Headers.Count);

                // If you want to stream the response data this is a good time to start.
                upload.GetResultStreamAt(0);
            }
        }

        /// <summary>
        /// Report the progress
        /// </summary>
        public async void ProgressChanged(DownloadOperation op)
        {
            var p = op.Progress;
            double t = p.TotalBytesToReceive;
            double d = p.BytesReceived;
            // Calculate the percent
            double pc = d / t * 100;
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                this.probar.Value = pc;
                this.tbMsg.Text = string.Format("已下载{0}字节/共{1}字节。", d.ToString("N0"), t.ToString("N0"));
            });
        }

        #region Log for Notification

        private bool IsExceptionHandled(string title, Exception ex, IBackgroundTransferOperation operation = null)
        {
            WebErrorStatus error = BackgroundTransferError.GetStatus(ex.HResult);
            if (error == WebErrorStatus.Unknown)
            {
                return false;
            }

            if (operation == null)
            {
                Trace.LogStatus(rootPage, String.Format(CultureInfo.CurrentCulture, "Error: {0}: {1}", title, error),
                    NotifyType.ErrorMessage);
            }
            else
            {
                Trace.LogStatus(rootPage, String.Format(CultureInfo.CurrentCulture, "Error: {0} - {1}: {2}", operation.Guid, title,
                    error), NotifyType.ErrorMessage);
            }

            return true;
        }

        /// Dispatcher - Schedules the provided callback on the UI thread from a worker thread, and returns the results asynchronously.
        /// When operations happen on a background thread we have to marshal UI updates back to the UI thread.
        private void MarshalLog(string value)
        {
            var ignore = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Trace.Log(value);
            });
        }

        #endregion

        private void StackPanel_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FrameworkElement senderElement = sender as FrameworkElement;
            FlyoutBase flyoutBase = FlyoutBase.GetAttachedFlyout(senderElement);

            flyoutBase.ShowAt(senderElement);

        }

        #endregion

#if WINDOWS_PHONE_APP
        public async void SaveSingleFileToLocalFolder(StorageFile file)
#else
        private async void SaveSingleFileToLocalFolder(FileOpenPicker picker)
#endif
        {
#if WINDOWS_APP
            var file = await picker.PickSingleFileAsync();
#endif
            //Check if the file already exists
            IReadOnlyList<StorageFile> storageFileList = await ApplicationData.Current.LocalFolder.GetFilesAsync();
            StorageFile existedfile = storageFileList.Where(x => x.Name == file.Name).FirstOrDefault();
            if (existedfile != null)
            {
                //Merge: file + local file
                List<Phrase> alist = await storage.ReadBingDictPhraseList(file);
                List<Phrase> blist = await storage.ReadBingDictPhraseList(existedfile);
                
                List<Phrase> newlist = new List<Phrase>();
                newlist.AddRange(alist);
                newlist.AddRange(blist.Where(b => !alist.Any(a => a.Eng == b.Eng)));
                
                storage.addPhrase(existedfile.Name, newlist);
                Trace.LogStatus(rootPage, "Merge successfully", NotifyType.StatusMessage);
            }
            else
            {
                await file.CopyAsync(ApplicationData.Current.LocalFolder, file.Name, NameCollisionOption.GenerateUniqueName);
                Trace.LogStatus(rootPage, "Imported successfully", NotifyType.StatusMessage);
            }
        }

        private void MenuFlyoutItem_DelPhrase_Click(object sender, RoutedEventArgs e)
        {
            if (PhraseList.SelectedItem != null)
            {
                Phrase p = (Phrase)PhraseList.SelectedItem;
                List<Phrase> plist = new List<Phrase>();
                plist.Add(p);
                storage.removePhrase(fileName, plist);
                Trace.LogStatus(rootPage,"Removed Successfully. Please flush for check.",NotifyType.StatusMessage);
            }
            else {

                Trace.LogStatus(rootPage, "No Item Selected.", NotifyType.ErrorMessage);
            }
        }

        private void MenuFlyoutItem_EditPhrase_Click(object sender, RoutedEventArgs e)
        {
            if (PhraseList.SelectedItem != null)
            {
            Phrase oldp = (Phrase)PhraseList.SelectedItem;
            }
            else
            {

                Trace.LogStatus(rootPage, "No Item Selected.", NotifyType.ErrorMessage);
            }
        }

        private void btnFlush_Click(object sender, RoutedEventArgs e)
        {
            LoadPhraseList();
            Trace.LogStatus(rootPage, "Flush Completed.", NotifyType.StatusMessage);
        }

        public void Dispose()
        {
            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
