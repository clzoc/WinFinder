namespace WinFinder.Thumbnailer.Worker.Common {
    public sealed class ExternalProcessResult {
        public int ExitCode { get; set; }
        public bool Started { get; set; }
        public bool TimedOut { get; set; }
        public long ElapsedMs { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public string Diagnostic { get; set; }
    }
}
