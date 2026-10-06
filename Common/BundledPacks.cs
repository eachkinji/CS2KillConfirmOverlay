using System;
using System.IO;
namespace KillConfirmCompatibility.Contracts
{
    public static class BundledPacks
    {
        public const string CrossfireRootKey="Crossfire.BundledRoot";
        public static void Register(FileSettings settings)
        {
            string root=Path.Combine(RuntimePaths.InstallRoot,"DefaultPacks","crossfire");
            if(!Equals(settings[CrossfireRootKey],root)) settings[CrossfireRootKey]=root;
        }
    }
}
