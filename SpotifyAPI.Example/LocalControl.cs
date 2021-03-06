﻿using SpotifyAPI.Local;
using SpotifyAPI.Local.Enums;
using SpotifyAPI.Local.Models;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;

namespace SpotifyAPI.Example
{
    public partial class LocalControl : UserControl
    {
        private readonly SpotifyLocalAPIConfig _config;
        private SpotifyLocalAPI _spotify;
        private Track _currentTrack;

        public LocalControl()
        {
            InitializeComponent();
            
            _config = new SpotifyLocalAPIConfig
            {
                ProxyConfig = new ProxyConfig()
            };

            _spotify = new SpotifyLocalAPI(_config);
            _spotify.OnPlayStateChange += _spotify_OnPlayStateChange;
            _spotify.OnTrackChange += _spotify_OnTrackChange;
            _spotify.OnTrackTimeChange += _spotify_OnTrackTimeChange;
            _spotify.OnVolumeChange += _spotify_OnVolumeChange;
            //_spotify.SynchronizingObject = this;

            artistLinkLabel.Click += (sender, args) => Process.Start(artistLinkLabel.Tag.ToString());
            albumLinkLabel.Click += (sender, args) => Process.Start(albumLinkLabel.Tag.ToString());
            titleLinkLabel.Click += (sender, args) => Process.Start(titleLinkLabel.Tag.ToString());
        }

        public void Connect()
        {
            if (!SpotifyLocalAPI.IsSpotifyRunning())
            {
                MessageBox.Show(@"Spotify isn't running!");
                return;
            }
            if (!SpotifyLocalAPI.IsSpotifyWebHelperRunning())
            {
                MessageBox.Show(@"SpotifyWebHelper isn't running!");
                return;
            }

            bool successful = _spotify.Connect();
            if (successful)
            {
                connectBtn.Text = @"Connection to Spotify successful";
                connectBtn.Enabled = false;
                UpdateInfos();
                _spotify.ListenForEvents = true;
            }
            else
            {
                DialogResult res = MessageBox.Show(@"Couldn't connect to the spotify client. Retry?", @"Spotify", MessageBoxButtons.YesNo);
                if (res == DialogResult.Yes)
                    Connect();
            }
        }

        public void UpdateInfos()
        {
            StatusResponse status = _spotify.GetStatus();
            if (status == null)
                return;

            //Basic Spotify Infos
            UpdatePlayingStatus(status.Playing);
            clientVersionLabel.Text = status.ClientVersion;
            versionLabel.Text = status.Version.ToString();
            repeatShuffleLabel.Text = status.Repeat + @" and " + status.Shuffle;

            if (status.Track != null) //Update track infos
                UpdateTrack(status.Track);

            RefreshVolumeMixerVolume();
        }

        public async void UpdateTrack(Track track)
        {
            _currentTrack = track;

            advertLabel.Text = track.IsAd() ? "ADVERT" : "";
            timeProgressBar.Maximum = track.Length;

            if (track.IsAd())
                return; //Don't process further, maybe null values

            titleLinkLabel.Text = track.TrackResource?.Name;
            titleLinkLabel.Tag = track.TrackResource?.Uri;

            artistLinkLabel.Text = track.ArtistResource?.Name;
            artistLinkLabel.Tag = track.ArtistResource?.Uri;

            albumLinkLabel.Text = track.AlbumResource?.Name;
            albumLinkLabel.Tag = track.AlbumResource?.Uri;

            SpotifyUri uri = track.TrackResource?.ParseUri();

            trackInfoBox.Text = $@"Track Info - {uri?.Id}";

            bigAlbumPicture.Image = track.AlbumResource != null ? await track.GetAlbumArtAsync(AlbumArtSize.Size640, _config.ProxyConfig) : null;
            smallAlbumPicture.Image = track.AlbumResource != null ? await track.GetAlbumArtAsync(AlbumArtSize.Size160, _config.ProxyConfig) : null;

            SpotifyWebAPI swa = WebControl._spotify;
            if (swa != null)
            {
                AudioFeatures af = swa.GetAudioFeatures(uri?.Id);
                // int i = int.Parse(af.Acousticness.ToString());
                FloatToTrackbar(af.Acousticness, tbarAcousticness);
                FloatToTrackbar(af.Danceability, tbarDanceability);
                FloatToTrackbar(af.Energy, tbarEnergy);
                FloatToTrackbar(af.Instrumentalness, tbarInstrumentalness);
                FloatToTrackbar(af.Liveness,tbarLiveness);
                FloatToTrackbar(af.Speechiness,tbarSpeechiness);

                FloatToTrackbar(af.Tempo/100,tbarTempo);
                FloatToTrackbar(af.Loudness,tbarLoudness);
                FloatToTrackbar(af.Valence, tbarValence);

                txtKey.Text = String.Format("{0} {1}", Enum.GetName(typeof(KeyType), af.Key),Enum.GetName(typeof(ModeType),af.Mode));
                txtTime.Text = af.TimeSignature.ToString();


            }


        }

