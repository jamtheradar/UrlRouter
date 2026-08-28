using System.Runtime.InteropServices;
using System.Text;

namespace UrlRouter.Services
{
    /// <summary>
    /// Lets the diagnostic switches (--test, --register) print to whatever launched them.
    ///
    /// The app is a WinExe so that clicking a link never flashes a console window, which
    /// also means it starts with no console of its own. Three cases have to be handled:
    ///
    ///   1. stdout is already redirected (a pipe or a file) - write to it directly, so
    ///      `UrlRouter --test ... > out.txt` and piping both work.
    ///   2. launched from a terminal - borrow the parent's console.
    ///   3. no console at all (double-clicked) - fall back to a message box.
    /// </summary>
    public static class ConsoleOutput
    {
        private const int AttachParentProcess = -1;
        private const int StdOutputHandle = -11;

        private const uint FileTypeDisk = 0x0001;
        private const uint FileTypePipe = 0x0003;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileType(IntPtr hFile);

        private static bool _canWrite;
        private static bool _initialised;
        private static readonly StringBuilder Buffered = new();

        private static void Initialise()
        {
            if (_initialised) return;
            _initialised = true;

            try
            {
                if (IsStdOutRedirected())
                {
                    // Already pointed somewhere useful - leave Console.Out alone.
                    _canWrite = true;
                    return;
                }

                _canWrite = AttachConsole(AttachParentProcess);
                if (_canWrite)
                {
                    Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                }
            }
            catch (Exception)
            {
                _canWrite = false;
            }
        }

        /// <summary>
        /// True when stdout is a pipe or a file rather than a console (or nothing at all).
        /// Checked via the Win32 handle because Console.IsOutputRedirected is unreliable for
        /// a GUI-subsystem process that has no console yet.
        /// </summary>
        private static bool IsStdOutRedirected()
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return false;

            var type = GetFileType(handle);
            return type == FileTypePipe || type == FileTypeDisk;
        }

        public static void WriteLine(string text = "")
        {
            Initialise();
            Buffered.AppendLine(text);

            if (_canWrite) Console.WriteLine(text);
        }

        /// <summary>
        /// Call once before exiting. Shows everything written so far in a dialog when there
        /// was nowhere to print, so output is never silently lost.
        /// </summary>
        public static void Flush(string caption)
        {
            Initialise();

            if (_canWrite)
            {
                Console.Out.Flush();
                return;
            }

            if (Buffered.Length == 0) return;

            MessageBox.Show(Buffered.ToString(), caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
