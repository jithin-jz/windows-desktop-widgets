using System;
using System.Threading.Tasks;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace KiroWidgets
{
    internal class MediaSnapshot
    {
        internal bool HasSession;
        internal string Title;
        internal string Artist;
        internal bool IsPlaying;
        /// <summary>Album art bytes, or null when unchanged or unavailable.</summary>
        internal byte[] Thumbnail;
        /// <summary>Identity of the current track, used to avoid re-decoding art.</summary>
        internal string TrackKey;

        /// <summary>False when the player reports no usable timeline, which some
        /// browsers and streams do; the wave then stays unfilled.</summary>
        internal bool HasTimeline;
        internal TimeSpan Position;
        internal TimeSpan Duration;
        /// <summary>
        /// When the player last reported Position. Players update it only
        /// occasionally, so the UI advances it locally from this stamp rather than
        /// polling the session faster.
        /// </summary>
        internal DateTimeOffset PositionAt;

        /// <summary>
        /// Whether the player accepts a seek. It varies by app - Chrome reports
        /// true, some players report false - so the wave only acts as a scrubber
        /// when this is set.
        /// </summary>
        internal bool CanSeek;
    }

    /// <summary>
    /// Reads the system media transport controls, the same source the Windows
    /// volume flyout uses, so it works for any player that reports to Windows.
    /// </summary>
    internal class MediaMonitor
    {
        private const uint MaxArtBytes = 8 * 1024 * 1024;

        private GlobalSystemMediaTransportControlsSessionManager manager;
        private string lastTrackKey = "";

        internal async Task<MediaSnapshot> ReadAsync()
        {
            MediaSnapshot snap = new MediaSnapshot();
            try
            {
                if (manager == null)
                {
                    manager = await WinRtAsync.ToTask(
                        GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                }

                GlobalSystemMediaTransportControlsSession session =
                    manager == null ? null : manager.GetCurrentSession();

                if (session == null)
                {
                    lastTrackKey = "";
                    return snap;   // HasSession stays false
                }

                snap.HasSession = true;

                GlobalSystemMediaTransportControlsSessionMediaProperties props =
                    await WinRtAsync.ToTask(session.TryGetMediaPropertiesAsync());

                if (props != null)
                {
                    snap.Title = string.IsNullOrWhiteSpace(props.Title) ? "Unknown track" : props.Title;
                    snap.Artist = props.Artist == null ? "" : props.Artist;
                    snap.TrackKey = snap.Title + "|" + snap.Artist;

                    if (snap.TrackKey != lastTrackKey)
                    {
                        lastTrackKey = snap.TrackKey;
                        snap.Thumbnail = await ReadThumbnailAsync(props);
                    }
                }

                GlobalSystemMediaTransportControlsSessionPlaybackInfo info = session.GetPlaybackInfo();
                snap.IsPlaying = info != null &&
                    info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (info != null && info.Controls != null)
                {
                    snap.CanSeek = info.Controls.IsPlaybackPositionEnabled;
                }

                // Timeline is a separate call and is allowed to fail on its own -
                // a player can report a title but no position - so it must not
                // discard the rest of the snapshot.
                try
                {
                    GlobalSystemMediaTransportControlsSessionTimelineProperties tl =
                        session.GetTimelineProperties();
                    if (tl != null)
                    {
                        TimeSpan span = tl.EndTime - tl.StartTime;
                        if (span > TimeSpan.Zero)
                        {
                            snap.Duration = span;
                            snap.Position = tl.Position - tl.StartTime;
                            snap.PositionAt = tl.LastUpdatedTime;
                            snap.HasTimeline = true;
                        }
                    }
                }
                catch { }
            }
            catch
            {
                // A player closing mid-call invalidates the manager; rebuild next tick.
                manager = null;
            }
            return snap;
        }

        /// <summary>
        /// Asks the current player to jump to a position. Returns false when the
        /// player refuses or advertises no seek support, so the caller can leave
        /// the wave where the player actually is.
        /// </summary>
        internal async Task<bool> SeekAsync(TimeSpan position)
        {
            try
            {
                if (manager == null) return false;
                GlobalSystemMediaTransportControlsSession session = manager.GetCurrentSession();
                if (session == null) return false;
                return await WinRtAsync.ToTask(
                    session.TryChangePlaybackPositionAsync(position.Ticks));
            }
            catch { return false; }
        }

        /// <summary>
        /// Pulls the album art through a WinRT DataReader rather than
        /// AsStreamForRead, which is unavailable for the same metadata reason
        /// described in WinRtAsync.
        /// </summary>
        private static async Task<byte[]> ReadThumbnailAsync(
            GlobalSystemMediaTransportControlsSessionMediaProperties props)
        {
            IRandomAccessStreamWithContentType stream = null;
            DataReader reader = null;
            try
            {
                IRandomAccessStreamReference reference = props.Thumbnail;
                if (reference == null) return null;

                stream = await WinRtAsync.ToTask(reference.OpenReadAsync());
                if (stream == null) return null;

                ulong size = stream.Size;
                if (size == 0 || size > MaxArtBytes) return null;

                reader = new DataReader(stream);
                uint loaded = await WinRtAsync.ToTask(reader.LoadAsync((uint)size));
                if (loaded == 0) return null;

                byte[] bytes = new byte[loaded];
                reader.ReadBytes(bytes);
                return bytes;
            }
            catch { return null; }
            finally
            {
                try { if (reader != null) reader.Dispose(); } catch { }
                try { if (stream != null) stream.Dispose(); } catch { }
            }
        }
    }
}