        private void FloatToTrackbar(double floatValue, TrackBar target)

        {
            double displayValue = Math.Round(floatValue * 100, 2);
            target.Value = int.Parse(Math.Round(displayValue, 0).ToString());
        }

        public void UpdatePlayingStatus(bool playing)
        {
            isPlayingLabel.Text = playing.ToString();
        }

        public void RefreshVolumeMixerVolume()
        {
            volumeMixerLabel.Text = _spotify.GetSpotifyVolume().ToString(CultureInfo.InvariantCulture);
        }

        private void applyProxyBtn_Click(object sender, EventArgs e)
        {
            _config.ProxyConfig.Host = proxyHostTextBox.Text;
            _config.ProxyConfig.Port = (int)proxyPortUpDown.Value;
            _config.ProxyConfig.Username = proxyUsernameTextBox.Text;
            _config.ProxyConfig.Password = proxyPasswordTextBox.Text;

            bool connected = _spotify.ListenForEvents;
            if (connected)
            {
                // Reconnect using new proxy
                _spotify.ListenForEvents = false;
                _spotify.OnPlayStateChange -= _spotify_OnPlayStateChange;
                _spotify.OnTrackChange -= _spotify_OnTrackChange;
                _spotify.OnTrackTimeChange -= _spotify_OnTrackTimeChange;
                _spotify.OnVolumeChange -= _spotify_OnVolumeChange;

                _spotify.Dispose();

                _spotify = new SpotifyLocalAPI(_config);
                _spotify.OnPlayStateChange += _spotify_OnPlayStateChange;
                _spotify.OnTrackChange += _spotify_OnTrackChange;
                _spotify.OnTrackTimeChange += _spotify_OnTrackTimeChange;
                _spotify.OnVolumeChange += _spotify_OnVolumeChange;

                connectBtn.Text = @"Reconnecting...";
                Connect();
            }
        }

        private void _spotify_OnVolumeChange(object sender, VolumeChangeEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _spotify_OnVolumeChange(sender, e)));
                return;
            }
            volumeLabel.Text = (e.NewVolume * 100).ToString(CultureInfo.InvariantCulture);
        }

        private void _spotify_OnTrackTimeChange(object sender, TrackTimeChangeEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _spotify_OnTrackTimeChange(sender, e)));
                return;
            }
            timeLabel.Text = $@"{FormatTime(e.TrackTime)}/{FormatTime(_currentTrack.Length)}";
            if(e.TrackTime < _currentTrack.Length)
                timeProgressBar.Value = (int)e.TrackTime;
        }

        private void _spotify_OnTrackChange(object sender, TrackChangeEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _spotify_OnTrackChange(sender, e)));
                return;
            }
            UpdateTrack(e.NewTrack);
        }

        private void _spotify_OnPlayStateChange(object sender, PlayStateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => _spotify_OnPlayStateChange(sender, e)));
                return;
            }
            UpdatePlayingStatus(e.Playing);
        }

        private void connectBtn_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private async void playUrlBtn_Click(object sender, EventArgs e)
        {
            await _spotify.PlayURL(playTextBox.Text, contextTextBox.Text);
        }

        private async void playBtn_Click(object sender, EventArgs e)
        {
            await _spotify.Play();
        }

        private async void pauseBtn_Click(object sender, EventArgs e)
        {
            await _spotify.Pause();
        }

        private void prevBtn_Click(object sender, EventArgs e)
        {
            _spotify.Previous();
        }

        private void skipBtn_Click(object sender, EventArgs e)
        {
            _spotify.Skip();
        }

        private void volumeUpBtn_Click(object sender, EventArgs e)
        {
            float currentVolume = _spotify.GetSpotifyVolume();
            float newVolume = currentVolume + 2.0f;
            _spotify.SetSpotifyVolume(newVolume >= 100.0f ? 100.0f : newVolume);

            RefreshVolumeMixerVolume();
        }

        private void volumeDownBtn_Click(object sender, EventArgs e)
        {
            float currentVolume = _spotify.GetSpotifyVolume();
            float newVolume = currentVolume - 2.0f;
            _spotify.SetSpotifyVolume(newVolume <= 0.0f ? 0.0f : newVolume);

            RefreshVolumeMixerVolume();
        }

        private static String FormatTime(double sec)
        {
            TimeSpan span = TimeSpan.FromSeconds(sec);
            String secs = span.Seconds.ToString(), mins = span.Minutes.ToString();
            if (secs.Length < 2)
                secs = "0" + secs;
            return mins + ":" + secs;
        }
    }
}