using System.Collections.Generic;

namespace RpgmvpConverterWinForms
{
    internal static class Loc
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>();
        private static string currentLang = "en";

        static Loc()
        {
            Dictionary<string, string> en = new Dictionary<string, string>();
            en["app_title"] = "RPGMVP/PNG_ -> PNG ULTRA";
            en["app_subtitle"] = "Fast RPGMVP to PNG conversion with live progress, ETA and task control";
            en["root_label"] = "Game Root";
            en["path_label"] = "Project folder";
            en["key_label"] = "HEX key";
            en["unlocker_label"] = "Renpy Gallery Unlocker";
            en["browse_btn"] = "...";
            en["game_root_btn"] = "G";
            en["start_btn"] = "Start";
            en["pause_btn"] = "Pause";
            en["resume_btn"] = "Resume";
            en["cancel_btn"] = "Cancel";
            en["status_waiting"] = "Waiting to start";
            en["status_preparing"] = "Preparing...";
            en["status_processing"] = "Processing";
            en["status_paused"] = "Paused";
            en["status_stopping"] = "Stopping...";
            en["status_done"] = "Done";
            en["status_cancelled"] = "Cancelled";
            en["stats_processed"] = "Processed";
            en["stats_speed"] = "Speed";
            en["stats_eta"] = "ETA";
            en["stats_files"] = "files";
            en["stats_fps"] = "f/s";
            en["log_header"] = "Log";
            en["msg_invalid_path"] = "Invalid path.";
            en["msg_key_not_found"] = "Key not found.";
            en["msg_key_too_short"] = "Key too short. Minimum 16 bytes in HEX required.";
            en["msg_no_files"] = "No .rpgmvp/.png_ files found.";
            en["msg_started"] = "Started files";
            en["msg_threads"] = "threads";
            en["msg_skip_tilesets"] = "Skipping: img/tilesets, img/weather";
            en["msg_paused"] = "Processing paused.";
            en["msg_pause_enabled"] = "Pause enabled.";
            en["msg_resumed"] = "Processing resumed.";
            en["msg_cancel_requested"] = "Stop requested.";
            en["msg_cancelled"] = "Operation cancelled. Already processed";
            en["msg_completed"] = "Completed";
            en["msg_files_processed"] = "files processed in";
            en["msg_errors"] = "Completed with errors";
            en["msg_last_error"] = "Last error";
            en["rpa_extract_btn"] = "Extract RPA";
            en["rpa_no_files"] = "No RPA files found.";
            en["rpa_found"] = "RPA files found";
            en["rpa_extracting"] = "Extracting RPA";
            en["rpa_complete"] = "RPA extraction complete";
            en["rpa_success"] = "Successfully extracted";
            en["rpa_errors"] = "RPA extraction completed with errors. Check log for details.";
            en["rpa_try_external"] = "Possibly RPA format not supported. Try rpaExtract.exe.";
            en["unlocker_btn"] = "Unlock Gallery";
            en["unlocker_soft"] = "Soft";
            en["unlocker_hard"] = "Hard";
            en["unlocker_installed"] = "Unlocker already installed. First remove _mods/ZLZK_UGU_* folder";
            en["unlocker_install_success"] = "Unlocker installed successfully!";
            en["unlocker_install_path"] = "Path";
            en["unlocker_remove_note"] = "Don't forget to remove the mod after use!";
            en["unlocker_install_error"] = "Unlocker installation error";
            en["folder_dialog_title"] = "Select project folder";
            en["game_root_not_found"] = "Game root not found in specified path.";
            en["game_root_detected"] = "Game root detected";
            en["key_found"] = "Key found";
            en["invalid_key"] = "Key must contain even number of HEX characters.";
            en["close_warning_title"] = "RPGMVP -> PNG ULTRA";
            en["close_warning_msg"] = "Please stop or wait for the operation to complete first.";
            en["unity_label"] = "Unity Asset Extractor";
            en["unity_mode_textures"] = "Textures";
            en["unity_mode_videos"] = "Videos";
            en["unity_mode_all"] = "All Assets";
            en["unity_extract_btn"] = "Extract Unity";
            en["clear_log_btn"] = "Clear";
            Strings["en"] = en;

            Dictionary<string, string> ru = new Dictionary<string, string>();
            ru["app_title"] = "RPGMVP/PNG_ -> PNG ULTRA";
            ru["app_subtitle"] = "Быстрая конвертация RPGMVP в PNG с живым прогрессом, ETA и управлением задачей";
            ru["root_label"] = "Корень";
            ru["path_label"] = "Папка проекта";
            ru["key_label"] = "HEX ключ";
            ru["unlocker_label"] = "Renpy Gallery Unlocker";
            ru["browse_btn"] = "...";
            ru["game_root_btn"] = "G";
            ru["start_btn"] = "Старт";
            ru["pause_btn"] = "Пауза";
            ru["resume_btn"] = "Продолжить";
            ru["cancel_btn"] = "Отмена";
            ru["status_waiting"] = "Ожидание запуска";
            ru["status_preparing"] = "Подготовка к обработке...";
            ru["status_processing"] = "Обработка";
            ru["status_paused"] = "Пауза";
            ru["status_stopping"] = "Останавливаю обработку...";
            ru["status_done"] = "Готово";
            ru["status_cancelled"] = "Остановлено";
            ru["stats_processed"] = "Обработано";
            ru["stats_speed"] = "Скорость";
            ru["stats_eta"] = "ETA";
            ru["stats_files"] = "ф./с";
            ru["stats_fps"] = "ф./с";
            ru["log_header"] = "Журнал";
            ru["msg_invalid_path"] = "Неверный путь.";
            ru["msg_key_not_found"] = "Ключ не найден.";
            ru["msg_key_too_short"] = "Ключ слишком короткий. Нужно минимум 16 байт в HEX.";
            ru["msg_no_files"] = "Файлы .rpgmvp/.png_ не найдены.";
            ru["msg_started"] = "Запущено файлов";
            ru["msg_threads"] = "Потоков";
            ru["msg_skip_tilesets"] = "Пропускаются: img/tilesets, img/weather";
            ru["msg_paused"] = "Обработка продолжена.";
            ru["msg_pause_enabled"] = "Пауза включена.";
            ru["msg_resumed"] = "Обработка возобновлена.";
            ru["msg_cancel_requested"] = "Запрошена остановка.";
            ru["msg_cancelled"] = "Операция отменена. Уже обработано";
            ru["msg_completed"] = "Завершено";
            ru["msg_files_processed"] = "файлов обработано за";
            ru["msg_errors"] = "Завершено с ошибками";
            ru["msg_last_error"] = "Последняя ошибка";
            ru["rpa_extract_btn"] = "Извлечь RPA";
            ru["rpa_no_files"] = "RPA файлы не найдены.";
            ru["rpa_found"] = "Найдено RPA файлов";
            ru["rpa_extracting"] = "Извлечение RPA";
            ru["rpa_complete"] = "Извлечение завершено";
            ru["rpa_success"] = "Успешно извлечено";
            ru["rpa_errors"] = "RPA извлечение завершено с ошибками. Проверьте лог для деталей.";
            ru["rpa_try_external"] = "Возможно формат RPA не поддерживается. Попробуйте rpaExtract.exe.";
            ru["unlocker_btn"] = "Unlock Gallery";
            ru["unlocker_soft"] = "Soft";
            ru["unlocker_hard"] = "Hard";
            ru["unlocker_installed"] = "Unlocker уже установлен. Сначала удалите папку _mods/ZLZK_UGU_*";
            ru["unlocker_install_success"] = "Unlocker успешно установлен!";
            ru["unlocker_install_path"] = "Путь";
            ru["unlocker_remove_note"] = "Не забудьте удалить мод после использования!";
            ru["unlocker_install_error"] = "Ошибка установки unlocker";
            ru["folder_dialog_title"] = "Выбор папки проекта";
            ru["game_root_not_found"] = "Корень игры не найден в указанном пути.";
            ru["game_root_detected"] = "Определён корень игры";
            ru["key_found"] = "Найден ключ";
            ru["invalid_key"] = "Ключ должен содержать чётное число HEX-символов.";
            ru["close_warning_title"] = "RPGMVP -> PNG ULTRA";
            ru["close_warning_msg"] = "Сначала остановите или дождитесь завершения обработки.";
            ru["unity_label"] = "Unity Asset Extractor";
            ru["unity_mode_textures"] = "Текстуры";
            ru["unity_mode_videos"] = "Видео";
            ru["unity_mode_all"] = "Все ассеты";
            ru["unity_extract_btn"] = "Извлечь Unity";
            ru["clear_log_btn"] = "Очистить";
            Strings["ru"] = ru;
        }

        public static string Get(string key)
        {
            Dictionary<string, string> langStrings;
            if (Strings.TryGetValue(currentLang, out langStrings))
            {
                string value;
                if (langStrings.TryGetValue(key, out value)) return value;
            }
            Dictionary<string, string> enStrings;
            if (Strings.TryGetValue("en", out enStrings))
            {
                string value;
                if (enStrings.TryGetValue(key, out value)) return value;
            }
            return key;
        }

        public static void SetLanguage(string lang)
        {
            if (Strings.ContainsKey(lang)) currentLang = lang;
        }

        public static string CurrentLanguage
        {
            get { return currentLang; }
        }

        public static string[] AvailableLanguages
        {
            get { return new string[] { "en", "ru" }; }
        }
    }
}
