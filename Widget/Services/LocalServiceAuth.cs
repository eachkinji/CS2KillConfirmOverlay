using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Web.Http;

namespace KillConfirmGameBar.Services
{
    internal static class LocalServiceAuth
    {
        private const string HeaderName = "x-killconfirm-token";
        private const string TokenFileName = "service-auth-token.txt";
        private const int TokenReadRetries = 20;

        private static readonly SemaphoreSlim TokenGate = new SemaphoreSlim(1, 1);
        private static string _cachedToken;

        public static async Task<HttpClient> CreateHttpClientAsync()
        {
            string token = await GetTokenAsync();
            var client = new HttpClient();
            if (!client.DefaultRequestHeaders.TryAppendWithoutValidation(HeaderName, token))
            {
                client.Dispose();
                throw new InvalidOperationException("Failed to attach local service authentication.");
            }

            return client;
        }

        public static async Task AuthenticateWebSocketAsync(MessageWebSocket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            socket.SetRequestHeader(HeaderName, await GetTokenAsync());
        }

        public static void InvalidateCachedToken()
        {
            Interlocked.Exchange(ref _cachedToken, null);
        }

        private static async Task<string> GetTokenAsync()
        {
            if (IsValidToken(_cachedToken))
            {
                return _cachedToken;
            }

            await TokenGate.WaitAsync();
            try
            {
                if (IsValidToken(_cachedToken))
                {
                    return _cachedToken;
                }

                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile tokenFile = null;
                try
                {
                    tokenFile = await folder.CreateFileAsync(
                        TokenFileName,
                        CreationCollisionOption.FailIfExists);
                    string newToken = CreateToken();
                    await FileIO.WriteTextAsync(tokenFile, newToken);
                    _cachedToken = newToken;
                    return newToken;
                }
                catch (Exception)
                {
                    tokenFile = await folder.GetFileAsync(TokenFileName);
                }

                for (int attempt = 0; attempt < TokenReadRetries; attempt++)
                {
                    string existingToken = (await FileIO.ReadTextAsync(tokenFile)).Trim();
                    if (IsValidToken(existingToken))
                    {
                        _cachedToken = existingToken;
                        return existingToken;
                    }

                    await Task.Delay(25);
                }

                string repairedToken = CreateToken();
                await FileIO.WriteTextAsync(tokenFile, repairedToken);
                _cachedToken = repairedToken;
                return repairedToken;
            }
            finally
            {
                TokenGate.Release();
            }
        }

        private static string CreateToken()
        {
            return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        }

        private static bool IsValidToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 32)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool isHex = (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
