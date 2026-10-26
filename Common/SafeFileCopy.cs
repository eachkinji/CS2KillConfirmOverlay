using System;
using System.IO;

namespace KillConfirmCompatibility.Contracts
{
    // File.Copy keeps the EFS encrypted attribute on the destination. When the
    // destination cannot be encrypted it fails with ERROR_ENCRYPTION_FAILED,
    // which used to abort legacy migration on encrypted package data.
    public static class SafeFileCopy
    {
        public static void Copy(string source, string target, bool overwrite = true)
        {
            if ((File.GetAttributes(source) & FileAttributes.Encrypted) == 0)
            {
                File.Copy(source, target, overwrite);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target)));
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var output = new FileStream(target, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None))
                input.CopyTo(output);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
        }
    }
}
