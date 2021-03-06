﻿using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using AnyListen.Music;
using AnyListen.Music.Data;
using AnyListen.Settings;
using AnyListen.Settings.RegistryManager;
using AnyListen.Settings.Themes;
using AnyListen.Settings.Themes.Background;
using AnyListen.Settings.Themes.Visual;
using AnyListen.ViewModelBase;
using CSCore.SoundOut;
using Microsoft.Win32;

// ReSharper disable ExplicitCallerInfoArgument

namespace AnyListen.ViewModels
{
    class SettingsViewModel : PropertyChangedBase
    {
        #region "Singleton & Constructor"
        private static SettingsViewModel _instance;
        public static SettingsViewModel Instance => _instance ?? (_instance = new SettingsViewModel());

        private SettingsViewModel()
        {
            RegistryManager = new RegistryManager(); //important for shortcut
        }

        public void Load()
        {
            SoundOutList = MusicManager.CSCoreEngine.SoundOutManager.SoundOutList;
            SelectedSoundOut = SoundOutList.First(x => x.SoundOutMode == Config.SoundOutMode);
            CurrentLanguage = Config.Languages.First(x => x.Code == Config.Language);
        }

        public MusicManager MusicManager => MainViewModel.Instance.MusicManager;
        public ApplicationThemeManager ApplicationThemeManager => ApplicationThemeManager.Instance;
        public RegistryManager RegistryManager { get; set; }

        public ConfigSettings Config => AnyListenSettings.Instance.Config;

        public MainWindow BaseWindow => (MainWindow)Application.Current.MainWindow;

        #endregion

        private int _selectedtab;
        public int SelectedTab
        {
            get { return _selectedtab; }
            set
            {
                SetProperty(value, ref _selectedtab);
                switch (value)
                {
                    case 2:
                        OnPropertyChanged("MusicManager");
                        break;
                }
            }
        }

        #region Playback

        private ObservableCollection<SoundOutRepresenter> _soundOutList;
        public ObservableCollection<SoundOutRepresenter> SoundOutList
        {
            get { return _soundOutList; }
            set
            {
                SetProperty(value, ref _soundOutList);
            }
        }

        
        private int _selectedAudioDeviceIndex;
        public int SelectedAudioDeviceIndex
        {
            get { return _selectedAudioDeviceIndex; }
            set
            {
                SetProperty(value, ref _selectedAudioDeviceIndex);
            }
        }

        private AudioDevice _selectedaudiodevice;
        public AudioDevice SelectedAudioDevice
        {
            get { return _selectedaudiodevice; }
            set
            {
                if (SetProperty(value, ref _selectedaudiodevice)) OnPropertyChanged("CanApplySoundOut");
                if (value == null && SelectedSoundOut.AudioDevices.Count > 0)
                    SelectedAudioDevice = SelectedSoundOut.AudioDevices.First();
            }
        }

        private SoundOutRepresenter _selectedSoundOut;
        public SoundOutRepresenter SelectedSoundOut
        {
            get { return _selectedSoundOut; }
            set
            {
                if (SetProperty(value, ref _selectedSoundOut) && value != null)
                {
                    OnPropertyChanged("CanApplySoundOut");

                    var device = value.AudioDevices.FirstOrDefault(x => x.ID == Config.SoundOutDeviceID) ??
                                 value.AudioDevices.FirstOrDefault(x => x.IsDefault);
                    SelectedAudioDeviceIndex = device == null ? 0 : value.AudioDevices.IndexOf(device);
                }
            }
        }

        private RelayCommand _applySoundOut;
        public RelayCommand ApplySoundOut
        {
            get
            {
                return _applySoundOut ?? (_applySoundOut = new RelayCommand(parameter =>
                {
                    if (!CanApplySoundOut) return;
                    Config.SoundOutMode = SelectedSoundOut.SoundOutMode;
                    Config.SoundOutDeviceID = SelectedAudioDevice.ID;
                    MusicManager.CSCoreEngine.Refresh();
                    OnPropertyChanged("CanApplySoundOut");
                }));
            }
        }

        public bool CanApplySoundOut
        {
            get
            {
                if (SelectedAudioDevice == null || SelectedSoundOut == null) return false;
                return Config.SoundOutDeviceID != SelectedAudioDevice.ID || Config.SoundOutMode != SelectedSoundOut.SoundOutMode;
            }
        }

        #endregion

        #region Apperance

        private RelayCommand _openDesigner;
        public RelayCommand OpenDesigner
        {
            get
            {
                return _openDesigner ?? (_openDesigner = new RelayCommand(parameter =>
                {
                    Process.Start(Assembly.GetExecutingAssembly().Location, "/designer");
                }));
            }
        }

