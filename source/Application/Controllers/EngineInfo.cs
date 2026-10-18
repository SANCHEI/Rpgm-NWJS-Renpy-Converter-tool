using System.Drawing;

namespace RpgmvpConverterWinForms
{
    public sealed partial class RpgmvpConverterForm
    {
        private sealed class EngineInfo
        {
        public readonly GameEngine Engine;
        public readonly string Name;
        public readonly string OverrideLabel;
        public readonly string OutputFolder;
        public readonly string HintEn;
        public readonly string HintRu;
        public readonly string KeyLabelEn;
        public readonly string KeyLabelRu;
        public readonly int Argb;
        public readonly bool UsesRuntime;
        public readonly string[] RuntimePacks;
        public readonly string Markers;

        private EngineInfo(
            GameEngine engine,
            string name,
            string overrideLabel,
            string outputFolder,
            string hintEn,
            string hintRu,
            string keyLabelEn,
            string keyLabelRu,
            int argb,
            bool usesRuntime,
            string[] runtimePacks,
            string markers)
        {
            Engine = engine;
            Name = name;
            OverrideLabel = overrideLabel;
            OutputFolder = outputFolder;
            HintEn = hintEn;
            HintRu = hintRu;
            KeyLabelEn = keyLabelEn;
            KeyLabelRu = keyLabelRu;
            Argb = argb;
            UsesRuntime = usesRuntime;
            RuntimePacks = runtimePacks;
            Markers = markers;
        }

        private static EngineInfo Row(
            GameEngine engine, string name, string overrideLabel, string outputFolder,
            string hintEn, string hintRu, string keyLabelEn, string keyLabelRu,
            int a, int r, int g, int b, bool usesRuntime, string[] runtimePacks, string markers)
        {
            return new EngineInfo(engine, name, overrideLabel, outputFolder, hintEn, hintRu,
                keyLabelEn, keyLabelRu, (a << 24) | (r << 16) | (g << 8) | b,
                usesRuntime, runtimePacks, markers);
        }

        private static readonly string[] NoPacks = [];

