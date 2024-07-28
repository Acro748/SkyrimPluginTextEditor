using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SkyrimPluginTextEditor
{
    public class Config
    {
        private Config() { }
        private static readonly Lazy<Config> _instance = new Lazy<Config> (() => new Config());
        public static Config GetSingleton { get { return _instance.Value; } }

        const string jsonFilePath = @"SkyrimPluginEditor.json";
        const string typeDictionaryFolder = @"TypeDictionary\";
        const string typeDictionaryFile = @"TypeDictionary.json";
        const string typeDictionaryFilePrefix = @"TypeDictionary";
        public void ConfigRead()
        {
            if (File.Exists(jsonFilePath))
            {
                using (StreamReader file = File.OpenText(jsonFilePath))
                {
                    string json = file.ReadToEnd();

                    ConfigContainer newConfigContainer = JsonConvert.DeserializeObject<ConfigContainer>(json);
                    configContainer = newConfigContainer;
                }
            }
            else //no config file
            {
                ConfigWrite();
            }

            if (File.Exists(typeDictionaryFolder + typeDictionaryFile))
            {
                using (StreamReader file = File.OpenText(typeDictionaryFolder + typeDictionaryFile))
                {
                    string json = file.ReadToEnd();

                    TypeDictionary newTypeDictionary = JsonConvert.DeserializeObject<TypeDictionary>(json);
                    typeDictionary = newTypeDictionary;
                }
            }
            else
            {
                DictionaryWrite();
            }
            foreach(var path in Directory.GetFiles(typeDictionaryFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                string name = System.IO.Path.GetFileName(path);
                if (name == typeDictionaryFile)
                    continue;
                if (name.StartsWith(typeDictionaryFilePrefix, StringComparison.OrdinalIgnoreCase) || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    using (StreamReader file = File.OpenText(path))
                    {
                        string json = file.ReadToEnd();

                        TypeDictionary newTypeDictionary = JsonConvert.DeserializeObject<TypeDictionary>(json);
                        foreach (var item in newTypeDictionary.EditableType)
                        {
                            if (typeDictionary.EditableType.ContainsKey(item.Key))
                            {
                                typeDictionary.EditableType[item.Key].AddRange(item.Value.FindAll(x => typeDictionary.EditableType[item.Key].FindIndex(y => x == y) == -1));
                            }
                            else
                            {
                                typeDictionary.EditableType.Add(item.Key, item.Value);
                            }
                        }
                        foreach (var item in newTypeDictionary.EditableBlackList)
                        {
                            if (typeDictionary.EditableBlackList.ContainsKey(item.Key))
                            {
                                typeDictionary.EditableBlackList[item.Key].AddRange(item.Value.FindAll(x => typeDictionary.EditableBlackList[item.Key].FindIndex(y => x == y) == -1));
                            }
                            else
                            {
                                typeDictionary.EditableBlackList.Add(item.Key, item.Value);
                            }
                        }
                        foreach (var item in newTypeDictionary.LocalizeType)
                        {
                            if (typeDictionary.LocalizeType.ContainsKey(item.Key))
                            {
                                typeDictionary.LocalizeType[item.Key].AddRange(item.Value.FindAll(x => typeDictionary.LocalizeType[item.Key].FindIndex(y => x == y) == -1));
                            }
                            else
                            {
                                typeDictionary.LocalizeType.Add(item.Key, item.Value);
                            }
                        }
                    }

                }
            };
        }

        public void ConfigWrite()
        {
            File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(configContainer, Formatting.Indented));
        }
        public void DictionaryWrite()
        {
            Directory.CreateDirectory(typeDictionaryFolder);
            File.WriteAllText(typeDictionaryFolder + typeDictionaryFile, JsonConvert.SerializeObject(typeDictionary, Formatting.Indented));
        }

        public Logger.LogLevel GetLogLevel() { return configContainer.Logging; }
        public string GetDefaultPath() { return configContainer.DefaultPath; }
        public bool GetIsFixedDefaultPath() { return configContainer.IsFixedDefaultPath; }
        public PluginStreamBase._Encoding GetEncoding() { return configContainer.Encoding; }
        public string GetStringLanguage() { return configContainer.StringLanguage; }
        public bool GetParallelFolderRead() { return configContainer.ParallelFolderRead; }
        public double GetSkyrimPluginEditor_Height() { return configContainer.skyrimPluginEditor.Height; }
        public double GetSkyrimPluginEditor_Width() { return configContainer.skyrimPluginEditor.Width; }
        public bool GetSkyrimPluginEditor_MatchCase() { return configContainer.skyrimPluginEditor.MatchCase; }
        public bool GetSkyrimPluginEditor_FileBackup() { return configContainer.skyrimPluginEditor.FileBackup; }
        public bool GetSkyrimPluginEditor_SafetyMode() { return configContainer.skyrimPluginEditor.SafetyMode; }
        public double GetFileManager_Height() { return configContainer.fileManager.Height; }
        public double GetFileManager_Width() { return configContainer.fileManager.Width; }
        public bool GetFileManager_MatchCase() { return configContainer.fileManager.MatchCase; }
        public bool GetFileManager_FileContent() { return configContainer.fileManager.FileContent; }
        public bool GetFileManager_FileOverwrite() { return configContainer.fileManager.FileOverwrite; }
        public bool GetFileManager_ClearEmptySubFolder() { return configContainer.fileManager.ClearEmptySubFolder; }
        public bool GetFileManager_NonSkyrimFile() { return configContainer.fileManager.NonSkyrimFile; }
        public double GetNifManager_Height() { return configContainer.nifManager.Height; }
        public double GetNifManager_Width() { return configContainer.nifManager.Width; }
        public bool GetNifManager_MatchCase() { return configContainer.nifManager.MatchCase; }
        public bool GetNifManager_FileBackup() { return configContainer.nifManager.FileBackup; }

        public void SetLogLevel(Logger.LogLevel logLevel)
        {
            configContainer.Logging = logLevel;
            Logger.Instance.SetLogLevel(logLevel);
        }
        public bool SetDefaultPath(string newDefaultPath)
        {
            if (Directory.Exists(newDefaultPath))
            {
                configContainer.DefaultPath = newDefaultPath;
                ConfigWrite();
                return true;
            }
            return false;
        }
        public void SetIsFixedDefaultPath(bool active) { configContainer.IsFixedDefaultPath = active; ConfigWrite(); }
        public void SetEncoding(PluginStreamBase._Encoding encoding) { configContainer.Encoding = encoding; ConfigWrite(); }
        public void SetStringLanguage(string language) { configContainer.StringLanguage = language; ConfigWrite(); }
        public void SetParallelFolderRead(bool active) { configContainer.ParallelFolderRead = active; ConfigWrite(); }
        public void SetSkyrimPluginEditor_Size(double Height, double Width) {
            configContainer.skyrimPluginEditor.Height = Height;
            configContainer.skyrimPluginEditor.Width = Width;
            ConfigWrite();
        }
        public void SetSkyrimPluginEditor_MatchCase(bool active) { configContainer.skyrimPluginEditor.MatchCase = active; ConfigWrite(); }
        public void SetSkyrimPluginEditor_FileBackup(bool active) { configContainer.skyrimPluginEditor.FileBackup = active; ConfigWrite(); }
        public void SetSkyrimPluginEditor_SafetyMode(bool active) { configContainer.skyrimPluginEditor.SafetyMode = active; ConfigWrite(); }
        public void SetFileManager_Size(double Height, double Width)
        {
            configContainer.fileManager.Height = Height;
            configContainer.fileManager.Width = Width;
            ConfigWrite();
        }
        public void SetFileManager_MatchCase(bool active) { configContainer.fileManager.MatchCase = active; ConfigWrite(); }
        public void SetFileManager_FileContent(bool active) { configContainer.fileManager.FileContent = active; ConfigWrite(); }
        public void SetFileManager_FileOverwrite(bool active) { configContainer.fileManager.FileOverwrite = active; ConfigWrite(); }
        public void SetFileManager_ClearEmptySubFolder(bool active) { configContainer.fileManager.ClearEmptySubFolder = active; ConfigWrite(); }
        public void SetFileManager_NonSkyrimFile(bool active) { configContainer.fileManager.NonSkyrimFile = active; ConfigWrite(); }

        public void SetNifManager_Size(double Height, double Width)
        {
            configContainer.nifManager.Height = Height;
            configContainer.nifManager.Width = Width;
            ConfigWrite();
        }
        public void SetNifManager_MatchCase(bool active) { configContainer.nifManager.MatchCase = active; ConfigWrite(); }
        public void SetNifManager_FileBackup(bool active) { configContainer.nifManager.FileBackup = active; ConfigWrite(); }


        public Dictionary<string, List<string>> GetEditableType() { return typeDictionary.EditableType; }
        public bool IsEditableType(string recordSig, string sig)
        {
            if (GetEditableType()["0000"].FindIndex(x => x == sig) != -1)
                return true;
            else if (GetEditableType().TryGetValue(recordSig, out List<string> result) && result.FindIndex(x => x == sig) != -1)
                return true;
            return false;
        } 
        public Dictionary<string, List<string>> GetEditableBlackList() { return typeDictionary.EditableBlackList; }
        public bool IsEditableBlackList(string recordSig, string sig)
        {
            if (GetEditableBlackList()["0000"].FindIndex(x => x == sig) != -1)
                return true;
            else if (GetEditableBlackList().TryGetValue(recordSig, out List<string> result) && result.FindIndex(x => x == sig) != -1)
                return true;
            return false;
        }
        public Dictionary<string, List<string>> GetLocalizeType() { return typeDictionary.LocalizeType; }
        public bool IsLocalizeType(string recordSig, string sig)
        {
            if (GetLocalizeType()["0000"].FindIndex(x => x == sig) != -1)
                return true;
            else if (GetLocalizeType().TryGetValue(recordSig, out List<string> result) && result.FindIndex(x => x == sig) != -1)
                return true;
            return false;
        }
        public List<string> GetEditableFileContentList() {  return typeDictionary.EditableFileContentList; }
        public bool IsEditableFileContent(string fileName)
        {
            return GetEditableFileContentList().FindIndex(x => fileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)) != -1;
        }

        public class ConfigContainer
        {
            public string DefaultPath = Directory.GetCurrentDirectory();
            public bool IsFixedDefaultPath = false;
            public Logger.LogLevel Logging = Logger.LogLevel.Info;
            public PluginStreamBase._Encoding Encoding = PluginStreamBase._Encoding.UTF8;
            public string StringLanguage = "English";
            public bool ParallelFolderRead = true;

            public class SkyrimPluginEditor
            {
                public double Height = 800;
                public double Width = 1200;
                public bool MatchCase = false;
                public bool FileBackup = true;
                public bool SafetyMode = false;
            }
            public SkyrimPluginEditor skyrimPluginEditor = new SkyrimPluginEditor();

            public class FileManager
            {
                public double Height = 550;
                public double Width = 900;
                public bool MatchCase = false;
                public bool FileContent = false;
                public bool FileOverwrite = false;
                public bool ClearEmptySubFolder = true;
                public bool NonSkyrimFile = false;
            }
            public FileManager fileManager = new FileManager();

            public class NifManager
            {
                public double Height = 550;
                public double Width = 900;
                public bool MatchCase = false;
                public bool FileBackup = true;
            }
            public NifManager nifManager = new NifManager();
        }
        private ConfigContainer configContainer = new ConfigContainer();
        public class TypeDictionary
        {
            public readonly Dictionary<string, List<string>> EditableType = new Dictionary<string, List<string>>
            {
                { "0000", new List<string>
                {
                    "EDID", "FULL", "MODL", "MOD2", "MOD3", "MOD4", "MOD5",
                "TX00", "TX01", "TX02", "TX03", "TX04", "TX05", "TX06", "TX07",
                "00TX", "10TX", "20TX", "30TX", "40TX", "50TX", "60TX", "70TX", "80TX", "90TX", ":0TX", ";0TX", "<0TX", "=0TX", ">0TX",
                "?0TX", "@0TX", "A0TX", "B0TX", "C0TX", "D0TX", "E0TX", "F0TX", "G0TX", "H0TX", "I0TX", "J0TX", "K0TX", "L0TX"
                } },
                { "TES4", new List<string>
                {
                    "CNAM", "SNAM", "NAM1"
                } },
                { "BOOK", new List<string>
                {
                    "CNAM", "SNAM", "NAM1"
                } },
                { "QUST", new List<string>
                {
                    "CNAM", "SNAM", "NAM1"
                } },
                { "HDPT", new List<string>
                {
                    "NAM1"
                } },
                { "INFO", new List<string>
                {
                    "NAM1"
                } },
                { "RACE", new List<string>
                {
                    "ANAM"
                } }
            };
            public readonly Dictionary<string, List<string>> EditableBlackList = new Dictionary<string, List<string>>
            {
                { "0000", new List<string> { } },
                { "ARMO", new List<string> { "MODL" } },
                { "ARMA", new List<string> { "MODL" } }
            };
            public readonly Dictionary<string, List<string>> LocalizeType = new Dictionary<string, List<string>>
            {
                { "0000", new List<string> { "FULL" } },
                { "ARMO", new List<string> { "NAM1" } },
                { "INFO", new List<string> { "NAM1" } },
                { "QUST", new List<string> { "CNAM" } },
                { "BOOK", new List<string> { "CNAM" } }
            };
            public readonly List<string> EditableFileContentList = new List<string>
            {
                ".ini", ".ec", ".txt", ".xml", ".jslot", ".psc", ".osp"
            };
        }
        private TypeDictionary typeDictionary = new TypeDictionary();
    }
}