        public bool ShowArtistAndTitle
        {
            get { return Config.ShowArtistAndTitle; }
            set
            {
                Config.ShowArtistAndTitle = value;
                MusicManager.SelectedPlaylist.ViewSource.Refresh();
            }
        }

        public IAppTheme SelectedAppTheme
        {
            get { return Config.ApplicationDesign.AppTheme; }
            set
            {
                Config.ApplicationDesign.AppTheme = value;
                ApplyTheme();
            }
        }

        public IAccentColor SelectedAccentColor
        {
            get { return Config.ApplicationDesign.AccentColor; }
            set
            {
                Config.ApplicationDesign.AccentColor = value;
                ApplyTheme();
            }
        }

        private async void ApplyTheme()
        {
            await BaseWindow.MoveOut();
            ApplicationThemeManager.Instance.Apply(Config.ApplicationDesign);
            await BaseWindow.ResetAndMoveIn();
        }

        private RelayCommand _selectBackground;
        public RelayCommand SelectBackground
        {
            get
            {
                return _selectBackground ?? (_selectBackground = new RelayCommand(async parameter =>
                {
                    var ofd = new OpenFileDialog
                    {
                        Filter = string.Format("{0}|{4};{5}|{1}|{4}|{2}|{5}|{3}|*.*",
                            Application.Current.Resources["SupportedFiles"],
                            Application.Current.Resources["PictureFiles"],
                            Application.Current.Resources["VideoFiles"],
                            Application.Current.Resources["AllFiles"],
                            "*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.gif",
                            "*.mp4;*.wmv")
                    };

                    if (ofd.ShowDialog() == true)
                    {
                        Config.ApplicationDesign.ApplicationBackground = new CustomApplicationBackground { BackgroundPath = ofd.FileName };
                        await BaseWindow.BackgroundChanged();
                    }
                }));
            }
        }

        private RelayCommand _removeBackground;
        public RelayCommand RemoveBackground
        {
            get
            {
                return _removeBackground ?? (_removeBackground = new RelayCommand(async parameter =>
                {
                    Config.ApplicationDesign.ApplicationBackground = null;
                    await BaseWindow.BackgroundChanged();
                }));
            }
        }

        #endregion

        #region Behaviour

        public bool ShowProgressInTaskbar
        {
            get { return Config.ShowProgressInTaskbar; }
            set
            {
                Config.ShowProgressInTaskbar = value;
                BaseWindow.RefreshTaskbarInfo(MainViewModel.Instance.MusicManager.CSCoreEngine.IsPlaying ? PlaybackState.Playing : PlaybackState.Paused);
            }
        }

        #endregion

        #region Languages

        private LanguageInfo _currentlanguage;
        public LanguageInfo CurrentLanguage
        {
            get { return _currentlanguage; }
            set
            {
                if (SetProperty(value, ref _currentlanguage) && value != null)
                {
                    Config.Language = value.Code;
                    Config.LoadLanguage();
                }
            }
        }

        #endregion

        #region Reset

        public bool SaveSettingsToAppData
        {
            get { return SaveLocationManager.IsInstalled(); }
            set
            {
                if (value)
                {
                    SaveLocationManager.MoveToAppData(BaseWindow.WindowDialogService);
                }
                else
                {
                    SaveLocationManager.MoveToLocalFoler(BaseWindow.WindowDialogService);
                }
            }
        }

        private RelayCommand _switchLocation;
        public RelayCommand SwitchLocation
        {
            get { return _switchLocation ?? (_switchLocation = new RelayCommand(parameter => { SaveSettingsToAppData = !SaveSettingsToAppData; })); }
        }

        #endregion

        private RelayCommand _testnotification;
        public RelayCommand TestNotification
        {
            get
            {
                return _testnotification ?? (_testnotification = new RelayCommand(parameter => { MusicManager.Notification.Test(Config.Notification); }));
            }
        }

        private RelayCommand _resettrackimport;
        public RelayCommand ResetTrackImport
        {
            get
            {
                return _resettrackimport ?? (_resettrackimport = new RelayCommand(parameter =>
                {
                    Config.RememberTrackImportPlaylist = false;
                    Config.PlaylistToImportTrack = null;
                    OnPropertyChanged("Config");
                }));
            }
        }

        private RelayCommand _totalreset;
        public RelayCommand TotalReset
        {
            get
            {
                return _totalreset ?? (_totalreset = new RelayCommand(parameter =>
                {
                    Config.SetStandardValues();
                    SelectedAudioDevice = SoundOutList[0].AudioDevices[0];
                    OnPropertyChanged("Config");
                }));
            }
        }
    }
}