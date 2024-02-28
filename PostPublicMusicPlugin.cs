using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MusicBeePlugin;

public partial class Plugin
{
    private MusicBeeApiInterface _mbApiInterface;
    private readonly PluginInfo _about = new();
    
    private const string PluginSettingsFileName = "post-public-music-settings.json";

    public PluginInfo Initialise(IntPtr apiInterfacePtr)
    {
        _mbApiInterface = new MusicBeeApiInterface();
        _mbApiInterface.Initialise(apiInterfacePtr);
        _about.PluginInfoVersion = PluginInfoVersion;

        _about.Name = "PostPublicMusic";
        _about.Description = "A plugin to display what music I'm listening to on my website";
        _about.Author = "Autumn";
        _about.TargetApplication = ""; // the name of a Plugin Storage device or panel header for a dockable panel
        _about.Type = PluginType.General;

        // Plugin version
        _about.VersionMajor = 1;
        _about.VersionMinor = 0;
        _about.Revision = 1;

        _about.MinInterfaceVersion = MinInterfaceVersion;
        _about.MinApiRevision = MinApiRevision;

        _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents);
        // Height in pixels that MusicBee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function
        _about.ConfigurationPanelHeight = 0;

        return _about;
    }

    // receive event notifications from MusicBee
    // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
    public void ReceiveNotification(string _, NotificationType type)
    {
        if (type is not (NotificationType.TrackChanged or NotificationType.PlayStateChanged)) return;

        var playingData = GetStatus();
        PostData(playingData);
    }

    private PlayingData GetStatus()
    {
        var artist = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
        var title = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
        var album = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
        var durationMs = _mbApiInterface.NowPlaying_GetDuration();
        var positionMs = _mbApiInterface.Player_GetPosition();
        var playState = RemapPlayState(_mbApiInterface.Player_GetPlayState());

        var albumArt = _mbApiInterface.NowPlaying_GetArtwork();
        albumArt = ResizeAlbumArt(albumArt, maxWidth: 300);

        var playingData = new PlayingData
        {
            Artist = artist,
            Title = title,
            Album = album,
            DurationMs = durationMs,
            PositionMs = positionMs,
            PlayState = playState,
            AlbumArt = albumArt
        };
        return playingData;
    }

    private void PostData(object playingData)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };
        
        var settings = GetSettings();

        using var client = new System.Net.WebClient();
        client.Headers.Add("Content-Type", "application/json");
        client.Headers.Add("Authorization", $"Basic {settings.ApiKey}");
        client.Encoding = Encoding.UTF8;
        client.UploadString(settings.ServerUrl, "POST", JsonConvert.SerializeObject(playingData, serializerSettings));
    }

    private static string ResizeAlbumArt(string albumArt, int maxWidth)
    {
        if (albumArt is not { Length: > 0 }) return albumArt;

        using var image = Image.FromStream(new System.IO.MemoryStream(Convert.FromBase64String(albumArt)));

        var newWidth = Math.Min(image.Width, maxWidth);
        var newHeight = (int)(image.Height * (newWidth / (double)image.Width)); // scale the height to match the new width

        using var resizedImage = new Bitmap(image, newWidth, newHeight);

        using var memoryStream = new System.IO.MemoryStream();
        resizedImage.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        albumArt = Convert.ToBase64String(memoryStream.ToArray());

        return albumArt;
    }

    private PluginSettings GetSettings()
    {
        var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
        var filePath = System.IO.Path.Combine(dataPath, PluginSettingsFileName); 
        var settings = JsonConvert.DeserializeObject<PluginSettings>(System.IO.File.ReadAllText(filePath));
        return settings;
    }
    
    private void SaveSettings(PluginSettings settings)
    {
        var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
        var filePath = System.IO.Path.Combine(dataPath, PluginSettingsFileName);
        System.IO.File.WriteAllText(filePath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }
    
    private class PluginSettings
    {
        public string ApiKey { get; set; }
        public string ServerUrl { get; set; }
    }

    private static RemappedPlayState RemapPlayState(PlayState playState) => playState switch
    {
        PlayState.Playing => RemappedPlayState.Playing,
        PlayState.Paused => RemappedPlayState.Paused,
        _ => RemappedPlayState.Other
    };

    private enum RemappedPlayState
    {
        Playing,
        Paused,
        Other,
        Offline
    }

    private class PlayingData
    {
        public string Artist { get; set; }
        public string Title { get; set; }
        public string Album { get; set; }
        public int DurationMs { get; set; }
        public int PositionMs { get; set; }
        public RemappedPlayState PlayState { get; set; }
        public string AlbumArt { get; set; }
    }

    public bool Configure(IntPtr panelHandle)
    {
        // Save any persistent settings in a sub-folder of this path
        // var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();

        // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
        // Keep in mind the panel width is scaled according to the font the user has selected
        // If about.ConfigurationPanelHeight is set to 0, you can display your own popup window
        if (panelHandle == IntPtr.Zero) return false;

        var configPanel = (Panel)Control.FromHandle(panelHandle);
        var prompt = new Label();
        prompt.AutoSize = true;
        prompt.Location = new Point(0, 0);
        prompt.Text = "prompt:";
        using var textBox = new TextBox();
        textBox.Bounds = new Rectangle(60, 0, 100, textBox.Height);
        configPanel.Controls.AddRange([prompt, textBox]);

        return false;
    }

    // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
    // its up to you to figure out whether anything has changed and needs updating
    public void SaveSettings()
    {
        // save any persistent settings in a sub-folder of this path
        // var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
    }

    // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
    public void Close(PluginCloseReason reason)
    {
        var playingData = GetStatus();
        playingData.PlayState = RemappedPlayState.Offline;
        PostData(playingData);
    }

    // On uninstall clean up any persisted files
    public void Uninstall()
    {
        var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
        var filePath = System.IO.Path.Combine(dataPath, PluginSettingsFileName);
        System.IO.File.Delete(filePath);
    }
}
