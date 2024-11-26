using System.Text;

namespace GoodAI.Core.Diagnostics
{
    public class ProcessStartInfo
    {
        public string   FileName               { get; set; }
        public string   WorkingDirectory       { get; set; }
        public bool     UseShellExecute        { get; set; }
        public bool     CreateNoWindow         { get; set; }
        public bool     RedirectStandardOutput { get; set; }
        public Encoding StandardOutputEncoding { get; set; }
        public bool     RedirectStandardError  { get; set; }
        public Encoding StandardErrorEncoding  { get; set; }
        public string   Arguments              { get; set; }

        public ProcessStartInfo()
        {
            UseShellExecute = true;
        }

        public ProcessStartInfo(string fileName) : this()
        {
            FileName = fileName;
        }

        public ProcessStartInfo(string fileName, string arguments) : this(fileName)
        {
            Arguments = arguments;
        }
    }
}
