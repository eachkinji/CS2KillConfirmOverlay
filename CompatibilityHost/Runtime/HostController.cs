using KillConfirmCompatibility.Contracts;
using KillConfirmCompatibility.Controls;
using KillConfirmCompatibility.Danmaku;
using KillConfirmCompatibility.Desktop.Windowing;
using KillConfirmCompatibility.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace KillConfirmCompatibility.Desktop.Runtime
{
    internal sealed class HostController
    {
        private readonly FeedbackPresenter _presenter = new();
        private readonly DanmakuOverlay _danmaku = new();
        private readonly KillConfirmAnimation _danmakuState = new();
        private readonly List<OverlaySurface> _surfaces = new();
        private readonly ServiceSession _service = new();
        private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Render);
        private readonly DispatcherTimer _stateTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly string _configurationPath, _statusPath;
        private readonly Window _toolbar;
        private readonly HwndSource _hotkeys;
        private KillEventClient _events;
        private System.Threading.Tasks.Task _applyingConfiguration = System.Threading.Tasks.Task.CompletedTask;
        private DisplayConfiguration _configuration;
        private LayoutProfile _layout;
        private long _editRequest, _testRequest, _restartRequest;
        private bool _lastDanmakuEnabled;
        private string _style, _lastSignature, _renderError;
        private bool _editing, _hidden, _closing, _visible;
        private readonly bool _shutdownApplicationOnClose;
        private IntPtr _gameWindow;
        private DateTimeOffset _nextFind, _nextRegister, _nextStatus, _previewUntil;
        private int _lastPort;
        public HostController(bool shutdownApplicationOnClose = true)
        {
            _shutdownApplicationOnClose = shutdownApplicationOnClose;
            string folder = Path.Combine(DesktopStorage.Current.LocalFolder.Path, DisplayFiles.FolderName);
            _configurationPath = Path.Combine(folder, DisplayFiles.ConfigurationName);
            _statusPath = Path.Combine(folder, DisplayFiles.StatusName);
            _configuration = DisplayFiles.Read<DisplayConfiguration>(_configurationPath) ?? new DisplayConfiguration();
            _configuration.Normalize();
            _restartRequest = _configuration.RestartRequest;
            _lastDanmakuEnabled = DanmakuSettingsStore.IsEnabled;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _editRequest = now - _configuration.EditRequest > 10000 ? _configuration.EditRequest : 0;
            _testRequest = now - _configuration.TestRequest > 10000 ? _configuration.TestRequest : 0;
            KillFeedbackVisibilitySettingsStore.ApplyDesktopVisibility = (style, values) =>
            {
                var profile = _configuration.GetLayout(GameStyleService.ToStorageValue(style));
                values.CrosshairEnabled = profile.Crosshair.Visible;
                values.LowerEnabled = profile.Lower.Visible;
                values.UpperEnabled = profile.Upper.Visible;
            };
            AddSurface("Crosshair", "准星反馈", _presenter.CrosshairFeedbackAnimation);
            AddSurface("Lower", "下方反馈", _presenter.LowerFeedbackAnimation);
            AddSurface("Badge", "徽章", _presenter.LowerBadgeAnimation);
            AddSurface("Upper", "上方反馈", _presenter.UpperFeedbackAnimation);
            var danmakuSurface = AddSurface("Danmaku", "弹幕区域", _danmakuState);
            danmakuSurface.DirtyOverride = () => _danmaku.IsFrameDirty;
            danmakuSurface.DrawOverride = (session, w, h) =>
            {
                session.Transform = Matrix3x2.CreateScale((float)(w / Math.Max(1, _danmaku.Width)), (float)(h / Math.Max(1, _danmaku.Height)));
                _danmaku.DrawDesktopFrame(session);
            };
            _presenter.DanmakuEvent = _danmaku.TriggerGameEvent;
            _danmaku.RaiseLoaded();
            _toolbar = BuildToolbar();
            _hotkeys = new HwndSource(new HwndSourceParameters("KillConfirmCompatibilityHotkeys") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 });
            _hotkeys.AddHook(HotkeyMessage);
            NativeWindows.RegisterHotKey(_hotkeys.Handle, 1, 0x4003, 0x4F);
            NativeWindows.RegisterHotKey(_hotkeys.Handle, 2, 0x4003, 0x4C);
            StartEvents();
            _frameTimer.Tick += RenderFrame;
            _stateTimer.Tick += UpdateState;
            _frameTimer.Start(); _stateTimer.Start(); UpdateState(null, EventArgs.Empty);
        }
        private OverlaySurface AddSurface(string key, string label, KillConfirmAnimation animation)
        {
            var surface = new OverlaySurface(key, label, animation) { LayoutChanged = SaveLayout, FinishEditing = EndEditing };
            _surfaces.Add(surface); return surface;
        }
        private Window BuildToolbar()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            row.Children.Add(new TextBlock { Text = "拖动元素 · 滚轮缩放 · 方向键微调 · Esc 结束", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) });
            var test = new Button { Content = "预览", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) };
            test.Click += (s, e) => Preview(); row.Children.Add(test);
            var finish = new Button { Content = "保存并结束", Padding = new Thickness(12, 5, 12, 5) };
            finish.Click += (s, e) => EndEditing(); row.Children.Add(finish);
            var toolbar = new Window { Title = "兼容显示布局编辑", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, Topmost = true, SizeToContent = SizeToContent.WidthAndHeight, Background = new SolidColorBrush(Color.FromRgb(32, 35, 42)), Content = row };
            toolbar.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Enter) { EndEditing(); e.Handled = true; }
            };
            return toolbar;
        }
        private void StartEvents()
        {
            _events?.Dispose(); _lastPort = LocalServiceEndpoints.Port;
            _events = new KillEventClient(new DesktopDispatcher());
            _events.KillReceived += (s, ev) =>
            {
                if (_closing || _hidden || !_visible || _editing || !_applyingConfiguration.IsCompleted) return;
                try { _presenter.HandleKillEvent(ev); } catch (Exception error) { _renderError = error.Message; App.Log("Compatibility event failed: " + error); }
            };
            _events.Start();
        }
        private void UpdateState(object sender, EventArgs args)
        {
            if (_closing) return;
            try
            {
                DisplayConfiguration next = DisplayFiles.Read<DisplayConfiguration>(_configurationPath);
                if (next != null) { next.Normalize(); _configuration = next; }
                if (!_configuration.Enabled) { _ = CloseAsync(); return; }
                if (_configuration.RestartRequest != _restartRequest) { _restartRequest = _configuration.RestartRequest; _ = CloseAsync(); return; }
                if (_lastDanmakuEnabled != DanmakuSettingsStore.IsEnabled)
                {
                    _lastDanmakuEnabled = DanmakuSettingsStore.IsEnabled;
                    DanmakuSettingsStore.NotifyExternalEnabledChange(_lastDanmakuEnabled);
                }
                string style = GameStyleService.ToStorageValue(GameStyleService.Current);
                if (_style != style)
                {
                    if (_editing) EndEditing();
                    _presenter.CrosshairFeedbackAnimation.StopDesktopPlayback(); _presenter.LowerFeedbackAnimation.StopDesktopPlayback();
                    _presenter.LowerBadgeAnimation.StopDesktopPlayback(); _presenter.UpperFeedbackAnimation.StopDesktopPlayback();
                    _style = style;
                }
                if (!_surfaces.Any(surface => surface.IsDragging)) _layout = _configuration.GetLayout(_style);
                string signature = _style + "|" + DesktopStorage.Current.LocalSettings.Values["KillIconPack." + _style] + "|" + DesktopStorage.Current.LocalSettings.Values["KillEliteEffect"] + "|" + DesktopStorage.Current.LocalSettings.Values["KillWeaponBadge"] + "|" + DesktopStorage.Current.LocalSettings.Values["KillFxEnabled"] + "|" + DesktopStorage.Current.LocalSettings.Values["MainAnimationStyle"] + "|" + File.GetLastWriteTimeUtc(Path.Combine(DesktopStorage.Current.LocalFolder.Path, "pack-catalog.json")).Ticks;
                if (_lastSignature != signature && _applyingConfiguration.IsCompleted) { _applyingConfiguration = _presenter.ApplyConfigurationAsync(); _lastSignature = signature; _renderError = null; }
                if (_applyingConfiguration.IsCompleted)
                {
                    if (_configuration.EditRequest != _editRequest) { _editRequest = _configuration.EditRequest; BeginEditing(); }
                    if (_configuration.TestRequest != _testRequest) { _testRequest = _configuration.TestRequest; Preview(); }
                }
                _frameTimer.Interval = TimeSpan.FromSeconds(1.0 / _configuration.FramesPerSecond);
                var screens = NativeWindows.Screens();
                if (screens.Length == 0) return;
                var monitor = screens.FirstOrDefault(s => s.Device == _configuration.ScreenName);
                if (monitor.Size == 0) monitor = screens.FirstOrDefault(s => (s.Flags & 1) != 0);
                if (monitor.Size == 0) monitor = screens[0];
                NativeWindows.Rect bounds = monitor.Monitor;
                if (DateTimeOffset.UtcNow >= _nextFind) { _gameWindow = NativeWindows.FindGameWindow(); _nextFind = DateTimeOffset.UtcNow.AddSeconds(2); }
                bool hasGame = NativeWindows.TryGameBounds(_gameWindow, out var gameBounds);
                if (_configuration.FollowGame && hasGame) bounds = gameBounds;
                bool gameActive = NativeWindows.IsGameWindow(NativeWindows.GetForegroundWindow());
                _visible = !_hidden && (_editing || DateTimeOffset.UtcNow < _previewUntil || !_configuration.HideWhenInactive || gameActive);
                var visibility = KillFeedbackVisibilitySettingsStore.Load(GameStyleService.Current);
                foreach (OverlaySurface surface in _surfaces)
                {
                    string key = surface.ElementKey;
                    ElementLayout element = _layout.GetElement(key == "Badge" ? "Lower" : key);
                    bool supported = key != "Upper" || GameStyleService.Current == GameStyleMode.ModernWarfare2019;
                    bool enabled = key == "Crosshair" ? visibility.CrosshairEnabled : key == "Upper" ? visibility.UpperEnabled : key == "Danmaku" ? DanmakuSettingsStore.IsEnabled : visibility.LowerEnabled;
                    if (key == "Danmaku")
                    {
                        double dpi = Math.Max(96, NativeWindows.GetDpiForWindow(new WindowInteropHelper(surface).Handle)) / 96.0;
                        surface.FixedSize = new Size(bounds.Width * 0.9 / dpi, bounds.Height * 0.3 / dpi);
                        _danmaku.Width = surface.FixedSize.Value.Width; _danmaku.Height = surface.FixedSize.Value.Height;
                        _danmakuState.Visibility = global::Windows.UI.Xaml.Visibility.Visible;
                    }
                    surface.Place(bounds, element, _visible && supported && (enabled || _editing), _editing && key != "Badge", key == "Badge" ? -128 : 0);
                }
                if (_editing)
                {
                    new WindowInteropHelper(_toolbar).EnsureHandle();
                    NativeWindows.SetWindowPos(new WindowInteropHelper(_toolbar).Handle, new IntPtr(-1), bounds.Left + 20, bounds.Top + 20, 0, 0, 0x1 | 0x10);
                }
                if (LocalServiceEndpoints.Port != _lastPort) StartEvents();
                if (DateTimeOffset.UtcNow >= _nextRegister) { _nextRegister = DateTimeOffset.UtcNow.AddSeconds(3); _ = _service.EnsureRegisteredAsync(); }
                if (DateTimeOffset.UtcNow >= _nextStatus)
                {
                    _nextStatus = DateTimeOffset.UtcNow.AddSeconds(1);
                    DisplayFiles.Write(_statusPath, new DisplayStatus { Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), ProcessId = Environment.ProcessId, Connected = _events.ConnectionState == KillEventConnectionState.Connected, Editing = _editing, Visible = _visible, Screen = monitor.Device, Screens = screens.Select(s => s.Device).ToArray(), Error = _renderError ?? _presenter.ConfigurationError ?? _service.Error });
                }
            }
            catch (Exception error) { _renderError = error.Message; App.Log("Compatibility state: " + error); }
        }
        private void RenderFrame(object sender, EventArgs args)
        {
            try { _danmaku.AdvanceDesktopFrame(); }
            catch (Exception error) { _renderError = error.Message; App.Log("Compatibility danmaku: " + error); }
            foreach (OverlaySurface surface in _surfaces)
            {
                try { surface.Render(); }
                catch (Exception error) { _renderError = error.Message; App.Log("Compatibility drawing: " + error); }
            }
        }
        private void SaveLayout()
        {
            if (_layout == null || _style == null) return;
            string style = _style; LayoutProfile layout = _layout;
            DisplayFiles.Update(_configurationPath, config => config.Layouts[style] = layout);
        }
        private void BeginEditing() { _hidden = false; _editing = true; _toolbar.Show(); Preview(); }
        private void EndEditing() { SaveLayout(); _editing = false; _toolbar.Hide(); }
        private void Preview()
        {
            _previewUntil = DateTimeOffset.UtcNow.AddSeconds(5); _hidden = false;
            _presenter.HandleKillEvent(new KillEvent { KillCount = 3, IsHeadshot = true, PlayMainAnimation = true, EventChannel = "combat", EventKind = "kill", AnimationKey = "multi3", TargetName = "预览玩家", WeaponName = "AK-47", MoneyReward = 300 });
            if (DanmakuSettingsStore.IsEnabled) _danmaku.TriggerBarrage(5, 3);
        }
        private IntPtr HotkeyMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (message != 0x312) return IntPtr.Zero;
            if (wparam.ToInt32() == 1) _hidden = !_hidden;
            else if (_editing) EndEditing(); else BeginEditing();
            handled = true; return IntPtr.Zero;
        }
        public async System.Threading.Tasks.Task CloseAsync()
        {
            if (_closing) return; _closing = true;
            _frameTimer.Stop(); _stateTimer.Stop(); _events?.Dispose(); _danmaku.RaiseUnloaded();
            NativeWindows.UnregisterHotKey(_hotkeys.Handle, 1); NativeWindows.UnregisterHotKey(_hotkeys.Handle, 2); _hotkeys.Dispose();
            foreach (var surface in _surfaces) surface.Dispose(); _toolbar.Close();
            await _service.ReleaseAsync();
            try { DisplayFiles.Write(_statusPath, new DisplayStatus { Timestamp = 0, ProcessId = 0 }); } catch { }
            if (_shutdownApplicationOnClose) System.Windows.Application.Current.Shutdown();
        }
    }
}
