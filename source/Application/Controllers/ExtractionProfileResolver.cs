using System;

namespace RpgmvpConverterWinForms
{
    internal sealed class ExtractionProfileModes
    {
        public ExtractionProfileModes(ExtractionProfile profile, string unityMode, string javaMode, string looseMode)
        {
            Profile = profile;
            UnityMode = unityMode;
            JavaMode = javaMode;
            LooseMode = looseMode;
        }

        public ExtractionProfile Profile { get; private set; }
        public string UnityMode { get; private set; }
        public string JavaMode { get; private set; }
        public string LooseMode { get; private set; }
    }

    internal static class ExtractionProfileResolver
    {
        public static ExtractionProfileModes Resolve(int profileIndex, string displayName, int javaModeIndex, int looseModeIndex)
        {
            ExtractionProfile profile = ExtractionProfile.FromIndex(profileIndex, displayName);
            return new ExtractionProfileModes(
                profile,
                profile.UnityMode(0),
                profile.JavaMode(javaModeIndex),
                profile.LooseMode(looseModeIndex));
        }
    }
}
