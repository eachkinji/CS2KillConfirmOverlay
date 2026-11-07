using System;
using System.IO;
using System.Threading.Tasks;

namespace KillConfirmCompatibility.Validation;

internal static class AtomicDiagnosticMarker
{
    // Progress readers should open with FileShare.ReadWrite | FileShare.Delete.
    // Tolerate a short-lived incompatible reader without skipping a measurement;
    // persistent permission / I/O failures still propagate after 8 attempts.
    internal static async Task WriteAsync(string file, string contents)
    {
        const int attempts = 8;
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.WriteAllText(file + ".tmp", contents);
                File.Move(file + ".tmp", file, true);
                return;
            }
            catch (IOException) when (attempt < attempts - 1)
            {
                await Task.Delay(20 * (attempt + 1)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts - 1)
            {
                await Task.Delay(20 * (attempt + 1)).ConfigureAwait(false);
            }
        }
    }
}
