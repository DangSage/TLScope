using System;

namespace TLScope.Utilities;

/// <summary>
/// Manages console state including alternate screen buffer and cursor visibility.
/// Ensures the terminal is properly restored to its original state on exit.
/// </summary>
public class ConsoleStateManager : IDisposable
{
    private bool _isAlternateBufferActive = false;
    private bool _originalCursorVisible;
    private bool _cursorVisibilitySupported = false;
    private bool _disposed = false;

    private const string ENTER_ALTERNATE_BUFFER = "\x1b[?1049h";
    private const string EXIT_ALTERNATE_BUFFER = "\x1b[?1049l";
    private const string SHOW_CURSOR = "\x1b[?25h";
    private const string HIDE_CURSOR = "\x1b[?25l";
    private const string RESET_COLORS = "\x1b[0m";

    /// <summary>
    /// Initializes console state management but does not enter alternate buffer.
    /// Call EnterAlternateBuffer() explicitly when ready.
    /// </summary>
    public ConsoleStateManager()
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            _originalCursorVisible = Console.CursorVisible;
#pragma warning restore CA1416
            _cursorVisibilitySupported = true;
        }
        catch (PlatformNotSupportedException)
        {
            _cursorVisibilitySupported = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Failed to get cursor visibility state");
            _cursorVisibilitySupported = false;
        }
    }

    /// <summary>
    /// Enters the alternate screen buffer (like vim/less).
    /// The current terminal content is preserved and will be restored on exit.
    /// </summary>
    public void EnterAlternateBuffer()
    {
        if (_isAlternateBufferActive)
            return;

        try
        {
            Console.Write(ENTER_ALTERNATE_BUFFER);
            Console.Out.Flush();
            _isAlternateBufferActive = true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to enter alternate screen buffer");
        }
    }

    /// <summary>
    /// Exits the alternate screen buffer and restores the previous terminal content.
    /// </summary>
    public void ExitAlternateBuffer()
    {
        if (!_isAlternateBufferActive)
            return;

        try
        {
            Console.Write(RESET_COLORS);
            Console.Write(SHOW_CURSOR);
            Console.Write(EXIT_ALTERNATE_BUFFER);
            Console.Out.Flush();
            _isAlternateBufferActive = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to exit alternate screen buffer");
        }
    }

    /// <summary>
    /// Restores the cursor visibility to its original state.
    /// </summary>
    public void RestoreCursorVisibility()
    {
        if (!_cursorVisibilitySupported)
        {
            try
            {
                Console.Write(SHOW_CURSOR);
                Console.Out.Flush();
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Failed to restore cursor using ANSI escape sequence");
            }
            return;
        }

        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            Console.CursorVisible = _originalCursorVisible;
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to restore cursor visibility");
        }
    }

    /// <summary>
    /// Performs a full cleanup: exits alternate buffer and restores cursor.
    /// </summary>
    public void Cleanup()
    {
        ExitAlternateBuffer();
        RestoreCursorVisibility();
    }

    /// <summary>
    /// IDisposable implementation - ensures cleanup happens.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Cleanup();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure cleanup even if Dispose is not called.
    /// </summary>
    ~ConsoleStateManager()
    {
        if (!_disposed)
        {
            try
            {
                Console.Write(EXIT_ALTERNATE_BUFFER);
                Console.Write(SHOW_CURSOR);
                Console.Out.Flush();
            }
            catch
            {
            }
        }
    }
}
