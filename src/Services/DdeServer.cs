using System.Runtime.InteropServices;
using System.Text;

namespace UrlRouter.Services
{
    /// <summary>
    /// A minimal DDE server, which is how this tool survives Attack Surface Reduction.
    ///
    /// Some clients managed policy enables the ASR rule "Block Office communication application
    /// from creating child processes" (26190899-1602-49e8-8b27-eb1d0a1ce869), so Windows
    /// refuses to let Outlook start UrlRouter.exe at all - the click dies with "Windows
    /// cannot access the specified device, path, or file" and nothing reaches our code.
    ///
    /// ShellExecute has an older path that avoids this entirely: if the ProgId carries a
    /// `ddeexec` key and a DDE server with the matching service name is *already running*,
    /// the shell delivers the URL over a DDE conversation instead of launching anything.
    /// No child process is created, so the ASR rule never fires. The browser is then started
    /// by the resident agent, which is not an Office process and is not restricted.
    ///
    /// Consequence worth remembering: this only works while the agent is running. If it is
    /// not, the shell falls back to the `command` line and the click is blocked again -
    /// which is why registration also adds an autostart entry.
    /// </summary>
    public sealed class DdeServer : IDisposable
    {
        // Service and topic must match the ddeexec keys written by RegistrationService.
        public const string ServiceName = "UrlRouter";
        public const string TopicName = "System";

        private const int DmlErrNoError = 0;
        private const int CpWinUnicode = 1200;

        private const uint AppClassStandard = 0x00000000;
        private const uint CbfSkipRegistrations = 0x00080000;
        private const uint CbfSkipUnregistrations = 0x00100000;
        private const uint CbfSkipConnectConfirms = 0x00040000;
        private const uint CbfFailPokes = 0x00010000;
        private const uint CbfFailAdvises = 0x00004000;

        private const uint XtypConnect = 0x1062;
        private const uint XtypExecute = 0x4050;
        private const uint XtypRequest = 0x20B0;

        private const uint DdeFack = 0x8000;
        private const uint DnsRegister = 0x0001;
        private const uint DnsUnregister = 0x0002;

        private delegate IntPtr DdeCallbackDelegate(
            uint type, uint format, IntPtr conversation,
            IntPtr hsz1, IntPtr hsz2, IntPtr data, IntPtr data1, IntPtr data2);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int DdeInitializeW(ref uint pidInst, DdeCallbackDelegate callback,
            uint afCmd, uint ulRes);

        [DllImport("user32.dll")]
        private static extern bool DdeUninitialize(uint idInst);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DdeCreateStringHandleW(uint idInst, string psz, int codePage);

        [DllImport("user32.dll")]
        private static extern bool DdeFreeStringHandle(uint idInst, IntPtr hsz);

        [DllImport("user32.dll")]
        private static extern IntPtr DdeNameService(uint idInst, IntPtr hsz1, IntPtr hsz2, uint afCmd);

        [DllImport("user32.dll")]
        private static extern uint DdeGetData(IntPtr hData, byte[]? dst, uint cbMax, uint cbOff);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DdeQueryStringW(uint idInst, IntPtr hsz, StringBuilder? psz,
            uint cchMax, int codePage);

        private readonly Action<string> _onUrl;

        // Held in a field so the GC cannot collect the delegate while Windows holds the
        // native pointer to it.
        private readonly DdeCallbackDelegate _callback;

        private uint _instance;
        private IntPtr _serviceHandle;
        private bool _disposed;

        public DdeServer(Action<string> onUrl)
        {
            _onUrl = onUrl;
            _callback = DdeCallback;
        }

        public bool IsRunning { get; private set; }

        /// <summary>
        /// Registers the DDE service. Must be called on a thread with a running message
        /// pump - DDEML is built on window messages.
        /// </summary>
        public bool Start()
        {
            var filters = AppClassStandard
                          | CbfSkipRegistrations
                          | CbfSkipUnregistrations
                          | CbfSkipConnectConfirms
                          | CbfFailPokes
                          | CbfFailAdvises;

            var result = DdeInitializeW(ref _instance, _callback, filters, 0);
            if (result != DmlErrNoError)
            {
                RouterLog.Write($"DDE initialise failed (0x{result:X})");
                return false;
            }

            _serviceHandle = DdeCreateStringHandleW(_instance, ServiceName, CpWinUnicode);
            if (_serviceHandle == IntPtr.Zero)
            {
                RouterLog.Write("DDE service name handle could not be created");
                return false;
            }

            if (DdeNameService(_instance, _serviceHandle, IntPtr.Zero, DnsRegister) == IntPtr.Zero)
            {
                RouterLog.Write("DDE service registration was refused");
                return false;
            }

            IsRunning = true;
            RouterLog.Write($"DDE server listening as {ServiceName}|{TopicName}");
            return true;
        }

        private IntPtr DdeCallback(uint type, uint format, IntPtr conversation,
            IntPtr hsz1, IntPtr hsz2, IntPtr data, IntPtr data1, IntPtr data2)
        {
            switch (type)
            {
                case XtypConnect:
                    // hsz1 is the topic. Accept any topic: some callers use "System", others
                    // the browser-era "WWW_OpenURL", and refusing the conversation would send
                    // the shell straight back to launching a process.
                    return new IntPtr(1);

                case XtypExecute:
                    HandleExecute(data);
                    return new IntPtr(DdeFack);

                case XtypRequest:
                    // Some clients probe with a request before executing; an empty answer is
                    // enough to keep the conversation alive.
                    return IntPtr.Zero;

                default:
                    return IntPtr.Zero;
            }
        }

        private void HandleExecute(IntPtr data)
        {
            try
            {
                var command = ReadString(data);
                if (string.IsNullOrWhiteSpace(command)) return;

                var url = ExtractUrl(command);
                if (url is null)
                {
                    RouterLog.Write($"DDE command not understood: {command}");
                    return;
                }

                _onUrl(url);
            }
            catch (Exception ex)
            {
                RouterLog.Write($"DDE execute failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads a DDE data handle as text. The encoding depends on how the *client*
        /// initialised DDEML, so both are tried rather than assumed.
        /// </summary>
        private static string? ReadString(IntPtr data)
        {
            var size = DdeGetData(data, null, 0, 0);
            if (size == 0) return null;

            var buffer = new byte[size];
            DdeGetData(data, buffer, size, 0);

            var unicode = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
            if (unicode.Contains('[')) return unicode;

            var ansi = Encoding.Default.GetString(buffer).TrimEnd('\0');
            return ansi.Contains('[') ? ansi : unicode;
        }

        /// <summary>
        /// Pulls the URL out of the shell's command string. RegistrationService registers
        /// `[open("%1")]`, so the payload arrives as [open("https://...")], but the classic
        /// browser form `"url",,-1,0` is accepted too in case the shell uses it.
        /// </summary>
        public static string? ExtractUrl(string command)
        {
            var first = command.IndexOf('"');
            if (first < 0) return null;

            var last = command.IndexOf('"', first + 1);
            if (last <= first) return null;

            var candidate = command[(first + 1)..last].Trim();
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_instance == 0) return;

                if (_serviceHandle != IntPtr.Zero)
                {
                    DdeNameService(_instance, _serviceHandle, IntPtr.Zero, DnsUnregister);
                    DdeFreeStringHandle(_instance, _serviceHandle);
                    _serviceHandle = IntPtr.Zero;
                }

                DdeUninitialize(_instance);
                _instance = 0;
                IsRunning = false;
            }
            catch (Exception)
            {
                // Nothing useful to do while shutting down.
            }
        }
    }
}
