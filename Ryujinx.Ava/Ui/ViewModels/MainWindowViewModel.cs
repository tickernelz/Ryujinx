using ARMeilleure.Translation.PTC;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem.NcaUtils;
using Ryujinx.Ava.Common;
using Ryujinx.Ava.Common.Locale;
using Ryujinx.Ava.Ui.Controls;
using Ryujinx.Ava.Ui.Windows;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.Modules;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ShaderCacheLoadingState = Ryujinx.Graphics.Gpu.Shader.ShaderCacheState;

namespace Ryujinx.Ava.Ui.ViewModels
{
    public class MainWindowViewModel : BaseModel
    {
        private readonly MainWindow _owner;
        private AvaloniaList<ApplicationData> _applications;
        private DataGridCollectionView _appsCollection;
        private string _aspectStatusText;
        private string _cacheLoadHeading;
        private string _cacheLoadStatus;
        private string _searchText;
        private string _dockedStatusText;
        private string _fifoStatusText;
        private string _gameStatusText;
        private string _gpuStatusText;
        private bool _isAmiiboRequested;
        private bool _isGameRunning;
        private bool _isLoading;
        private string _lastScannedAmiiboId;
        private int _progressMaximum;
        private int _progressValue;
        private bool _showAll;
        private bool _showLoadProgress;
        private bool _showMenuAndStatusBar = true;
        private bool _showStatusSeparator;
        private Brush _progressBarForegroundColor;
        private Brush _progressBarBackgroundColor;
        private Brush _vsyncColor;
        private byte[] _selectedIcon;
        private bool _isAppletMenuActive;
        private int _statusBarProgressMaximum;
        private int _statusBarProgressValue;

        public MainWindowViewModel(MainWindow owner) : this()
        {
            _owner = owner;
        }

        public MainWindowViewModel()
        {
            Applications   = new AvaloniaList<ApplicationData>();
            AppsCollection = new DataGridCollectionView(Applications)
            {
                Filter = Filter
            };

            AppsCollection.SortDescriptions.Add(DataGridSortDescription.FromPath("Favorite", System.ComponentModel.ListSortDirection.Descending));

            ApplicationLibrary.ApplicationCountUpdated += ApplicationLibrary_ApplicationCountUpdated;
            ApplicationLibrary.ApplicationAdded += ApplicationLibrary_ApplicationAdded;

            Ptc.PtcStateChanged -= ProgressHandler;
            Ptc.PtcStateChanged += ProgressHandler;
        }

        public string SearchText
        { 
            get => _searchText;
            set
            {
                _searchText = value;

                AppsCollection.Refresh();
            }
        }

        public DataGridCollectionView AppsCollection
        {
            get => _appsCollection;
            set
            {
                _appsCollection = value;

                OnPropertyChanged();
            }
        }

        public bool ShowFavoriteColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.FavColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.FavColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowIconColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.IconColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.IconColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool EnableNonGameRunningControls => !IsGameRunning;
        
        public bool ShowFirmwareStatus => !ShowLoadProgress;

        public bool IsGameRunning
        {
            get => _isGameRunning;
            set
            {
                _isGameRunning = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(EnableNonGameRunningControls));
                OnPropertyChanged(nameof(ShowFirmwareStatus));
            }
        }

        public bool IsAmiiboRequested
        {
            get => _isAmiiboRequested && _isGameRunning;
            set
            {
                _isAmiiboRequested = value;

                OnPropertyChanged();
            }
        }