        public static readonly EngineInfo[] All = [
            Row(GameEngine.Unknown, "not detected", null, "signature-recovery",
                "Unknown format. Recover embedded media by signatures or collect loose resources.",
                "Неизвестный формат. Извлеките встроенные медиа по сигнатурам или соберите открытые ресурсы.",
                null, null, 255, 155, 167, 181, false, NoPacks,
                "no strong engine markers found"),
            Row(GameEngine.RpgMaker, "RPG Maker MV/MZ", "RPG Maker MV/MZ", "rpgm",
                "Encrypted image assets. The key is detected automatically when possible.",
                "Зашифрованные изображения. Ключ определяется автоматически, когда это возможно.",
                "RPGM HEX key", "HEX-ключ RPGM", 255, 68, 197, 255, false, NoPacks,
                "RPG Maker markers: www/data and encrypted RPGM assets"),
            Row(GameEngine.Renpy, "Ren'Py", "Ren'Py", "renpy",
                "RPA archives will be extracted into separate folders.",
                "Архивы RPA будут извлечены в отдельные папки.",
                null, null, 255, 255, 105, 180, true, NoPacks,
                "Ren'Py markers: game folder and .rpa/.rpy resources"),
            Row(GameEngine.Unity, "Unity", "Unity", "unity",
                "Unity extraction follows the selected Extract profile, including Text only for TextAsset/localization files.",
                "Unity извлекается по выбранному профилю Extract, включая «Только текст» для TextAsset/локализаций.",
                null, null, 255, 160, 125, 255, true, [PortableRuntime.PackUnity],
                "Unity markers: *_Data folder, .assets/.bundle/.unity3d or Unity player files"),
            Row(GameEngine.Godot, "Godot", "Godot", "godot",
                "PCK extraction. For encrypted archives, enter the key or keep the field empty for automatic discovery.",
                "Извлечение PCK. Для зашифрованных архивов укажите ключ или оставьте поле пустым для автопоиска.",
                "Godot PCK key", "Ключ Godot PCK", 255, 71, 140, 191, true, NoPacks,
                "Godot markers: .pck, project.godot or embedded PCK executable"),
            Row(GameEngine.Kirikiri, "KiriKiri XP3", "KiriKiri XP3", "kirikiri",
                "Standard XP3 extraction. Supported TLG5 images also receive PNG previews.",
                "Извлечение стандартных XP3. Для поддерживаемых изображений TLG5 также создаются PNG-превью.",
                null, null, 255, 255, 155, 95, true, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Unreal, "Unreal experimental", "Unreal PAK", "unreal",
                "Experimental PAK extraction with AES key discovery and Zlib, Gzip, LZ4, Zstd or Oodle decoding. Oodle has a built-in open-source fallback; a local official DLL is preferred when available. IoStore is reported.",
                "Экспериментальное извлечение PAK с поиском AES-ключа и распаковкой Zlib, Gzip, LZ4, Zstd или Oodle. Для Oodle встроен open-source fallback; локальная официальная DLL используется при наличии. IoStore отмечается в отчёте.",
                "Unreal AES key", "AES-ключ Unreal", 255, 178, 178, 190, true, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Nwjs, "NWJS", "NWJS", "nwjs",
                "NWJS files will be copied. ZIP-compatible package.nw archives will be unpacked.",
                "Файлы NWJS будут скопированы. ZIP-совместимые архивы package.nw будут распакованы.",
                null, null, 255, 255, 183, 77, false, NoPacks,
                "NWJS markers: package.json/www or browser game files"),
            Row(GameEngine.WolfRpg, "WOLF RPG", "WOLF RPG", "wolf",
                "WOLF archives use the embedded UberWolf CLI. Loose Data files are copied too.",
                "Архивы WOLF извлекаются встроенным UberWolf CLI. Открытые файлы Data также копируются.",
                null, null, 255, 110, 205, 150, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.TyranoScript, "TyranoScript", "TyranoScript", "tyrano",
                "TyranoScript project data will be copied with its original folder structure.",
                "Данные проекта TyranoScript будут скопированы с исходной структурой папок.",
                null, null, 255, 255, 140, 190, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.JavaJar, "Java game / JAR", "Java / JAR", "java",
                "Choose fast images-only, cached SVG previews or all resources.",
                "Выберите быстрые изображения, кэшируемые PNG-превью SVG или все ресурсы.",
                null, null, 255, 235, 155, 75, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Flash, "Flash SWF experimental", "Flash SWF", "flash",
                "Experimental Flash inspection copies SWF files and extracts embedded JPEG, PNG, GIF and FLV video.",
                "Экспериментальный анализ Flash копирует SWF и извлекает встроенные JPEG, PNG, GIF и FLV-видео.",
                null, null, 255, 225, 80, 75, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Electron, "Electron ASAR", "Electron ASAR", "electron",
                "Electron app.asar files will be unpacked with archive safety limits.",
                "Файлы Electron app.asar будут распакованы с защитными лимитами архива.",
                null, null, 255, 105, 190, 210, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Html, "HTML game", "HTML", "html",
                "Static HTML game files and open media resources will be collected with their folder structure.",
                "Файлы статической HTML-игры и открытые медиа будут собраны с сохранением структуры.",
                null, null, 255, 85, 180, 235, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Qsp, "QSP", "QSP", "qsp",
                "QSP databases and open media resources will be collected without copying the bundled player.",
                "Базы QSP и открытые медиа будут собраны без копирования встроенного проигрывателя.",
                null, null, 255, 205, 165, 85, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.Rags, "RAGS experimental", "RAGS", "rags",
                "Experimental RAGS recovery copies the encrypted database and carves confidently detected embedded media.",
                "Экспериментальное восстановление RAGS копирует зашифрованную базу и извлекает уверенно найденные медиа.",
                null, null, 255, 190, 120, 210, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.LegacyRpgMaker, "RPG Maker XP/VX/VX Ace", "RPG Maker XP/VX", "rgss",
                "RGSSAD, RGSS2A and RGSS3A archives will be extracted by the built-in parser.",
                "Архивы RGSSAD, RGSS2A и RGSS3A будут извлечены встроенным parser-ом.",
                null, null, 255, 85, 190, 240, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.GameMaker, "GameMaker experimental", "GameMaker", "gamemaker",
                "Experimental data.win recovery preserves the original and extracts PNG, QOI and BZ2QOI texture pages.",
                "Экспериментальное восстановление data.win сохраняет оригинал и извлекает страницы текстур PNG, QOI и BZ2QOI.",
                null, null, 255, 100, 200, 190, true, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.AndroidApk, "Android APK recovery", "Android APK", "android-apk",
                "APK files are unpacked as ZIP containers and scanned for embedded assets, including GPU texture containers.",
                "APK распаковывается как ZIP-контейнер и сканируется на встроенные ресурсы, включая контейнеры GPU-текстур.",
                null, null, 255, 120, 205, 100, false, NoPacks,
                "APK file input"),
            Row(GameEngine.SrpgStudio, "SRPG Studio recovery", "SRPG Studio", "srpg-studio",
                "SRPG Studio resources are collected, and .rts/.dts/.srk containers are scanned by signatures.",
                "Ресурсы SRPG Studio собираются, а контейнеры .rts/.dts/.srk сканируются по сигнатурам.",
                null, null, 255, 210, 180, 90, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.PixelGameMaker, "Pixel Game Maker MV recovery", "Pixel Game Maker", "pixel-game-maker",
                "Pixel Game Maker MV open resources are collected, encrypted or packed data is scanned by signatures.",
                "Открытые ресурсы Pixel Game Maker MV собираются, зашифрованные или упакованные данные сканируются по сигнатурам.",
                null, null, 255, 110, 185, 235, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.SpakDat, "SPAK DAT experimental", "SPAK DAT", "spak-dat",
                "Experimental SPAK DAT / SPITE extraction decodes resources whose runtime paths can be recovered. Remaining protected blocks stay as .dat files in protected/.",
                "Экспериментальное извлечение SPAK DAT / SPITE декодирует ресурсы с восстановленными runtime-путями. Остальные защищённые блоки остаются .dat-файлами в protected/.",
                null, null, 255, 195, 150, 95, true, [PortableRuntime.PackSpite],
                "engine-specific markers found"),
            Row(GameEngine.PygamePyInstaller, "Pygame / PyInstaller", "Pygame / PyInstaller", "pygame",
                "Pygame/PyInstaller assets will be collected. XOR-obfuscated image .dat files are decoded when recognized.",
                "Ресурсы Pygame/PyInstaller будут собраны. XOR-зашифрованные изображения .dat декодируются при распознавании.",
                null, null, 255, 95, 190, 130, false, NoPacks,
                "engine-specific markers found"),
            Row(GameEngine.DataArc, "Data ARC", "Data ARC", "arc",
                "Data ARC containers are extracted with original folder structure preserved.",
                "Контейнеры Data ARC извлекаются с сохранением исходной структуры папок.",
                null, null, 255, 120, 170, 220, false, NoPacks,
                "Data ARC markers: .arc container with file table"),
            Row(GameEngine.Hsp, "HSP DPM experimental", "HSP DPM", "hsp",
                "HSP DPM contents are listed with loose files collected. DPM2 file data uses a closed cipher and stays inside the archive.",
                "Содержимое HSP DPM перечисляется, открытые файлы собираются. Данные файлов DPM2 закрыты шифром и остаются в архиве.",
                null, null, 255, 130, 200, 160, false, NoPacks,
                "HSP markers: .dpm containers or hsp DLLs"),
        ];

        public static EngineInfo For(GameEngine engine)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Engine == engine) return All[i];
            return All[0];
        }
    }
}
}
