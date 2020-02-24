﻿using StreamCore.SimpleJSON;
using StreamCore.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamCore.Config
{
    public class BilibiliLoginConfig
    {
        private string FilePath = Path.Combine(Globals.DataPath, $"BilibiliLoginInfo.ini");

        public int BilibiliChannelId = 0;
        public string BilibiliCookie = "";
        public int danmuku = 1;
        public int popularity = 0;
        public int gift = 0;
        public int guard = 0;
        public int welcome = 0;
        public int anchor = 0;
        public int global = 0;
        public int blacklist = 0;
        public int roomInfo = 0;
        public int junk = 0;

        public event Action<BilibiliLoginConfig> ConfigChangedEvent;

        private readonly FileSystemWatcher _configWatcher;
        private bool _saving;

        private static BilibiliLoginConfig _instance = null;
        public static BilibiliLoginConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new BilibiliLoginConfig();
                return _instance;
            }

            private set
            {
                _instance = value;
            }
        }

        public BilibiliLoginConfig()
        {
            Instance = this;

            string oldDataPath = Path.Combine(Environment.CurrentDirectory, "UserData", "EnhancedBilibiliChat");
            if (Directory.Exists(oldDataPath) && !Directory.Exists(Globals.DataPath))
                Directory.Move(oldDataPath, Globals.DataPath);

            if (!Directory.Exists(Path.GetDirectoryName(FilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));

            Load();
            CorrectConfigSettings();
            Save();

            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(FilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "BilibiliLoginInfo.ini",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += ConfigWatcherOnChanged;
        }

        ~BilibiliLoginConfig()
        {
            _configWatcher.Changed -= ConfigWatcherOnChanged;
        }

        public void Load()
        {
            if(File.Exists(FilePath))
                ObjectSerializer.Load(this, FilePath);

            CorrectConfigSettings();
        }

        public void Save(bool callback = false)
        {
            if (!callback)
                _saving = true;
            ObjectSerializer.Save(this, FilePath);
        }

        private void CorrectConfigSettings()
        {
            if (BilibiliChannelId < 0)
                BilibiliChannelId = 0;
            if (danmuku != 0 && danmuku != 1)
                danmuku = 1;
            if (popularity != 0 && popularity != 1)
                popularity = 0;
            if (gift != 0 && gift != 1)
                gift = 0;
            if (guard != 0 && guard != 1)
                guard = 0;
            if (welcome != 0 && welcome != 1)
                welcome = 0;
            if (anchor != 0 && anchor != 1)
                anchor = 0;
            if (global != 0 &&global != 1)
                global = 0;
            if (blacklist != 0 && blacklist != 1)
                blacklist = 0;
            if (roomInfo != 0 && roomInfo != 1)
                roomInfo = 0;
            if (junk != 0 && junk != 1)
                junk = 0;
        }

        private void ConfigWatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (_saving)
            {
                _saving = false;
                return;
            }

            Load();

            if (ConfigChangedEvent != null)
            {
                ConfigChangedEvent(this);
            }
        }
    }
}
