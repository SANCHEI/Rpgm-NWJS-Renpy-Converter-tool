using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class EmbeddedScripts
    {
        public static string ReadText(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException("Embedded script not found: " + resourceName);

                using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    return reader.ReadToEnd();
            }
        }
    }
}
