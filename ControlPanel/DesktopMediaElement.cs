using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
namespace KillConfirmGameBar;
// Keep the original video clip editor's behavior on WinUI's MediaPlayerElement.
internal sealed class DesktopMediaElement : MediaPlayerElement
{
    private readonly MediaPlayer _player = new();
    internal DesktopMediaElement() { SetMediaPlayer(_player); }
    internal TimeSpan Position { get => _player.PlaybackSession.Position; set => _player.PlaybackSession.Position=value; }
    internal void SetSource(IRandomAccessStream stream, string contentType) => Source=MediaSource.CreateFromStream(stream,contentType);
    internal void Play() => _player.Play();
    internal void Pause() => _player.Pause();
    internal void Stop() { _player.Pause(); _player.PlaybackSession.Position=TimeSpan.Zero; }
}
