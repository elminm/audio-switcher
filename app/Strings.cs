// UI strings: English (default), Azerbaijani, Russian.
using System;
using System.Collections.Generic;

namespace AudioSwitcher
{
    static class L
    {
        public static readonly string[] Codes = { "en", "az", "ru" };
        public static readonly string[] Names = { "English", "Azərbaycanca", "Русский" };
        static int idx;

        public static string Lang
        {
            get { return Codes[idx]; }
            set { idx = Math.Max(0, Array.IndexOf(Codes, (value ?? "en").ToLowerInvariant())); }
        }
        public static int Index { get { return idx; } }

        public static string T(string key, params object[] args)
        {
            string[] v;
            if (!D.TryGetValue(key, out v)) return key;
            string s = v[idx] ?? v[0];
            return args.Length > 0 ? string.Format(s, args) : s;
        }

        //                                                    English                                   Azərbaycanca                                   Русский
        static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>
        {
            { "autoSwitch",       new[] { "Auto switch (by priority)",              "Avtomatik keçid (prioritetə görə)",           "Автопереключение (по приоритету)" } },
            { "autoSwitchShort",  new[] { "Auto switch",                            "Avtomatik keçid",                             "Автопереключение" } },
            { "startWithWindows", new[] { "Start with Windows",                     "Windows ilə başlasın",                        "Запускать вместе с Windows" } },
            { "language",         new[] { "Language:",                              "Dil:",                                        "Язык:" } },
            { "output",           new[] { "Output",                                 "Output",                                      "Вывод" } },
            { "input",            new[] { "Input",                                  "Input",                                       "Ввод" } },
            { "outputGroup",      new[] { "Output priority (playback)",             "Output prioriteti (səs çıxışı)",              "Приоритет вывода (воспроизведение)" } },
            { "inputGroup",       new[] { "Input priority (microphone)",            "Input prioriteti (mikrofon)",                 "Приоритет ввода (микрофон)" } },
            { "btGroup",          new[] { "Bluetooth audio devices",                "Bluetooth audio cihazları",                   "Аудиоустройства Bluetooth" } },
            { "logGroup",         new[] { "Log",                                    "Jurnal",                                      "Журнал" } },
            { "colDevice",        new[] { "Device",                                 "Cihaz",                                       "Устройство" } },
            { "colStatus",        new[] { "Status",                                 "Status",                                      "Статус" } },
            { "colType",          new[] { "Type",                                   "Növ",                                         "Тип" } },
            { "btnUp",            new[] { "▲  Up",                                  "▲  Yuxarı",                                   "▲  Вверх" } },
            { "btnDown",          new[] { "▼  Down",                                "▼  Aşağı",                                    "▼  Вниз" } },
            { "btnAdd",           new[] { "+  Add",                                 "+  Əlavə et",                                 "+  Добавить" } },
            { "btnRemove",        new[] { "✕  Remove",                              "✕  Sil",                                      "✕  Удалить" } },
            { "btnUseNow",        new[] { "Use now",                                "İndi seç",                                    "Выбрать" } },
            { "btnConnect",       new[] { "Connect",                                "Qoş",                                         "Подключить" } },
            { "btnDisconnect",    new[] { "Disconnect",                             "Ayır",                                        "Отключить" } },
            { "btnRefresh",       new[] { "Refresh",                                "Yenilə",                                      "Обновить" } },
            { "stActive",         new[] { "● Active",                               "● Aktiv",                                     "● Активно" } },
            { "stConnected",      new[] { "Connected",                              "Qoşulu",                                      "Подключено" } },
            { "stBtConnected",    new[] { "● Connected",                            "● Qoşulu",                                    "● Подключено" } },
            { "stHeadsetOff",     new[] { "Headset off",                            "Qulaqlıq sönülü",                             "Гарнитура выкл." } },
            { "stDisconnected",   new[] { "Disconnected",                           "Qoşulu deyil",                                "Отключено" } },
            { "stNotFound",       new[] { "Not found",                              "Tapılmadı",                                   "Не найдено" } },
            { "kindBoth",         new[] { "Audio + mic",                            "Səs + mikrofon",                              "Звук + микрофон" } },
            { "kindOut",          new[] { "Audio",                                  "Səs",                                         "Звук" } },
            { "kindIn",           new[] { "Microphone",                             "Mikrofon",                                    "Микрофон" } },
            { "kindAudio",        new[] { "Audio",                                  "Audio",                                       "Аудио" } },
            { "noBt",             new[] { "(no Bluetooth audio devices)",           "(Bluetooth audio cihazı yoxdur)",             "(нет Bluetooth-аудиоустройств)" } },
            { "hxLabel",          new[] { "HyperX headset: {0}",                    "HyperX qulaqlıq: {0}",                        "Гарнитура HyperX: {0}" } },
            { "hxOn",             new[] { "on",                                     "açıq",                                        "вкл." } },
            { "hxOff",            new[] { "off",                                    "sönülü",                                      "выкл." } },
            { "hxNone",           new[] { "dongle not found",                       "dongle tapılmadı",                            "донгл не найден" } },
            { "matchTip",         new[] { "Match: \"{0}\"",                         "Axtarış: \"{0}\"",                            "Поиск: \"{0}\"" } },
            { "trayOpen",         new[] { "Open",                                   "Aç",                                          "Открыть" } },
            { "trayExit",         new[] { "Exit",                                   "Çıxış",                                       "Выход" } },
            { "trayNoDevices",    new[] { "(no devices)",                           "(cihaz yoxdur)",                              "(нет устройств)" } },
            { "addNotConnected",  new[] { "   (not connected)",                     "   (qoşulu deyil)",                           "   (не подключено)" } },
            { "addAllListed",     new[] { "(all devices are already in the list)",  "(bütün cihazlar artıq siyahıdadır)",          "(все устройства уже в списке)" } },
            { "logStarted",       new[] { "Started",                                "Başladı",                                     "Запущено" } },
            { "logCfgReadErr",    new[] { "Could not read config.json: {0}",        "config.json oxunmadı: {0}",                   "Не удалось прочитать config.json: {0}" } },
            { "logCfgWriteErr",   new[] { "Could not save config.json: {0}",        "config.json yazılmadı: {0}",                  "Не удалось сохранить config.json: {0}" } },
            { "logCfgInvalid",    new[] { "config.json is invalid, keeping previous settings: {0}", "config.json xətalıdır, köhnə ayarlar saxlanıldı: {0}", "Ошибка в config.json, оставлены прежние настройки: {0}" } },
            { "logCfgReloaded",   new[] { "config.json reloaded",                   "config.json yenidən oxundu",                  "config.json перезагружен" } },
            { "logAutoOn",        new[] { "Auto switch enabled",                    "Avtomatik keçid açıldı",                      "Автопереключение включено" } },
            { "logAutoOff",       new[] { "Auto switch disabled",                   "Avtomatik keçid söndürüldü",                  "Автопереключение выключено" } },
            { "logStartupOn",     new[] { "Start with Windows enabled",             "Windows ilə başlama açıldı",                  "Автозапуск включён" } },
            { "logStartupOff",    new[] { "Start with Windows disabled",            "Windows ilə başlama söndürüldü",              "Автозапуск выключен" } },
            { "logStartupErr",    new[] { "Could not change startup setting: {0}",  "Startup ayarı alınmadı: {0}",                 "Не удалось изменить автозапуск: {0}" } },
            { "logDevicesChanged",new[] { "Device list changed",                    "Cihaz siyahısı dəyişdi",                      "Список устройств изменился" } },
            { "logError",         new[] { "Error: {0}",                             "Xəta: {0}",                                   "Ошибка: {0}" } },
            { "logSwitched",      new[] { "{0}: → {1}",                             "{0}: → {1}",                                  "{0}: → {1}" } },
            { "logManual",        new[] { "Manual: {0}",                            "Əl ilə: {0}",                                 "Вручную: {0}" } },
            { "logAdded",         new[] { "Added to list: {0}",                     "Siyahıya əlavə edildi: {0}",                  "Добавлено в список: {0}" } },
            { "logRemoved",       new[] { "Removed from list: {0}",                 "Siyahıdan silindi: {0}",                      "Удалено из списка: {0}" } },
            { "logNotConnected",  new[] { "Not connected: {0}",                     "Qoşulu deyil: {0}",                           "Не подключено: {0}" } },
            { "logBtConnecting",  new[] { "Bluetooth connecting: {0}",              "Bluetooth qoşulur: {0}",                      "Подключение Bluetooth: {0}" } },
            { "logBtDisconnecting",new[]{ "Bluetooth disconnecting: {0}",           "Bluetooth ayrılır: {0}",                      "Отключение Bluetooth: {0}" } },
            { "logBtAlreadyOn",   new[] { "{0} is already connected",               "{0} artıq qoşuludur",                         "{0} уже подключено" } },
            { "logBtAlreadyOff",  new[] { "{0} is already disconnected",            "{0} artıq ayrılıb",                           "{0} уже отключено" } },
            { "logBtNoEndpoint",  new[] { "{0}: no audio device in Windows yet, connect it once from Windows Settings", "{0}: Windows-da audio cihazı yoxdur, bir dəfə Windows Settings-dən qoşun", "{0}: в Windows нет аудиоустройства, подключите его один раз через Параметры Windows" } },
            { "logBtFailed",      new[] { "Failed — the device must be nearby and turned on", "Alınmadı — cihaz yaxında və açıq olmalıdır", "Не удалось — устройство должно быть рядом и включено" } },
        };
    }
}
