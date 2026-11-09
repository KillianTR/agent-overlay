using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;

namespace ClaudeSessionOverlay;

/// <summary>
/// Emite una alerta sonora limpia (estilo iPhone) cuando un agente termina o requiere autorización/entrada.
/// Sin síntesis de voz (TTS eliminado).
/// </summary>
internal sealed class Notifier : IDisposable
{
    private static readonly string WindowsChime =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Windows Notify System Generic.wav");

    private readonly SessionStore _store;
    private readonly SoundPlayer? _chime;

    public Notifier(SessionStore store)
    {
        _store = store;
        _chime = LoadIosChime();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
    }

    private static SoundPlayer? LoadIosChime()
    {
        try
        {
            var asm = typeof(Notifier).Assembly;
            using var stream = asm.GetManifestResourceStream("ios_notification.wav");
            if (stream != null)
            {
                var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                var player = new SoundPlayer(ms);
                player.Load();
                return player;
            }

            string localWav = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sound", "ios_notification.wav");
            if (File.Exists(localWav))
            {
                var player = new SoundPlayer(localWav);
                player.Load();
                return player;
            }

            if (File.Exists(WindowsChime))
            {
                var player = new SoundPlayer(WindowsChime);
                player.Load();
                return player;
            }
        }
        catch { }
        return null;
    }

    // ── Silencio si el usuario está ausente: pantalla bloqueada, suspensión o pantalla apagada ──
    private static readonly TimeSpan QuietAfterReturn = TimeSpan.FromSeconds(10);

    private bool _isLocked;
    private bool _isSuspended;
    private bool _isDisplayOff;
    private DateTime _quietUntil;
    private IntPtr _displayNotification;

    private bool IsAway => _isLocked || _isSuspended || _isDisplayOff || DateTime.Now < _quietUntil;

    public void WatchDisplay(IntPtr hwnd)
    {
        var guid = Native.GUID_CONSOLE_DISPLAY_STATE;
        _displayNotification = Native.RegisterPowerSettingNotification(hwnd, ref guid, 0);
        HwndSource.FromHwnd(hwnd)?.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_POWERBROADCAST && wParam == Native.PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<Native.POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == Native.GUID_CONSOLE_DISPLAY_STATE)
                SetAway(ref _isDisplayOff, setting.Data == 0);
        }
        return IntPtr.Zero;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff
                or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect:
                SetAway(ref _isLocked, true);
                break;
            case SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon
                or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect:
                SetAway(ref _isLocked, false);
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) SetAway(ref _isSuspended, true);
        else if (e.Mode == PowerModes.Resume) SetAway(ref _isSuspended, false);
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => SetAway(ref _isLocked, true);

    private void SetAway(ref bool flag, bool isAway)
    {
        if (flag == isAway) return;
        flag = isAway;
        if (!isAway) _quietUntil = DateTime.Now + QuietAfterReturn;
    }

    public void Announce(IReadOnlyList<Notice> notices)
    {
        if (notices.Count == 0 || IsAway) return;
        if (_store.State.IsSoundOn)
        {
            PlayChime();
        }
    }

    public void Test()
    {
        PlayChime();
    }

    public void PlayChime()
    {
        try
        {
            _chime?.Play();
        }
        catch
        {
            SystemSounds.Asterisk.Play();
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionEnding -= OnSessionEnding;
        if (_displayNotification != IntPtr.Zero) Native.UnregisterPowerSettingNotification(_displayNotification);
        _chime?.Dispose();
    }
}
