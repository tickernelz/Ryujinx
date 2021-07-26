using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Models;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class AmiiboWindowViewModel : BaseModel, IDisposable
    {
        private const string DEFAULT_JSON = "{ \"amiibo\": [] }";

        private readonly string _amiiboJsonPath;
        private readonly byte[] _amiiboLogoBytes;
        private readonly HttpClient _httpClient;
        private readonly StyleableWindow _owner;
        private Bitmap _amiiboImage;
        private List<Amiibo.AmiiboApi> _amiiboList;
        private AvaloniaList<Amiibo.AmiiboApi> _amiibos;

        private int _amiiboSelectedIndex;
        private AvaloniaList<string> _amiiboSeries;
        private bool _enableScanning;

        private int _seriesSelectedIndex;
        private bool _showAllAmiibo;
        private string _usage;
        private bool _useRandomUuid;

        public AmiiboWindowViewModel(StyleableWindow owner, string lastScannedAmiiboId, string titleId)
        {
            _owner = owner;
            _httpClient = new HttpClient {Timeout = TimeSpan.FromMilliseconds(5000)};
            LastScannedAmiiboId = lastScannedAmiiboId;
            TitleId = titleId;

            Directory.CreateDirectory(Path.Join(AppDataManager.BaseDirPath, "system", "amiibo"));

            _amiiboJsonPath = Path.Join(AppDataManager.BaseDirPath, "system", "amiibo", "Amiibo.json");
            _amiiboList = new List<Amiibo.AmiiboApi>();
            _amiiboSeries = new AvaloniaList<string>();
            _amiibos = new AvaloniaList<Amiibo.AmiiboApi>();

            _amiiboLogoBytes = EmbeddedResources.Read("Ryujinx.Ava/Assets/Images/Logo_Amiibo.png");

            _ = LoadContentAsync();
        }

        public string AmiiboId { get; private set; }
        public string TitleId { get; set; }
        public string LastScannedAmiiboId { get; set; }

        public UserResult Response { get; private set; }

        public bool UseRandomUuid
        {
            get => _useRandomUuid;
            set
            {
                _useRandomUuid = value;

                OnPropertyChanged();
            }
        }

        public bool ShowAllAmiibo
        {
            get => _showAllAmiibo;
            set
            {
                _showAllAmiibo = value;


                new Task(() => ParseAmiiboData()).Start();

                OnPropertyChanged();
            }
        }

        public AvaloniaList<Amiibo.AmiiboApi> AmiiboList
        {
            get => _amiibos;
            set
            {
                _amiibos = value;

                OnPropertyChanged();
            }
        }

        public AvaloniaList<string> AmiiboSeries
        {
            get => _amiiboSeries;
            set
            {
                _amiiboSeries = value;
                OnPropertyChanged();
            }
        }

        public int SeriesSelectedIndex
        {
            get => _seriesSelectedIndex;
            set
            {
                _seriesSelectedIndex = value;

                FilterAmiibo();

                OnPropertyChanged();
            }
        }

        public int AmiiboSelectedIndex
        {
            get => _amiiboSelectedIndex;
            set
            {
                _amiiboSelectedIndex = value;

                EnableScanning = _amiiboSelectedIndex >= 0 && _amiiboSelectedIndex < _amiibos.Count;

                SetAmiiboDetails();

                OnPropertyChanged();
            }
        }

        public Bitmap AmiiboImage
        {
            get => _amiiboImage;
            set
            {
                _amiiboImage = value;

                OnPropertyChanged();
            }
        }

        public string Usage
        {
            get => _usage;
            set
            {
                _usage = value;

                OnPropertyChanged();
            }
        }

        public bool EnableScanning
        {
            get => _enableScanning;
            set
            {
                _enableScanning = value;

                OnPropertyChanged();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private async Task LoadContentAsync()
        {
            string amiiboJsonString = DEFAULT_JSON;

            if (File.Exists(_amiiboJsonPath))
            {
                amiiboJsonString = File.ReadAllText(_amiiboJsonPath);

                if (await NeedsUpdate(JsonSerializer.Deserialize<Amiibo.AmiiboJson>(amiiboJsonString).LastUpdated))
                {
                    amiiboJsonString = await DownloadAmiiboJson();
                }
            }
            else
            {
                try
                {
                    amiiboJsonString = await DownloadAmiiboJson();
                }
                catch
                {
                    ShowInfoDialog();
                }
            }

            _amiiboList = JsonSerializer.Deserialize<Amiibo.AmiiboJson>(amiiboJsonString).Amiibo;
            _amiiboList = _amiiboList.OrderBy(amiibo => amiibo.AmiiboSeries).ToList();

            ParseAmiiboData();
        }

        private void ParseAmiiboData()
        {
            _amiiboSeries.Clear();
            _amiibos.Clear();

            for (int i = 0; i < _amiiboList.Count; i++)
            {
                if (!_amiiboSeries.Contains(_amiiboList[i].AmiiboSeries))
                {
                    if (!ShowAllAmiibo)
                    {
                        foreach (Amiibo.AmiiboApiGamesSwitch game in _amiiboList[i].GamesSwitch)
                        {
                            if (game != null)
                            {
                                if (game.GameId.Contains(TitleId))
                                {
                                    AmiiboSeries.Add(_amiiboList[i].AmiiboSeries);

                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        AmiiboSeries.Add(_amiiboList[i].AmiiboSeries);
                    }
                }
            }

            if (LastScannedAmiiboId != "")
            {
                SelectLastScannedAmiibo();
            }
            else
            {
                SeriesSelectedIndex = 0;
            }
        }

        private void SelectLastScannedAmiibo()
        {
            Amiibo.AmiiboApi scanned = _amiiboList.FirstOrDefault(amiibo => amiibo.GetId() == LastScannedAmiiboId);

            SeriesSelectedIndex = AmiiboSeries.IndexOf(scanned.AmiiboSeries);
            AmiiboSelectedIndex = AmiiboList.IndexOf(scanned);
        }

        private void FilterAmiibo()
        {
            _amiibos.Clear();

            if (_seriesSelectedIndex < 0)
            {
                return;
            }

            List<Amiibo.AmiiboApi> amiiboSortedList = _amiiboList
                .Where(amiibo => amiibo.AmiiboSeries == _amiiboSeries[SeriesSelectedIndex])
                .OrderBy(amiibo => amiibo.Name).ToList();

            for (int i = 0; i < amiiboSortedList.Count; i++)
            {
                if (!_amiibos.Contains(amiiboSortedList[i]))
                {
                    if (!_showAllAmiibo)
                    {
                        foreach (Amiibo.AmiiboApiGamesSwitch game in amiiboSortedList[i].GamesSwitch)
                        {
                            if (game != null)
                            {
                                if (game.GameId.Contains(TitleId))
                                {
                                    _amiibos.Add(amiiboSortedList[i]);

                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        _amiibos.Add(amiiboSortedList[i]);
                    }
                }
            }

            AmiiboSelectedIndex = 0;
        }

        private void SetAmiiboDetails()
        {
            ResetAmiiboPreview();

            Usage = string.Empty;

            if (_amiiboSelectedIndex < 0)
            {
                return;
            }

            Amiibo.AmiiboApi selected = _amiibos[_amiiboSelectedIndex];

            string imageUrl = _amiiboList.FirstOrDefault(amiibo => amiibo.Equals(selected)).Image;

            string usageString = "";

            for (int i = 0; i < _amiiboList.Count; i++)
            {
                if (_amiiboList[i].Equals(selected))
                {
                    bool writable = false;

                    foreach (Amiibo.AmiiboApiGamesSwitch item in _amiiboList[i].GamesSwitch)
                    {
                        if (item.GameId.Contains(TitleId))
                        {
                            foreach (Amiibo.AmiiboApiUsage usageItem in item.AmiiboUsage)
                            {
                                usageString += Environment.NewLine +
                                               $"- {usageItem.Usage.Replace("/", Environment.NewLine + "-")}";

                                writable = usageItem.Write;
                            }
                        }
                    }

                    if (usageString.Length == 0)
                    {
                        usageString = "Unknown.";
                    }

                    Usage = $"Usage{(writable ? " (Writable)" : "")} : {usageString}";
                }
            }

            _ = UpdateAmiiboPreview(imageUrl);
        }

        private async Task<bool> NeedsUpdate(DateTime oldLastModified)
        {
            try
            {
                HttpResponseMessage response =
                    await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, "https://amiibo.ryujinx.org/"));

                if (response.IsSuccessStatusCode)
                {
                    return response.Content.Headers.LastModified != oldLastModified;
                }

                return false;
            }
            catch
            {
                ShowInfoDialog();

                return false;
            }
        }

        private async Task<string> DownloadAmiiboJson()
        {
            HttpResponseMessage response = await _httpClient.GetAsync("https://amiibo.ryujinx.org/");

            if (response.IsSuccessStatusCode)
            {
                string amiiboJsonString = await response.Content.ReadAsStringAsync();

                using (FileStream dlcJsonStream = File.Create(_amiiboJsonPath, 4096, FileOptions.WriteThrough))
                {
                    dlcJsonStream.Write(Encoding.UTF8.GetBytes(amiiboJsonString));
                }

                return amiiboJsonString;
            }

            await ContentDialogHelper.CreateInfoDialog(_owner, "Amiibo API", "An error occured while fetching informations from the API.");

            Close();

            return DEFAULT_JSON;
        }

        private void Close()
        {
            Dispatcher.UIThread.Post(_owner.Close);
        }

        private async Task UpdateAmiiboPreview(string imageUrl)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(imageUrl);

            if (response.IsSuccessStatusCode)
            {
                byte[] amiiboPreviewBytes = await response.Content.ReadAsByteArrayAsync();
                using (MemoryStream memoryStream = new(amiiboPreviewBytes))
                {
                    Bitmap bitmap = new(memoryStream);

                    double ratio = Math.Min(350f / bitmap.Size.Width,
                        350f / bitmap.Size.Height);

                    int resizeHeight = (int)(bitmap.Size.Height * ratio);
                    int resizeWidth = (int)(bitmap.Size.Width * ratio);

                    AmiiboImage = bitmap.CreateScaledBitmap(new PixelSize(resizeWidth, resizeHeight));
                }
            }
        }

        private void ResetAmiiboPreview()
        {
            using (MemoryStream memoryStream = new(_amiiboLogoBytes))
            {
                Bitmap bitmap = new(memoryStream);

                AmiiboImage = bitmap;
            }
        }

        private async void ShowInfoDialog()
        {
            await ContentDialogHelper.CreateInfoDialog(_owner, "Amiibo API",
                "Unable to connect to Amiibo API server. The service may be down or you may need to verify your internet connection is online.");
        }
    }
}