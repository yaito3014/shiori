using System;
using System.IO;

namespace Shiori.Tests
{
    /// <summary>A throw-away directory under the system temp folder; removes read-only git objects on dispose.</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory(string prefix = "shiori-test")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string File(string relativePath)
        {
            return System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        public void WriteText(string relativePath, string content)
        {
            var full = File(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            System.IO.File.WriteAllText(full, content);
        }

        public string ReadText(string relativePath)
        {
            return System.IO.File.ReadAllText(File(relativePath));
        }

        public void Dispose()
        {
            try
            {
                ClearReadOnly(new DirectoryInfo(Path));
                Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // Leave it for the OS temp cleaner.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void ClearReadOnly(DirectoryInfo dir)
        {
            foreach (var info in dir.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if ((info.Attributes & FileAttributes.ReadOnly) != 0) info.Attributes &= ~FileAttributes.ReadOnly;
            }
        }
    }
}