        public bool ShowLoadProgress
        {
            get => _showLoadProgress;
            set
            {
                _showLoadProgress = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFirmwareStatus));
            }
        }

        public string GameStatusText
        {
            get => _gameStatusText;
            set
            {
                _gameStatusText = value;

                OnPropertyChanged();
            }
        }

        public string CacheLoadHeading
        {
            get => _cacheLoadHeading;
            set
            {
                _cacheLoadHeading = value;

                OnPropertyChanged();
            }
        }

        public string CacheLoadStatus
        {
            get => _cacheLoadStatus;
            set
            {
                _cacheLoadStatus = value;

                OnPropertyChanged();
            }
        }

        public Brush ProgressBarBackgroundColor
        {
            get => _progressBarBackgroundColor;
            set
            {
                _progressBarBackgroundColor = value;

                OnPropertyChanged();
            }
        }

        public Brush ProgressBarForegroundColor
        {
            get => _progressBarForegroundColor;
            set
            {
                _progressBarForegroundColor = value;

                OnPropertyChanged();
            }
        }

        public Brush VsyncColor
        {
            get => _vsyncColor;
            set
            {
                _vsyncColor = value;

                OnPropertyChanged();
            }
        }

        public byte[] SelectedIcon
        {
            get => _selectedIcon;
            set
            {
                _selectedIcon = value;

                OnPropertyChanged();
            }
        }

        public int ProgressMaximum
        {
            get => _progressMaximum;
            set
            {
                _progressMaximum = value;

                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;

                OnPropertyChanged();
            }
        }

        public int StatusBarProgressMaximum
        {
            get => _statusBarProgressMaximum;
            set
            {
                _statusBarProgressMaximum = value;

                OnPropertyChanged();
            }
        }

        public int StatusBarProgressValue
        {
            get => _statusBarProgressValue;
            set
            {
                _statusBarProgressValue = value;

                OnPropertyChanged();
            }
        }

        public string FifoStatusText
        {
            get => _fifoStatusText;
            set
            {
                _fifoStatusText = value;

                OnPropertyChanged();
            }
        }

        public string GpuStatusText
        {
            get => _gpuStatusText;
            set
            {
                _gpuStatusText = value;

                OnPropertyChanged();
            }
        }

        public string DockedStatusText
        {
            get => _dockedStatusText;
            set
            {
                _dockedStatusText = value;

                OnPropertyChanged();
            }
        }

        public string AspectRatioStatusText
        {
            get => _aspectStatusText;
            set
            {
                _aspectStatusText = value;

                OnPropertyChanged();
            }
        }

        public bool ShowStatusSeparator
        {
            get => _showStatusSeparator;
            set
            {
                _showStatusSeparator = value;

                OnPropertyChanged();
            }
        }

        public bool ShowMenuAndStatusBar
        {
            get => _showMenuAndStatusBar;
            set
            {
                _showMenuAndStatusBar = value;

                OnPropertyChanged();
            }
        }
        
        public bool IsAppletMenuActive
        {
            get => _isAppletMenuActive && EnableNonGameRunningControls;
            set
            {
                _isAppletMenuActive = value;

                OnPropertyChanged();
            }
        }

        public bool ShowTitleColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.AppColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.AppColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowDeveloperColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.DevColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.DevColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowVersionColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.VersionColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.VersionColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowTimePlayedColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.TimePlayedColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.TimePlayedColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowLastPlayedColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.LastPlayedColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.LastPlayedColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowFileExtColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.FileExtColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.FileExtColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowFileSizeColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.FileSizeColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.FileSizeColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool ShowFilePathColumn
        {
            get => ConfigurationState.Instance.Ui.GuiColumns.PathColumn;
            set
            {
                ConfigurationState.Instance.Ui.GuiColumns.PathColumn.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                _owner.UpdateGridColumns();

                OnPropertyChanged();
            }
        }

        public bool StartGamesInFullscreen
        {
            get => ConfigurationState.Instance.Ui.StartFullscreen;
            set
            {
                ConfigurationState.Instance.Ui.StartFullscreen.Value = value;

                ConfigurationState.Instance.ToFileFormat().SaveConfig(Program.ConfigurationPath);

                OnPropertyChanged();
            }
        }

        public AvaloniaList<ApplicationData> Applications
        {
            get => _applications;
            set
            {
                _applications = value;

                OnPropertyChanged();
            }
        }

        public bool IsRefreshing { get; private set; }

        public async void OpenAmiiboWindow()
        {
            if (!_isAmiiboRequested)
            {
                return;
            }

            if (_owner.AppHost.Device.System.SearchingForAmiibo(out int deviceId))
            {
                string titleId = _owner.AppHost.Device.Application.TitleIdText.ToUpper();
                AmiiboWindow window = new(_showAll, _lastScannedAmiiboId, titleId);

                await window.ShowDialog(_owner);

                if (window.IsScanned)
                {
                    _showAll = window.ViewModel.ShowAllAmiibo;
                    _lastScannedAmiiboId = window.ScannedAmiibo.GetId();

                    _owner.AppHost.Device.System.ScanAmiibo(deviceId, _lastScannedAmiiboId, window.ViewModel.UseRandomUuid);
                }
            }
        }

        public void HandleShaderProgress(Switch emulationContext)
        {
            emulationContext.Gpu.ShaderCacheStateChanged -= ProgressHandler;
            emulationContext.Gpu.ShaderCacheStateChanged += ProgressHandler;
        }

        private bool Filter(object arg)
        {
            if (arg is ApplicationData app)
            {
                return string.IsNullOrWhiteSpace(_searchText) || app.ApplicationName.ToLower().Contains(_searchText.ToLower());
            }

            return false;
        }

        private void ApplicationLibrary_ApplicationAdded(object sender, ApplicationAddedEventArgs e)
        {
            AddApplication(e.AppData);
        }

        private void ApplicationLibrary_ApplicationCountUpdated(object sender, ApplicationCountUpdatedEventArgs e)
        {
            StatusBarProgressValue = e.NumAppsLoaded;
            StatusBarProgressMaximum = e.NumAppsFound;
            LocaleManager.Instance.UpdateDynamicValue("StatusBarGamesLoaded", StatusBarProgressValue, StatusBarProgressMaximum);

            Dispatcher.UIThread.Post(() =>
            {
                if (e.NumAppsFound == 0)
                {
                    _owner.LoadProgressBar.IsVisible = false;
                }
            });
        }

        public void AddApplication(ApplicationData applicationData)
        {
            Dispatcher.UIThread.Post(() =>
            {
                Applications.Add(applicationData);
            });
        }

        public async void LoadApplications()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Applications.Clear();
                _owner.LoadProgressBar.IsVisible = true;
                StatusBarProgressMaximum = 0;
                StatusBarProgressValue = 0;
                LocaleManager.Instance.UpdateDynamicValue("StatusBarGamesLoaded", 0, 0);
            });

            ReloadGameList();
        }

        private void ReloadGameList()
        {
            if (_isLoading)
            {
                return;
            }

            _isLoading = true;

            Thread thread = new(() =>
            {
                ApplicationLibrary.LoadApplications(ConfigurationState.Instance.Ui.GameDirs, _owner.VirtualFileSystem, ConfigurationState.Instance.System.Language);

                _isLoading = false;
            }) { Name = "GUI.AppListLoadThread", Priority = ThreadPriority.AboveNormal };

            thread.Start();
        }

        public async void OpenFile()
        {
            OpenFileDialog dialog = new();
            dialog.Filters.Add(new FileDialogFilter
            {
                Name = "All Supported Formats",
                Extensions =
                {
                    "nsp",
                    "pfs0",
                    "xci",
                    "nca",
                    "nro",
                    "nso"
                }
            });
            dialog.Filters.Add(new FileDialogFilter {Name = "NSP", Extensions = {"nsp"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "PFS0", Extensions = {"pfs0"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "XCI", Extensions = {"xci"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "NCA", Extensions = {"nca"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "NRO", Extensions = {"nro"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "NSO", Extensions = {"nso"}});

            string[] files = await dialog.ShowAsync(_owner);

            if (files != null && files.Length > 0)
            {
                _owner.LoadApplication(files[0]);
            }
        }

        public async void OpenFolder()
        {
            OpenFolderDialog dialog = new();

            string folder = await dialog.ShowAsync(_owner);

            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                _owner.LoadApplication(folder);
            }
        }

        public async void TakeScreenshot()
        {
            _owner.AppHost.ScreenshotRequested = true;
        }
        
        public async void OpenMiiApplet()
        {
            string contentPath = _owner.ContentManager.GetInstalledContentPath(0x0100000000001009, StorageId.NandSystem, NcaContentType.Program);
            
            if (!string.IsNullOrWhiteSpace(contentPath))
            {
                _owner.LoadApplication(contentPath);
            }
        }

        public void OpenRyujinxFolder()
        {
            OpenHelper.OpenFolder(AppDataManager.BaseDirPath);
        }

        public void OpenLogsFolder()
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

            new DirectoryInfo(logPath).Create();

            OpenHelper.OpenFolder(logPath);
        }

        public void ToggleFullscreen()
        {
            WindowState state = _owner.WindowState;

            if (state == WindowState.FullScreen)
            {
                _owner.WindowState = WindowState.Normal;
            }
            else
            {
                _owner.WindowState = WindowState.FullScreen;
            }
        }

        public async void OpenSettings()
        {
            SettingsWindow window = new(_owner.VirtualFileSystem, _owner.ContentManager);

            await window.ShowDialog(_owner);
        }

        public async void ManageProfiles()
        {
            UserProfileWindow window = new(_owner.AccountManager, _owner.ContentManager, _owner.VirtualFileSystem);

            await window.ShowDialog(_owner);
        }

        public async void OpenAboutWindow()
        {
            AboutWindow window = new();

            await window.ShowDialog(_owner);
        }

        public void ChangeLanguage(object obj)
        {
            LocaleManager.Instance.LoadLanguage((string)obj);
        }

        private void ProgressHandler<T>(T state, int current, int total) where T : Enum
        {
            bool showLoadProgress = false;
            ProgressMaximum = total;
            ProgressValue = current;
            switch (state)
            {
                case PtcLoadingState ptcState:
                    CacheLoadStatus = $"{current} / {total}";
                    if (ptcState == PtcLoadingState.Start)
                    {
                        _owner.HideGuestRendering();
                        CacheLoadHeading = "Compiling PTC";
                    }
                    else if (ptcState == PtcLoadingState.Loaded)
                    {
                        _owner.ShowGuestRendering();
                    }
                    showLoadProgress = ptcState != PtcLoadingState.Loaded;
                    break;
                case ShaderCacheLoadingState shaderCacheState:
                    CacheLoadStatus = $"{current} / {total}";
                    if (shaderCacheState == ShaderCacheLoadingState.Start)
                    {
                        _owner.HideGuestRendering();
                        CacheLoadHeading = "Compiling shaders";
                    }
                    else if (shaderCacheState == ShaderCacheLoadingState.Loaded)
                    {
                        _owner.ShowGuestRendering();
                    }
                    showLoadProgress = shaderCacheState != ShaderCacheLoadingState.Loaded;
                    break;
                default:
                    throw new ArgumentException($"Unknown Progress Handler type {typeof(T)}");
            }

            ShowLoadProgress = showLoadProgress && IsGameRunning;
        }

        public void OpenUserSaveDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                SaveDataFilter filter = new();
                filter.SetUserId(new UserId(1, 0));
                OpenSaveDirectory(filter, data);
            }
        }

        public void OpenModsDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                string modsBasePath  = _owner.VirtualFileSystem.ModLoader.GetModsBasePath();
                string titleModsPath = _owner.VirtualFileSystem.ModLoader.GetTitleDir(modsBasePath, data.TitleId);

                OpenHelper.OpenFolder(titleModsPath);
            }
        }

        public void OpenPtcDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                string ptcDir = Path.Combine(AppDataManager.GamesDirPath, data.TitleId, "cache", "cpu");

                string mainPath   = Path.Combine(ptcDir, "0");
                string backupPath = Path.Combine(ptcDir, "1");

                if (!Directory.Exists(ptcDir))
                {
                    Directory.CreateDirectory(ptcDir);
                    Directory.CreateDirectory(mainPath);
                    Directory.CreateDirectory(backupPath);
                }

                OpenHelper.OpenFolder(ptcDir);
            }
        }

        public async void PurgePtcCache()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                DirectoryInfo mainDir   = new(Path.Combine(AppDataManager.GamesDirPath, data.TitleId, "cache", "cpu", "0"));
                DirectoryInfo backupDir = new(Path.Combine(AppDataManager.GamesDirPath, data.TitleId, "cache", "cpu", "1"));

                // FIXME: Found a way to reproduce the bold effect on the title name (fork?).
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(_owner, "Warning", $"You are about to delete the PPTC cache for :\n\n{data.TitleName}\n\nAre you sure you want to proceed?");

                List<FileInfo> cacheFiles = new();

                if (mainDir.Exists)
                {
                    cacheFiles.AddRange(mainDir.EnumerateFiles("*.cache"));
                }

                if (backupDir.Exists)
                {
                    cacheFiles.AddRange(backupDir.EnumerateFiles("*.cache"));
                }

                if (cacheFiles.Count > 0 && result == UserResult.Yes)
                {
                    foreach (FileInfo file in cacheFiles)
                    {
                        try
                        {
                            file.Delete();
                        }
                        catch (Exception e)
                        {
                            ContentDialogHelper.CreateErrorDialog(_owner, $"Error purging PPTC cache at {file.Name}: {e}");
                        }
                    }
                }
            }
        }

        public void OpenShaderCacheDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                string shaderCacheDir = Path.Combine(AppDataManager.GamesDirPath, data.TitleId, "cache", "shader");

                if (!Directory.Exists(shaderCacheDir))
                {
                    Directory.CreateDirectory(shaderCacheDir);
                }

                OpenHelper.OpenFolder(shaderCacheDir);
            }
        }

        public void SimulateWakeUpMessage()
        {
            _owner.AppHost.Device.System.SimulateWakeUpMessage();
        }

        public async void PurgeShaderCache()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                DirectoryInfo shaderCacheDir = new(Path.Combine(AppDataManager.GamesDirPath, data.TitleId, "cache", "shader"));

                // FIXME: Found a way to reproduce the bold effect on the title name (fork?).
                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(_owner, "Warning", $"You are about to delete the shader cache for :\n\n{data.TitleName}\n\nAre you sure you want to proceed?");

                List<DirectoryInfo> cacheDirectory = new();

                if (shaderCacheDir.Exists)
                {
                    cacheDirectory.AddRange(shaderCacheDir.EnumerateDirectories("*"));
                }

                if (cacheDirectory.Count > 0 && result == UserResult.Yes)
                {
                    foreach (DirectoryInfo directory in cacheDirectory)
                    {
                        try
                        {
                            directory.Delete(true);
                        }
                        catch (Exception e)
                        {
                            ContentDialogHelper.CreateErrorDialog(_owner, $"Error purging shader cache at {directory.Name}: {e}");
                        }
                    }
                }
            }
        }

        public async void CheckForUpdates()
        {
            if (Updater.CanUpdate(true, _owner))
            {
                await Updater.BeginParse(_owner, true);
            }
        }

        public async void OpenTitleUpdateManager()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                TitleUpdateWindow titleUpdateManager = new(_owner.VirtualFileSystem, data.TitleId, data.TitleName);

                await titleUpdateManager.ShowDialog(_owner);
            }
        }

        public async void OpenDlcManager()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                DlcManagerWindow dlcManager = new(_owner.VirtualFileSystem, data.TitleId, data.TitleName);

                await dlcManager.ShowDialog(_owner);
            }
        }


        public void OpenDeviceSaveDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                SaveDataFilter filter = new();
                filter.SetSaveDataType(SaveDataType.Device);
                OpenSaveDirectory(filter, data);
            }
        }

        public void OpenBcatSaveDirectory()
        {
            object selection = _owner.GameList.SelectedItem;

            if (selection != null && selection is ApplicationData data)
            {
                SaveDataFilter filter = new();
                filter.SetSaveDataType(SaveDataType.Bcat);
                OpenSaveDirectory(filter, data);
            }
        }

        private void OpenSaveDirectory(SaveDataFilter filter, ApplicationData data)
        {
            if (!ulong.TryParse(data.TitleId, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                out ulong titleIdNumber))
            {
                ContentDialogHelper.CreateErrorDialog(_owner,
                    "Ryujinx has encountered an error", "UI error: The selected game did not have a valid title ID");

                return;
            }

            Task.Run(() => ApplicationHelper.OpenSaveDir(filter, titleIdNumber, data.ControlHolder, data.TitleName));
        }

        private void ExtractLogo()
        {
            object selection = _owner.GameList.SelectedItem;
            if (selection != null && selection is ApplicationData data)
            {
                ApplicationHelper.ExtractSection(NcaSectionType.Logo, data.Path);
            }
        }

        private void ExtractRomFs()
        {
            object selection = _owner.GameList.SelectedItem;
            if (selection != null && selection is ApplicationData data)
            {
                ApplicationHelper.ExtractSection(NcaSectionType.Data, data.Path);
            }
        }

        private void ExtractExeFs()
        {
            object selection = _owner.GameList.SelectedItem;
            if (selection != null && selection is ApplicationData data)
            {
                ApplicationHelper.ExtractSection(NcaSectionType.Code, data.Path);
            }
        }

        public void CloseWindow()
        {
            _owner.Close();
        }

        private async void HandleFirmwareInstallation(string path)
        {
            try
            {
                string filename = path;

                SystemVersion firmwareVersion = _owner.ContentManager.VerifyFirmwarePackage(filename);

                string dialogTitle = $"Install Firmware {firmwareVersion.VersionString}";

                if (firmwareVersion == null)
                {
                    ContentDialogHelper.CreateErrorDialog(_owner, $"A valid system firmware was not found in {filename}.");

                    return;
                }

                SystemVersion currentVersion = _owner.ContentManager.GetCurrentFirmwareVersion();

                string dialogMessage = $"System version {firmwareVersion.VersionString} will be installed.";

                if (currentVersion != null)
                {
                    dialogMessage +=
                        $"\n\nThis will replace the current system version {currentVersion.VersionString}. ";
                }

                dialogMessage += "\n\nDo you want to continue?";

                UserResult result = await ContentDialogHelper.CreateConfirmationDialog(_owner, dialogTitle, dialogMessage);

                UpdateWaitWindow waitingDialog = ContentDialogHelper.CreateWaitingDialog(dialogTitle, "Installing firmware...");

                if (result == UserResult.Yes)
                {
                    Logger.Info?.Print(LogClass.Application, $"Installing firmware {firmwareVersion.VersionString}");

                    Thread thread = new(() =>
                    {
                        Dispatcher.UIThread.InvokeAsync(delegate
                        {
                            waitingDialog.Show();
                        });

                        try
                        {
                            _owner.ContentManager.InstallFirmware(filename);

                            Dispatcher.UIThread.InvokeAsync(async delegate
                            {
                                waitingDialog.Close();

                                string message =
                                    $"System version {firmwareVersion.VersionString} successfully installed.";

                                await ContentDialogHelper.CreateInfoDialog(_owner, dialogTitle, message, "", "Ok");
                                Logger.Info?.Print(LogClass.Application, message);
                                
                                // Purge Applet Cache.

                                DirectoryInfo miiEditorCacheFolder = new DirectoryInfo(System.IO.Path.Combine(AppDataManager.GamesDirPath, "0100000000001009", "cache"));

                                if (miiEditorCacheFolder.Exists)
                                {
                                    miiEditorCacheFolder.Delete(true);
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.UIThread.InvokeAsync(async delegate
                            {
                                waitingDialog.Close();

                                ContentDialogHelper.CreateErrorDialog(_owner, ex.Message);
                            });
                        }
                        finally
                        {
                            _owner.RefreshFirmwareStatus();
                        }
                    });

                    thread.Name = "GUI.FirmwareInstallerThread";
                    thread.Start();
                }
            }
            catch (MissingKeyException ex)
            {
                Logger.Error?.Print(LogClass.Application, ex.ToString());
                UserErrorDialog.ShowUserErrorDialog(UserError.NoKeys, _owner);
            }
            catch (Exception ex)
            {
                ContentDialogHelper.CreateErrorDialog(_owner, ex.Message);
            }
        }

        public async void InstallFirmwareFromFile()
        {
            OpenFileDialog dialog = new() {AllowMultiple = false};
            dialog.Filters.Add(new FileDialogFilter {Name = "All types", Extensions = {"xci", "zip"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "XCI", Extensions = {"xci"}});
            dialog.Filters.Add(new FileDialogFilter {Name = "ZIP", Extensions = {"zip"}});

            string[] file = await dialog.ShowAsync(_owner);

            if (file != null && file.Length > 0)
            {
                HandleFirmwareInstallation(file[0]);
            }
        }

        public async void InstallFirmwareFromFolder()
        {
            OpenFolderDialog dialog = new();

            string folder = await dialog.ShowAsync(_owner);

            if (!string.IsNullOrWhiteSpace(folder))
            {
                HandleFirmwareInstallation(folder);
            }
        }
    }
}