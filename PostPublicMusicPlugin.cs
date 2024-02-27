using System;
using System.Drawing;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new();

        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            _about.PluginInfoVersion = PluginInfoVersion;

            _about.Name = "PostPublicMusic";
            _about.Description = "A plugin to display what music I'm listening to on my website";
            _about.Author = "Autumn";
            _about.TargetApplication = "";   //  the name of a Plugin Storage device or panel header for a dockable panel
            _about.Type = PluginType.General;

            _about.VersionMajor = 1;  // your plugin version
            _about.VersionMinor = 0;
            _about.Revision = 1;

            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;

            _about.ReceiveNotifications = (ReceiveNotificationFlags.PlayerEvents | ReceiveNotificationFlags.TagEvents);
            _about.ConfigurationPanelHeight = 0;   // height in pixels that MusicBee should reserve in a panel for config settings. When set, a handle to an empty panel will be passed to the Configure function

            return _about;
        }

        public bool Configure(IntPtr panelHandle)
        {
            // save any persistent settings in a sub-folder of this path
            // var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();

            // panelHandle will only be set if you set about.ConfigurationPanelHeight to a non-zero value
            // keep in mind the panel width is scaled according to the font the user has selected
            // if about.ConfigurationPanelHeight is set to 0, you can display your own popup window
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
        }

        // uninstall this plugin - clean up any persisted files
        public void Uninstall()
        {
        }

        // receive event notifications from MusicBee
        // you need to set about.ReceiveNotificationFlags = PlayerEvents to receive all notifications, and not just the startup event
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
            // perform some action depending on the notification type
            if (type is not (NotificationType.TrackChanged or NotificationType.PlayStateChanged)) return;


            var artist = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Artist);
            var title = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.TrackTitle);
            var album = _mbApiInterface.NowPlaying_GetFileTag(MetaDataType.Album);
            var durationMs = _mbApiInterface.NowPlaying_GetDuration();
            var positionMs = _mbApiInterface.Player_GetPosition();
            var playState = RemapPlayState(_mbApiInterface.Player_GetPlayState());

            var albumArt = _mbApiInterface.NowPlaying_GetArtwork();

            var json = new
            {
                artist,
                title,
                album,
                durationMs,
                positionMs,
                playState,

                albumArt
            };

            // Send the JSON object to the server 
            var client = new System.Net.WebClient();
            client.Headers.Add("Content-Type", "application/json");
            client.UploadString("http://localhost:3000/now-playing", "POST", JsonConvert.SerializeObject(json));
        }

        private static RemappedPlayState RemapPlayState(PlayState playState)
        {
            return playState switch
            {
                PlayState.Playing => RemappedPlayState.Playing,
                PlayState.Paused => RemappedPlayState.Paused,
                _ => RemappedPlayState.Other
            };
        }

        private enum RemappedPlayState
        {
            Playing,
            Paused,
            Other
        }
    }
}