using System;
using System.IO;
using System.Text;

namespace RpgmvpConverterWinForms
{
    internal static class ApplicationErrorLog
    {
        public static string LogPath
        {
            get
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GameAssetTool");
                return Path.Combine(folder, "GameAssetTool-last-error.log");
            }
        }

        public static void Write(Exception ex)
        {
            if (ex == null) return;
            try
            {
                string folder = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("Game Asset Tool last error");
                builder.AppendLine("Created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine("Version: " + typeof(ApplicationErrorLog).Assembly.GetName().Version);
                builder.AppendLine();
                builder.AppendLine(ex.ToString());
                File.WriteAllText(LogPath, builder.ToString(), new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }
}
