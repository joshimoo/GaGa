﻿
// GaGa.
// A minimal radio player for the Windows Tray.


using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;


namespace GaGa
{
    internal class Player
    {
        private readonly NotifyIcon notifyIcon;
        private readonly MediaPlayer player;

        private readonly Icon idleIcon;
        private readonly Icon playingIcon;
        private readonly Icon playingMutedIcon;
        private readonly Icon[] bufferingIcons;

        private readonly DispatcherTimer bufferingIconTimer;
        private Int32 currentBufferingIcon;

        /// <summary>
        /// Get the current source stream.
        /// </summary>
        public PlayerStream Source
        {
            get;
            private set;
        }

        /// <summary>
        /// True when not currently playing.
        /// </summary>
        public Boolean IsIdle
        {
            get;
            private set;
        }

        /// <summary>
        /// A media player that takes control of a notifyicon icon,
        /// tooltip and balloon to display status.
        /// </summary>
        /// <param name="icon">
        /// The notify icon to use.
        /// </param>
        public Player(NotifyIcon icon)
        {
            notifyIcon = icon;

            player = new MediaPlayer();
            player.BufferingStarted += OnBufferingStarted;
            player.BufferingEnded += OnBufferingEnded;
            player.MediaEnded += OnMediaEnded;
            player.MediaFailed += OnMediaFailed;

            idleIcon = Util.ResourceAsIcon("GaGa.Resources.idle.ico");
            playingIcon = Util.ResourceAsIcon("GaGa.Resources.playing.ico");
            playingMutedIcon = Util.ResourceAsIcon("GaGa.Resources.playing-muted.ico");

            bufferingIcons = new Icon[] {
                Util.ResourceAsIcon("GaGa.Resources.buffering1.ico"),
                Util.ResourceAsIcon("GaGa.Resources.buffering2.ico"),
                Util.ResourceAsIcon("GaGa.Resources.buffering3.ico"),
                Util.ResourceAsIcon("GaGa.Resources.buffering4.ico"),
            };

            bufferingIconTimer = new DispatcherTimer();
            bufferingIconTimer.Interval = TimeSpan.FromMilliseconds(300);
            bufferingIconTimer.Tick += OnBufferingIconTimerTick;
            currentBufferingIcon = 0;

            Source = null;
            IsIdle = true;

            UpdateIcon();
        }

        ///
        /// Icon handling
        ///

        /// <summary>
        /// Updates the notify icon icon and tooltip text
        /// depending on the current player state.
        /// </summary>
        private void UpdateIcon()
        {
            Icon icon;
            String text;

            // player state
            if (IsIdle)
            {
                icon = idleIcon;
                text = "Idle";
            }
            else if (player.IsMuted)
            {
                icon = playingMutedIcon;
                text = "Playing (muted)";
            }
            else
            {
                icon = playingIcon;
                text = "Playing";
            }

            // separator:
            text += " - ";

            // source state:
            if (Source == null)
            {
                text += "No stream selected";
            }
            else
            {
                text += Source.Name;
            }

            notifyIcon.Icon = icon;
            notifyIcon.SetToolTipText(text);
        }

        ///
        /// Player
        ///

        /// <summary>
        /// Open and play the current source stream.
        /// Unmutes the player.
        /// </summary>
        public void Play()
        {
            // do nothing if there is no source:
            if (Source == null)
                return;

            player.Open(Source.Uri);
            player.Play();
            player.IsMuted = false;

            IsIdle = false;
            UpdateIcon();
        }

        /// <summary>
        /// Stop playing and close the current stream.
        /// Unmutes the player.
        /// </summary>
        public void Stop()
        {
            // do nothing if already idle:
            if (IsIdle)
                return;

            // corner case:
            // if we only call .Stop(), the player continues downloading
            // from online streams, but .Close() calls _mediaState.Init()
            // changing the volume, so save and restore it:
            // .NET reference source:
            // <http://referencesource.microsoft.com/#PresentationCore/src/Core/CSharp/System/Windows/Media/MediaPlayerState.cs>
            Double volume = player.Volume;

            player.Stop();
            player.Close();
            player.IsMuted = false;

            player.Volume = volume;

            bufferingIconTimer.Stop();
            currentBufferingIcon = 0;

            IsIdle = true;
            UpdateIcon();
        }

        /// <summary>
        /// Set a given stream as current and play it.
        /// Unmutes the player.
        /// </summary>
        /// <param name="stream">Source stream to play.</param>
        public void Play(PlayerStream stream)
        {
            Source = stream;
            Play();
        }

        /// <summary>
        /// Mute the player.
        /// </summary>
        public void Mute()
        {
            // do nothing if idle or already muted:
            if (IsIdle || player.IsMuted)
                return;

            player.IsMuted = true;
            UpdateIcon();
        }

        /// <summary>
        /// Unmute the player.
        /// </summary>
        public void UnMute()
        {
            // do nothing if idle or not muted:
            if (IsIdle || !player.IsMuted)
                return;

            player.IsMuted = false;
            UpdateIcon();
        }

        /// <summary>
        /// Toggle between idle/playing.
        /// </summary>
        public void TogglePlay()
        {
            if (IsIdle)
            {
                Play();
            }
            else
            {
                Stop();
            }
        }

        /// <summary>
        /// Toggle between muted/unmuted.
        /// </summary>
        public void ToggleMute()
        {
            if (player.IsMuted)
            {
                UnMute();
            }
            else
            {
                Mute();
            }
        }

        /// <summary>
        /// Get or set the player balance.
        /// </summary>
        public Double Balance
        {
            get
            {
                return player.Balance;
            }
            set
            {
                player.Balance = value;
            }
        }

        /// <summary>
        /// Get or set the player volume.
        /// </summary>
        public Double Volume
        {
            get
            {
                return player.Volume;
            }
            set
            {
                player.Volume = value;
            }
        }

        ///
        /// Buffering animation
        ///

        /// <summary>
        /// Start watching the buffering state to update our icon.
        /// </summary>
        private void OnBufferingStarted(Object sender, EventArgs e)
        {
            bufferingIconTimer.Start();
        }

        /// <summary>
        /// Stop watching the buffering state.
        /// </summary>
        private void OnBufferingEnded(Object sender, EventArgs e)
        {
            bufferingIconTimer.Stop();
            currentBufferingIcon = 0;
            UpdateIcon();
        }

        /// <summary>
        /// Override the current icon with an animation while buffering.
        /// </summary>
        private void OnBufferingIconTimerTick(Object sender, EventArgs e)
        {
            // only change the icon when NOT muted
            // the mute icon has priority over the buffering icons:
            if (!player.IsMuted)
            {
                notifyIcon.Icon = bufferingIcons[currentBufferingIcon];
            }

            // but keep the animation always running:
            currentBufferingIcon++;
            if (currentBufferingIcon == bufferingIcons.Length)
            {
                currentBufferingIcon = 0;
            }
        }

        ///
        /// Media events
        ///

        /// <summary>
        /// Update state when media ended.
        /// </summary>
        private void OnMediaEnded(Object sender, EventArgs e)
        {
            Stop();
        }

        /// <summary>
        /// Update state when media failed. Show an error balloon with details.
        /// </summary>
        private void OnMediaFailed(Object sender, ExceptionEventArgs e)
        {
            Stop();

            String title = "Unable to play: " + Source.Name;
            String text = e.ErrorException.Message + "\n" + Source.Uri;

            notifyIcon.ShowBalloonTip(10, title, text, ToolTipIcon.Error);
        }
    }
}

