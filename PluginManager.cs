using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using log4net.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Markup;
using static SkyrimPluginEditor.PluginData;
using static System.Net.Mime.MediaTypeNames;
using static System.Resources.ResXFileRef;

namespace SkyrimPluginEditor
{
    public class PluginStreamBase
    {
        protected FileStream file = null;

        public enum _ErrorCode : Int16
        {
            Readed = 2,
            Passed = 1,
            //EndOfFile = 0,
            UnSupported = -1,
            Invalid = -2,
            WrongEOF = -3,
            WrongHeader = -4,
            StringMissing = -20,
            ILDLStringMissing = -21,
            InvalidStrings = -22
        }
        public enum _Encoding : Int16
        {
            Default,
            ASCII,
            UTF16,
            UTF8,
            Max
        }

        protected string ReadSignature()
        {
            return GetString(ReadBytes(4));
        }
        protected UInt16 ReadUInt16()
        {
            return BitConverter.ToUInt16(ReadBytes(2), 0);
        }
        protected UInt32 ReadUInt32()
        {
            return BitConverter.ToUInt32(ReadBytes(4), 0);
        }
        protected float ReadFloat()
        {
            return BitConverter.ToSingle(ReadBytes(4), 0);
        }
        protected string ReadString(int count = 1, int offset = 0)
        {
            return GetString(ReadBytes(count, offset));
        }
        protected string GetString(byte[] bytes)
        {
            switch (Config.GetSingleton.GetEncoding())
            {
                case _Encoding.Default:
                    return Encoding.Default.GetString(bytes);
                case _Encoding.ASCII:
                    return Encoding.ASCII.GetString(bytes);
                case _Encoding.UTF8:
                    return Encoding.UTF8.GetString(bytes);
                case _Encoding.UTF16:
                    return Encoding.Unicode.GetString(bytes);
            }
            return "";
        }
        protected byte[] GetBytes(char[] data)
        {
            if (data == null || data.Length == 0)
                return new byte[0];
            switch (Config.GetSingleton.GetEncoding())
            {
                case _Encoding.Default:
                    return Encoding.Default.GetBytes(data);
                case _Encoding.ASCII:
                    return Encoding.ASCII.GetBytes(data);
                case _Encoding.UTF8:
                    return Encoding.UTF8.GetBytes(data);
                case _Encoding.UTF16:
                    return Encoding.Unicode.GetBytes(data);
            }
            return null;
        }
        protected byte[] GetBytes(UInt16 data)
        {
            return BitConverter.GetBytes(data);
        }
        protected byte[] GetBytes(UInt32 data)
        {
            return BitConverter.GetBytes(data);
        }
        protected byte[] GetBytes(float data)
        {
            return BitConverter.GetBytes(data);
        }
        protected byte[] GetBytes(string data)
        {
            if (data == null)
                return new byte[0];
            switch (Config.GetSingleton.GetEncoding())
            {
                case _Encoding.Default:
                    return SetNullEndOfString(Encoding.Default.GetBytes(data));
                case _Encoding.ASCII:
                    return SetNullEndOfString(Encoding.ASCII.GetBytes(data));
                case _Encoding.UTF8:
                    return SetNullEndOfString(Encoding.UTF8.GetBytes(data));
                case _Encoding.UTF16:
                    return SetNullEndOfString(Encoding.Unicode.GetBytes(data));
            }
            return new byte[0];
        }
        protected byte[] SetNullEndOfString(byte[] bytes)
        {
            if (bytes.Last() == 0)
                return bytes; 
            byte[] newBytes = new byte[bytes.Length + 1];
            bytes.CopyTo(newBytes, 0);
            return newBytes;
        }

        protected List<byte> GetByteList(char[] data) { return GetBytes(data).ToList(); }
        protected List<byte> GetByteList(UInt16 data) { return GetBytes(data).ToList(); }
        protected List<byte> GetByteList(UInt32 data) { return GetBytes(data).ToList(); }
        protected List<byte> GetByteList(float data) { return GetBytes(data).ToList(); }
        protected List<byte> GetByteList(string data) { return data != null ? GetBytes(data).ToList() : new List<byte>(); }
        protected List<byte> GetByteList(byte[] data) { return data.ToList(); }

        protected byte[] ReadBytes(int count = 1, int offset = 0)
        {
            var bytes = new byte[count];
            file.Read(bytes, offset, count);
            return bytes;
        }
        protected void SkipBytes(int offset = 1)
        {
            file.Position += offset;
        }
        protected long GetSizePosition(uint length, int offset = 0) //get data size
        {
            var pos = file.Position;
            pos += length + offset;
            if (pos > file.Length)
                return 0;
            return pos;
        }
        protected bool IsEndedSize(long pos)
        {
            return pos == file.Position;
        }
        protected bool IsExceedSize(long pos)
        {
            return pos < file.Position;
        }
        protected string GetHex(uint hex)
        {
            return hex.ToString("X");
        }
    }

    public class PluginData : PluginStreamBase
    {
        public enum _FileFlags : UInt32
        {
            ESM = 1 << 0,
            Localized = 1 << 7,
            ESL = 1 << 9,
            Ignore = 1 << 12,
        }
        protected _FileFlags fileFlags;
        public bool IsESM() {  return (fileFlags & _FileFlags.ESM) == _FileFlags.ESM; }
        public bool IsESL() {  return (fileFlags & _FileFlags.ESL) == _FileFlags.ESL; }
        public bool IsLocalized() {  return (fileFlags & _FileFlags.Localized) == _FileFlags.Localized; }
        public bool IsIgnore() {  return (fileFlags & _FileFlags.Ignore) == _FileFlags.Ignore; }

        public enum _RecordFlags : UInt32
        {
            Delete = 1 << 5,
            Ignore = 1 << 12,
            Compressed = 1 << 18,
            BleedoutOverride = 1 << 29
        }
        public bool IsCompressed (_RecordHeader header) { 
            return (header.RecordFlags & (UInt32)_RecordFlags.Compressed) == (UInt32)_RecordFlags.Compressed; 
        }
        public bool IsCompressed (_Record record) { return IsCompressed(record.RecordHeader); }

        //EDID - EditorID
        //FULL - Name
        //MODL, MOD2, MOD3, MOD4, MOD5 - Mesh Path
        //TX00, TX01, TX02, TX03, TX04, TX05, TX06, TX07 - Texture Path
        [StructLayout(LayoutKind.Sequential)]
        public class _PluginDataStruct
        {
            public _RecordHeader PluginHeader; //data size = header + FrontFragment + MasterPlugin + BackFragment
            public _HEDR Header;
            public List<_Record_Fragment> FrontFragment; //OFST, DELE, CNAM, SNAM
            public List<_MAST> MasterPlugin;
            public List<_Record_Fragment> BackFragment; //ONAM, SCRN, INTV, INCC
            public List<_GRUP> Group;
        }
        public class _RecordHeader
        {
            public char[] Signature; //4
            public UInt32 DataSize; //4, all _Record_Fragment size without _Recordheader
            public UInt32 RecordFlags; //4
            public UInt32 FormID; //4
            public UInt16 TimeStamp; //2
            public UInt16 VersionInfo1; //2
            public UInt16 FormVersion; //2
            public UInt16 VersionInfo2; //2
        }
        public class _HEDR
        {
            public char[] Signature; //4
            public UInt16 DataSize; //2, version + recordnumber + nextobjectid
            public float Version; //4
            public UInt32 RecordNumber; //4
            public UInt32 NextObjectID; //4
        }
        public class _MAST
        {
            public char[] Signature; //4
            public UInt16 DataSize; //2, MasterPlugin only
            public byte[] MasterPlugin; //size is DataSize;
            public char[] DATA_Signature; //4
            public UInt16 DATA_DataSize; //2
            public byte[] DATA_Unk; //00 00 00 00 00 00 00 00

            //Below is not included in the file, just for parse
            public int IsEditable;
        }
        public class _GRUP
        {
            public char[] Signature; //4, GRUP
            public UInt32 DataSize; //4, Signiture + DataSize + RecordType + GroupType + TimeStamp + Version + Unk + Record
            public char[] RecordType; //4
            public UInt32 GroupType; //4
            public UInt16 TimeStamp; //2
            public UInt16 Version; //2
            public UInt32 Unk; //4
            public List<object> Record; //usually _Record but CELL uses another GRUP
        }
        public class _Record
        {
            public _RecordHeader RecordHeader;
            public List<_Record_Fragment> Fragment;

            //Below is not included in the file, just for parse
            public bool IsCompressed;
            //.e.g compressed
            //_RecordHeader RecordHeader; //DataSize = UncompressedSize + compressedData
            //UInt32 UncompressedSize;
            //byte[] compressedData; //Compressed Fragments
        }
        public class _Record_Fragment
        {
            public char[] Signature; //4
            public UInt16 DataSize; //2 only data
            public byte[] Data;

            //Below is not included in the file, just for parse
            public int IsEditable;
        }
        public string GetFilePath() { return path; }
        public string GetFileName() { return name; }

        protected string path;
        protected string name;
        protected _PluginDataStruct plugin;
        protected bool readed = false;

        public PluginData(string pluginPath)
        {
            this.path = pluginPath;
            this.name = Path.GetFileName(pluginPath);
        }

        protected void PluginBackup()
        {
            string targetBase = path + ".backup";
            string target = targetBase;
            UInt64 num = 0;
            while (File.Exists(target))
            {
                target = targetBase + num;
                num++;
            }
            File.Copy(path, target, true);
            Logger.Log.Info("Backup file... : " + path);
        }
    }

    public class LocalizeData : PluginStreamBase
    {
        public class _LocalizeDataStruct
        {
            public _LocalizeHeader Header; //Size = Strings size
            public List<_LocalizeEntry> Entry; //count is DataCount
            public List<string> Strings; //count is Entry count, each size is Entry StringSize
        }
        public class _LocalizeDataStruct_ILDL
        {
            public _LocalizeHeader Header; //Size = Strings size
            public List<_LocalizeEntry> Entry; //count is DataCount
            public List<_LocalizeData> Strings; //count is Entry count, each size is Entry StringSize

            public bool IsSTRINGS;
        }
        public class _LocalizeHeader
        {
            public UInt32 Count; //Entry count
            public UInt32 Size; //Strings size
        }
        public class _LocalizeEntry
        {
            public UInt32 ID;
            public UInt32 Pos; //first pos of string in Strings section
        }
        public class _LocalizeData //ILSTRINGS, DLSTRINGS
        {
            public UInt32 Size; //Data size
            public string Data;
        }

        public string GetString(UInt32 ID) 
        {
            int index = -1;
            if (STRINGS.Entry != null)
                index = STRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                if (STRINGS.Strings.Count <= index)
                    return "No String Error";
                return STRINGS.Strings[index];
            }

            index = -1;
            if (ILSTRINGS.Entry != null)
                index = ILSTRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                if (ILSTRINGS.Strings.Count <= index)
                    return "No String Error";
                return ILSTRINGS.Strings[index].Data;
            }

            index = -1;
            if (DLSTRINGS.Entry != null)
                index = DLSTRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                if (DLSTRINGS.Strings.Count <= index)
                    return "No String Error";
                return DLSTRINGS.Strings[index].Data;
            }
            return "No String Error";
        }

        private _LocalizeDataStruct STRINGS = null;
        private _LocalizeDataStruct_ILDL ILSTRINGS = null;
        private _LocalizeDataStruct_ILDL DLSTRINGS = null;

        private string BaseDirectory;
        private string PluginName; //without extension

        private string path;

        public LocalizeData(string pluginPath)
        {
            this.BaseDirectory = Path.GetDirectoryName(pluginPath) + "\\Strings\\";
            this.PluginName = Path.GetFileNameWithoutExtension(pluginPath);
        }

        public _ErrorCode Read()
        {
            _ErrorCode ErrorCode = _ErrorCode.Passed;

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".STRINGS";
            if (!File.Exists(path))
                return _ErrorCode.StringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    STRINGS = new _LocalizeDataStruct();
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(STRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".ILSTRINGS";
            if (!File.Exists(path))
                ErrorCode = _ErrorCode.ILDLStringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    ILSTRINGS = new _LocalizeDataStruct_ILDL();
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(ILSTRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".DLSTRINGS";
            if (!File.Exists(path))
                ErrorCode = _ErrorCode.ILDLStringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    DLSTRINGS = new _LocalizeDataStruct_ILDL();
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(DLSTRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }
            return ErrorCode;
        }
        public _ErrorCode ParseStringData(_LocalizeDataStruct localData)
        {
            localData.Header = new _LocalizeHeader();
            localData.Header.Count = ReadUInt32();
            localData.Header.Size = ReadUInt32();

            localData.Entry = new List<_LocalizeEntry>();
            for (int i = 0; i < localData.Header.Count; i++)
            {
                _LocalizeEntry entry = new _LocalizeEntry();
                entry.ID = ReadUInt32();
                entry.Pos = ReadUInt32();
                localData.Entry.Add(entry);
            }

            var pos = GetSizePosition(localData.Header.Size);
            if (pos > file.Length)
            {
                Error(_ErrorCode.InvalidStrings);
                return _ErrorCode.InvalidStrings;
            }

            localData.Strings = new List<string>();
            for (int i = 0; i < localData.Entry.Count; i++)
            {
                int readSize = 0;
                if (i + 1 < localData.Entry.Count)
                    readSize = (int)(localData.Entry[i + 1].Pos - localData.Entry[i].Pos);
                else //last 
                    readSize = (int)(file.Length - file.Position);
                byte[] bytes = ReadBytes(readSize);
                localData.Strings.Add(GetString(bytes));
            }
            return _ErrorCode.Passed;
        }
        public _ErrorCode ParseStringData(_LocalizeDataStruct_ILDL localData)
        {
            localData.Header = new _LocalizeHeader();
            localData.Header.Count = ReadUInt32();
            localData.Header.Size = ReadUInt32();

            localData.Entry = new List<_LocalizeEntry>();
            for (int i = 0; i < localData.Header.Count; i++)
            {
                _LocalizeEntry entry = new _LocalizeEntry();
                entry.ID = ReadUInt32();
                entry.Pos = ReadUInt32();
                localData.Entry.Add(entry);
            }

            var pos = GetSizePosition(localData.Header.Size);
            if (pos > file.Length)
            {
                Error(_ErrorCode.InvalidStrings);
                return _ErrorCode.InvalidStrings;
            }

            localData.Strings = new List<_LocalizeData>();
            foreach (var entry in localData.Entry)
            {
                _LocalizeData data = new _LocalizeData();
                data.Size = ReadUInt32();
                byte[] strbytes = ReadBytes((int)data.Size);
                data.Data = GetString(strbytes);
                localData.Strings.Add(data);
            }
            return _ErrorCode.Passed;
        }

        public bool EditString(UInt32 ID, string Text)
        {
            if (STRINGS != null)
            {
                int index = STRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    STRINGS.Strings[index] = Text;
                    return true;
                }
            }

            if (ILSTRINGS != null)
            {
                int index = ILSTRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    ILSTRINGS.Strings[index].Data = Text;
                    return true;
                }
            }
            if (DLSTRINGS != null)
            {
                int index = DLSTRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    DLSTRINGS.Strings[index].Data = Text;
                    return true;
                }
            }
            return false;
        }

        public void Write()
        {
            StringsBackup();

            if (STRINGS != null)
            {
                path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".STRINGS";
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(STRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }

            if (ILSTRINGS != null)
            {
                path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".ILSTRINGS";
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(ILSTRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }

            if (DLSTRINGS != null)
            {
                path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".DLSTRINGS";
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(DLSTRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }
        }

        public List<byte> GetByteList(_LocalizeDataStruct localize)
        {
            List<byte> data = new List<byte>();
            List<byte> entry = new List<byte>();
            List<byte> strings = new List<byte>();

            UInt32 stringPos = 0;
            for (int i = 0; i < localize.Strings.Count; i++)
            {
                entry.AddRange(GetByteList(localize.Entry[i], stringPos));
                string str = localize.Strings[i];
                if (str != null)
                {
                    List<byte> strBytes = GetByteList(str);
                    stringPos += (UInt32)strBytes.Count;
                    strings.AddRange(strBytes);
                }
            }
            data.AddRange(GetByteList(localize.Header, strings.Count));
            data.AddRange(entry);
            data.AddRange(strings);
            return data;
        }
        public List<byte> GetByteList(_LocalizeDataStruct_ILDL localize)
        {
            List<byte> data = new List<byte>();
            List<byte> entry = new List<byte>();
            List<byte> strings = new List<byte>();

            UInt32 stringPos = 0;
            for (int i = 0; i < localize.Strings.Count; i++)
            {
                entry.AddRange(GetByteList(localize.Entry[i], stringPos));
                strings.AddRange(GetByteList(localize.Strings[i], ref stringPos));
            }
            data.AddRange(GetByteList(localize.Header, strings.Count));
            data.AddRange(entry);
            data.AddRange(strings);
            return data;
        }
        public List<byte> GetByteList(_LocalizeHeader header, int stringsSize)
        {
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(header.Count));
            header.Size = (UInt32)stringsSize;
            data.AddRange(GetByteList(header.Size));
            return data;
        }
        public List<byte> GetByteList(_LocalizeEntry entry, UInt32 stringPos)
        {
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(entry.ID));
            entry.Pos = stringPos;
            data.AddRange(GetByteList(entry.Pos));
            return data;
        }
        public List<byte> GetByteList(_LocalizeData localData, ref UInt32 stringPos)
        {
            List<byte> data = new List<byte>();
            List<byte> stringData = GetByteList(localData.Data);
            localData.Size = (UInt32)stringData.Count;
            stringPos += 4;
            stringPos += (UInt32)stringData.Count;
            data.AddRange(GetByteList(localData.Size));
            data.AddRange(stringData);
            return data;
        }

        public void StringsBackup()
        {
            string targetBase = "";
            string target = "";
            UInt64 num = 0;

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".STRINGS";
            if (File.Exists(path))
            {
                targetBase = path + ".backup";
                target = targetBase;
                num = 0;
                while (File.Exists(target))
                {
                    target = targetBase + num;
                    num++;
                }
                File.Copy(path, target, true);
                Logger.Log.Info("Backup file... : " + path);
            }

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".ILSTRINGS";
            if (File.Exists(path))
            {
                targetBase = path + ".backup";
                target = targetBase;
                if (num > 0)
                    target = targetBase + num;
                File.Copy(path, target, true);
                Logger.Log.Info("Backup file... : " + path);
            }

            path = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".DLSTRINGS";
            if (File.Exists(path))
            {
                targetBase = path + ".backup";
                target = targetBase;
                if (num > 0)
                    target = targetBase + num;
                File.Copy(path, target, true);
                Logger.Log.Info("Backup file... : " + path);
            }
        }

        private void Error(_ErrorCode errorCode = 0, string extra = "")
        {
            if (errorCode != 0)
            {
                Logger.Log.Error("[" + file.Position + "] " + errorCode + extra + " : " + path);
            }
        }
    }

    public class PluginManager : PluginData
    {
        public class _Editable
        {
            public string RecordType { get; set; }
            public string FragmentType { get; set; }
            public string Text { get; set; }

            public bool Localized { get; set; }
            public UInt32 ID { get; set; }

            public int EditableIndex { get; set; }
        }
        private int EditableIndex = 0;
        protected List<_Editable> editableList = new List<_Editable>();
        public List<_Editable> GetEditableList() { return editableList; }
        public List<_Editable> GetEditableListOfMAST() { return editableList.FindAll(x => x.FragmentType == "MAST"); }
        public List<_Editable> GetEditableListOfRecord() { return editableList.FindAll(x => x.FragmentType != "MAST"); }

        LocalizeData Localize = null;

        public PluginManager(string pluginPath) : base(pluginPath)
        {

        }

        public _ErrorCode Read()
        {
            if (readed)
                return _ErrorCode.Readed;
            readed = true;

            _ErrorCode ErrorCode = _ErrorCode.Passed;
            _ErrorCode LocalizedErrorCode = _ErrorCode.Passed;

            file = new FileStream(path, FileMode.Open, FileAccess.Read);
            long sizePos = 0;

            Logger.Log.Info("Read : " + path);

            _PluginDataStruct newPlugin = new _PluginDataStruct();
            string sig = "";
            string RecordSig = "";

            newPlugin.PluginHeader = new _RecordHeader();
            sig = ReadSignature();
            if (sig != "TES4") //not support plugin
                return _ErrorCode.UnSupported;
            RecordSig = sig;
            newPlugin.PluginHeader.Signature = sig.ToCharArray();
            newPlugin.PluginHeader.DataSize = ReadUInt32();
            newPlugin.PluginHeader.RecordFlags = ReadUInt32();
            fileFlags = (_FileFlags)newPlugin.PluginHeader.RecordFlags;
            if (IsLocalized())
            {
                Localize = new LocalizeData(path);
                LocalizedErrorCode = Localize.Read();
            }
            newPlugin.PluginHeader.FormID = ReadUInt32();
            newPlugin.PluginHeader.TimeStamp = ReadUInt16();
            newPlugin.PluginHeader.VersionInfo1 = ReadUInt16();
            newPlugin.PluginHeader.FormVersion = ReadUInt16();
            newPlugin.PluginHeader.VersionInfo2 = ReadUInt16();

            sizePos = GetSizePosition(newPlugin.PluginHeader.DataSize);

            newPlugin.Header = new _HEDR();
            sig = ReadSignature();
            if (sig != "HEDR") //invalid plugin data structure
                return _ErrorCode.Invalid;
            newPlugin.Header.Signature = sig.ToCharArray();
            newPlugin.Header.DataSize = ReadUInt16();
            newPlugin.Header.Version = ReadFloat();
            newPlugin.Header.RecordNumber = ReadUInt32();
            newPlugin.Header.NextObjectID = ReadUInt32();

            sig = ReadSignature();

            newPlugin.FrontFragment = new List<_Record_Fragment>();
            bool isFirst = true;
            do
            {
                if (!isFirst)
                    sig = ReadSignature();
                if (sig != "OFST" && sig != "DELE" && sig != "CNAM" && sig != "SNAM")
                    break;

                _Record_Fragment fragment = new _Record_Fragment();
                ErrorCode = Read(fragment, RecordSig, ref sig);
                newPlugin.FrontFragment.Add(fragment);
                if (ErrorCode < 0)
                    return ErrorCode; //invalid or unsupported or wrong end or end of file

                isFirst = false;
            } while (true);

            newPlugin.MasterPlugin = new List<_MAST>();
            isFirst = true;
            do
            {
                if (!isFirst)
                    sig = ReadSignature();
                if (sig != "MAST")
                    break;

                _MAST master = new _MAST();
                ErrorCode = Read(master, RecordSig, ref sig);
                newPlugin.MasterPlugin.Add(master);
                if (ErrorCode < 0)
                    return ErrorCode; //invalid or unsupported or wrong end or end of file

                isFirst = false;
            } while (true);

            newPlugin.BackFragment = new List<_Record_Fragment>();
            isFirst = true;
            do
            {
                if (IsEndedSize(sizePos))
                    break;
                else if (IsExceedSize(sizePos))
                {
                    ErrorCode = _ErrorCode.WrongHeader;
                    break;
                }

                if (!isFirst)
                    sig = ReadSignature();
                if (sig == "GRUP")
                {
                    isFirst = true;
                    break;
                }
                _Record_Fragment fragment = new _Record_Fragment();
                ErrorCode = Read(fragment, RecordSig, ref sig);
                newPlugin.BackFragment.Add(fragment);
                if (ErrorCode < _ErrorCode.Passed)
                    return ErrorCode; //invalid or unsupported or wrong end or end of file

                isFirst = false;
            } while (true);

            newPlugin.Group = new List<_GRUP>();
            do
            {
                if (file.Length == file.Position)
                    break;

                if (!isFirst)
                    sig = ReadSignature();
                if (sig != "GRUP")
                {
                    if (ErrorCode == _ErrorCode.WrongHeader)
                    {
                        sig = ReadSignature();
                    }
                    if (sig != "GRUP")
                        return _ErrorCode.Invalid;
                }

                _GRUP group = new _GRUP();
                ErrorCode = Read(group, ref sig);
                newPlugin.Group.Add(group);
                if (ErrorCode < 0)
                    return ErrorCode; //invalid or unsupported file

                isFirst = false;
            } while (true);

            plugin = newPlugin;
            file.Close();

            ErrorCode = LocalizedErrorCode <= _ErrorCode.StringMissing ? LocalizedErrorCode : _ErrorCode.Passed;

            return ErrorCode; //passed
        }
        protected _ErrorCode Read(_Record_Fragment fragment, int extraSize, int XXXXdataSize, ref string sig)
        {
            fragment.Signature = sig.ToCharArray();
            fragment.DataSize = (UInt16)extraSize;
            fragment.Data = ReadBytes(fragment.DataSize + XXXXdataSize);

            return _ErrorCode.Passed;
        }
        protected _ErrorCode Read(_Record_Fragment fragment, string recordSig, ref string sig)
        {
            fragment.Signature = sig.ToCharArray();
            fragment.DataSize = ReadUInt16();
            fragment.Data = ReadBytes(fragment.DataSize);

            fragment.IsEditable = AddEditableList(recordSig, sig, fragment.Data);
            return _ErrorCode.Passed; //passed
        }
        protected _ErrorCode Read(MemoryStream stream, _Record_Fragment fragment, string recordSig, ref string sig)
        {
            fragment.Signature = sig.ToCharArray();
            byte[] data = new byte[2];
            stream.Read(data, 0, 2);
            fragment.DataSize = BitConverter.ToUInt16(data, 0);
            data = new byte[fragment.DataSize];
            stream.Read(data, 0, fragment.DataSize);
            fragment.Data = data;

            fragment.IsEditable = AddEditableList(recordSig, sig, fragment.Data);
            return _ErrorCode.Passed; //passed
        }
        protected _ErrorCode Read(_MAST master, string recordSig, ref string sig)
        {
            master.Signature = sig.ToCharArray();
            master.DataSize = ReadUInt16();
            string masterName = ReadString(master.DataSize);
            master.MasterPlugin = GetBytes(masterName);

            master.IsEditable = AddEditableList(recordSig, sig, masterName);

            sig = ReadSignature();
            if (sig != "DATA")
            {
                Error(master.Signature);
                return _ErrorCode.Invalid; //invalid plugin data structure
            }
            master.DATA_Signature = sig.ToCharArray();
            master.DATA_DataSize = ReadUInt16();
            master.DATA_Unk = ReadBytes(master.DATA_DataSize);

            return _ErrorCode.Passed; //passed
        }
        protected _ErrorCode Read(_GRUP group, ref string sig)
        {
            group.Signature = sig.ToCharArray();
            group.DataSize = ReadUInt32();
            string type = ReadSignature();
            group.RecordType = type.ToCharArray();
            group.GroupType = ReadUInt32();
            group.TimeStamp = ReadUInt16();
            group.Version = ReadUInt16();
            group.Unk = ReadUInt32();

            int CurrentGroupSize = 4 + 4 + 4 + 4 + 2 + 2 + 4; //sig, datasize, recordType, GroupType, TimeStamp, Version, Unk
            long pos = GetSizePosition(group.DataSize, -CurrentGroupSize);
            _ErrorCode ErrorCode = _ErrorCode.Passed;

            group.Record = new List<object>();
            do
            {
                if (IsEndedSize(pos))
                    break;
                else if (IsExceedSize(pos))
                    return _ErrorCode.Invalid;

                sig = ReadSignature();
                if (sig == "GRUP")
                {
                    _GRUP new_group = new _GRUP();
                    ErrorCode = Read(new_group, ref sig);
                    group.Record.Add(group);
                    if (ErrorCode < 0)
                    {
                        Error(group.Signature, ErrorCode);
                        return ErrorCode; //invalid or unsupported file
                    }
                }
                else
                {
                    _Record record = new _Record();
                    ErrorCode = Read(record, ref sig);
                    group.Record.Add(record);
                    if (ErrorCode < 0)
                    {
                        Error(group.RecordType, ErrorCode);
                        return ErrorCode; //invalid or unsupported file
                    }
                }
            } while (true);
            return _ErrorCode.Passed; //passed
        }
        protected _ErrorCode Read(_Record record, ref string sig)
        {
            record.RecordHeader = new _RecordHeader();
            record.RecordHeader.Signature = sig.ToCharArray();
            string RecordSig = sig;
            record.RecordHeader.DataSize = ReadUInt32();
            record.RecordHeader.RecordFlags = ReadUInt32();
            record.RecordHeader.FormID = ReadUInt32();
            record.RecordHeader.TimeStamp = ReadUInt16();
            record.RecordHeader.VersionInfo1 = ReadUInt16();
            record.RecordHeader.FormVersion = ReadUInt16();
            record.RecordHeader.VersionInfo2 = ReadUInt16();

            _ErrorCode ErrorCode = _ErrorCode.Passed;
            var pos = GetSizePosition(record.RecordHeader.DataSize);

            record.Fragment = new List<_Record_Fragment>();
            record.IsCompressed = IsCompressed(record);
            if (record.IsCompressed)
            {
                UInt32 DecompressedSize = ReadUInt32();
                byte[] compressedData = ReadBytes((Int32)record.RecordHeader.DataSize - 4);
                using (var compressedStream = new MemoryStream(compressedData))
                using (var decompressedStream = new MemoryStream())
                {
                    using (var uncompress = new InflaterInputStream(compressedStream))
                        uncompress.CopyTo(decompressedStream);

                    if (decompressedStream.Length != DecompressedSize)
                    {
                        Error(record.RecordHeader.Signature, ErrorCode, "Compressed Stream Size");
                        return _ErrorCode.Invalid;
                    }
                    decompressedStream.Position = 0;

                    do
                    {
                        if (decompressedStream.Position == decompressedStream.Length)
                            break;

                        byte[] data = new byte[4];
                        decompressedStream.Read(data, 0, 4);
                        sig = GetString(data);
                        _Record_Fragment fragment = new _Record_Fragment();
                        ErrorCode = Read(decompressedStream, fragment, RecordSig, ref sig);
                        record.Fragment.Add(fragment);
                        if (ErrorCode < 0)
                        {
                            Error(record.RecordHeader.Signature, ErrorCode, "Compressed Stream");
                            return ErrorCode;
                        }
                    } while (true);
                }
            }
            else
            {
                do
                {
                    if (IsEndedSize(pos))
                        break;
                    else if (IsExceedSize(pos))
                        return _ErrorCode.Invalid;

                    sig = ReadSignature();
                    _Record_Fragment fragment = new _Record_Fragment();
                    ErrorCode = Read(fragment, RecordSig, ref sig);
                    record.Fragment.Add(fragment);
                    if (ErrorCode < 0)
                        return ErrorCode;

                    if (sig == "XXXX") //OFST data size fragment
                    {
                        _Record_Fragment XXXX = new _Record_Fragment();
                        sig = ReadSignature(); //must be OFST or VMAD
                        //if (sig != "OFST" && sig != "VMAD")
                        //{
                        //    Error(RecordSig);
                        //    return _ErrorCode.Invalid;
                        //}
                        ErrorCode = Read(XXXX, 2, BitConverter.ToInt32(fragment.Data, 0), ref sig);
                        record.Fragment.Add(fragment);
                        if (ErrorCode < 0)
                        {
                            Error(RecordSig, ErrorCode);
                            return ErrorCode;
                        }
                    }

                } while (true);
            }

            return ErrorCode; //passed
        }
        private int AddEditableList(string recordSig, string sig, Byte[] data)
        {
            return AddEditableList(recordSig, sig, GetString(data), data);
        }
        private int AddEditableList(string recordSig, string sig, string str, Byte[] data = null)
        {
            if (!Config.GetSingleton.IsEditableType(recordSig, sig))
                return -1;
            if (Config.GetSingleton.IsEditableBlackList(recordSig, sig))
                return -1;

            _Editable editable = new _Editable();
            editable.RecordType = recordSig;
            editable.FragmentType = sig;
            if (IsLocalized() && Config.GetSingleton.IsLocalizeType(recordSig, sig))
            {
                editable.Localized = true;
                if (data != null)
                {
                    editable.ID = BitConverter.ToUInt32(data, 0);
                    editable.Text = Localize.GetString(editable.ID);
                }
            }
            else
            {
                editable.Localized = false;
                editable.Text = str;
            }
            editable.EditableIndex = EditableIndex;
            editableList.Add(editable);

            EditableIndex++;
            return editable.EditableIndex;
        }

        public bool EditEditableList(string RecordType, string FragmentType, string TextOrig, string Text)
        {
            int index = editableList.FindIndex(x => x.RecordType == RecordType && x.FragmentType == FragmentType && x.Text == TextOrig);
            if (index == -1)
                return false;
            editableList[index].Text = Text;
            return true;
        }
        public bool EditEditableList(int editableIndex, string Text)
        {
            if (EditableIndex < editableIndex)
                return false;
            int index = editableList.FindIndex(x => x.EditableIndex == editableIndex);
            if (index == -1)
                return false;
            editableList[index].Text = Text;
            return true;
        }
        public void ApplyEditableDatas()
        {
            foreach (_Record_Fragment fragment in plugin.FrontFragment)
            {
                ApplyEditableDatas_Record_Fragment(fragment);
            }
            foreach (_MAST mast in plugin.MasterPlugin)
            {
                ApplyEditableDatas_MAST(mast);
            }
            foreach (_Record_Fragment fragment in plugin.BackFragment)
            {
                ApplyEditableDatas_Record_Fragment(fragment);
            }
            foreach (_GRUP group in plugin.Group)
            {
                ApplyEditableDatas_GRUP(group);
            }
        }
        private void ApplyEditableDatas_MAST(_MAST mast)
        {
            if (mast.IsEditable == -1)
                return;
            int index = editableList.FindIndex(x => x.EditableIndex == mast.IsEditable);
            if (index == -1)
                return;
            mast.MasterPlugin = GetBytes(editableList[index].Text);
        }

        private void ApplyEditableDatas_GRUP(_GRUP group)
        {
            foreach (object item in group.Record)
            {
                if (item.GetType() == typeof(_GRUP))
                {
                    ApplyEditableDatas_GRUP(item as _GRUP);
                }
                else //record
                {
                    _Record record = item as _Record;
                    ApplyEditableDatas_Record(record);
                }
            }
        }
        private void ApplyEditableDatas_Record(_Record record)
        {
            foreach (_Record_Fragment fragment in record.Fragment)
            {
                ApplyEditableDatas_Record_Fragment(fragment);
            }
        }
        private void ApplyEditableDatas_Record_Fragment(_Record_Fragment fragment)
        {
            if (fragment.IsEditable == -1)
                return;
            int index = editableList.FindIndex(x => x.EditableIndex == fragment.IsEditable);
            if (index == -1)
                return;
            if (editableList[index].Localized)
            {
                if (!Localize.EditString(editableList[index].ID, editableList[index].Text))
                {
                    Logger.Log.Error("[" + editableList[index].RecordType + "|" + editableList[index].FragmentType + "]" + " Cannot edit localize " + editableList[index].ID + "|" + editableList[index].Text + " : " + path);
                }
                return;
            }
            fragment.Data = GetBytes(editableList[index].Text);
        }

        public void Write()
        {
            List<byte> fileData = new List<byte>();

            List<byte> pluginheaderFragments = new List<byte>();
            pluginheaderFragments.AddRange(GetByteList(plugin.Header));
            foreach (_Record_Fragment fragment in plugin.FrontFragment)
            {
                pluginheaderFragments.AddRange(GetByteList(fragment));
            }
            foreach (_MAST mast in plugin.MasterPlugin)
            {
                pluginheaderFragments.AddRange(GetByteList(mast));
            }
            foreach (_Record_Fragment fragment in plugin.BackFragment)
            {
                pluginheaderFragments.AddRange(GetByteList(fragment));
            }

            fileData.AddRange(GetByteList(plugin.PluginHeader, pluginheaderFragments.Count));
            fileData.AddRange(pluginheaderFragments);

            foreach (_GRUP group in plugin.Group)
            {
                fileData.AddRange(GetByteList(group));
            }

            PluginBackup();
            using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                file.Write(fileData.ToArray(), 0, fileData.Count);
            Logger.Log.Info("Write done : " + path);

            if (Localize != null)
                Localize.Write();
        }

        public List<byte> GetByteList(_RecordHeader header, int dataSize)
        {
            header.DataSize = (UInt32)dataSize;

            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(header.Signature));
            data.AddRange(GetByteList(header.DataSize));
            data.AddRange(GetByteList(header.RecordFlags));
            data.AddRange(GetByteList(header.FormID));
            data.AddRange(GetByteList(header.TimeStamp));
            data.AddRange(GetByteList(header.VersionInfo1));
            data.AddRange(GetByteList(header.FormVersion));
            data.AddRange(GetByteList(header.VersionInfo2));
            return data;
        }
        public List<byte> GetByteList(_HEDR header)
        {
            header.DataSize = 4 + 4 + 4;

            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(header.Signature));
            data.AddRange(GetByteList(header.DataSize));
            data.AddRange(GetByteList(header.Version));
            data.AddRange(GetByteList(header.RecordNumber));
            data.AddRange(GetByteList(header.NextObjectID));
            return data;
        }
        public List<byte> GetByteList(_Record_Fragment fragment)
        {
            fragment.DataSize = (UInt16)fragment.Data.Length;
            
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(fragment.Signature));
            data.AddRange(GetByteList(fragment.DataSize));
            data.AddRange(GetByteList(fragment.Data));
            return data;
        }
        public List<byte> GetByteList(_Record_Fragment fragment, int extraSize) //for XXXX record
        {
            fragment.DataSize = (UInt16)extraSize;
            
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(fragment.Signature));
            data.AddRange(GetByteList(fragment.DataSize));
            data.AddRange(GetByteList(fragment.Data));
            return data;
        }
        public List<byte> GetByteList(_MAST mast)
        {
            mast.DataSize = (UInt16)mast.MasterPlugin.Length;
            
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(mast.Signature));
            data.AddRange(GetByteList(mast.DataSize));
            data.AddRange(GetByteList(mast.MasterPlugin));
            data.AddRange(GetByteList(mast.DATA_Signature));
            data.AddRange(GetByteList(mast.DATA_DataSize));
            data.AddRange(GetByteList(mast.DATA_Unk));
            return data;
        }
        public List<byte> GetByteList(_GRUP group)
        {
            List<byte> recordBytes = new List<byte>();
            foreach (object item in group.Record)
            {
                if (item is _GRUP)
                {
                    recordBytes.AddRange(GetByteList(item as _GRUP));
                }
                else //record
                {
                    recordBytes.AddRange(GetByteList(item as _Record));
                }
            }

            group.DataSize = (UInt32)(4 + 4 + 4 + 4 + 2 + 2 + 4 + recordBytes.Count);

            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(group.Signature));
            data.AddRange(GetByteList(group.DataSize));
            data.AddRange(GetByteList(group.RecordType));
            data.AddRange(GetByteList(group.GroupType));
            data.AddRange(GetByteList(group.TimeStamp));
            data.AddRange(GetByteList(group.Version));
            data.AddRange(GetByteList(group.Unk));
            data.AddRange(recordBytes);

            return data;
        }
        public List<byte> GetByteList(_Record record)
        {
            List<byte> fragmentBytes = new List<byte>();
            bool isXXXX = false;
            foreach (_Record_Fragment fragment in record.Fragment)
            {
                if (isXXXX)
                {
                    fragmentBytes.AddRange(GetByteList(fragment, 2));
                }
                else
                {
                    fragmentBytes.AddRange(GetByteList(fragment));
                }
                isXXXX = (new string(fragment.Signature) == "XXXX");
            }

            List<byte> data = new List<byte>();
            if (record.IsCompressed)
            {
                List<byte> compressedData = new List<byte>();
                compressedData.AddRange(GetByteList((UInt32)fragmentBytes.Count));
                using (var decompressedStream = new MemoryStream(fragmentBytes.ToArray()))
                using (var compressedStream = new MemoryStream())
                {
                    using (var compress = new DeflaterOutputStream(compressedStream))
                    {
                        decompressedStream.CopyTo(compress);
                        compress.Finish();
                        compressedStream.Position = 0;
                        byte[] compressedBytes = new byte[compressedStream.Length];
                        compressedStream.Read(compressedBytes, 0, (int)compressedStream.Length);
                        compressedData.AddRange(compressedBytes.ToList());
                    }
                }
                data.AddRange(GetByteList(record.RecordHeader, compressedData.Count));
                data.AddRange(compressedData);
            }
            else
            {
                data.AddRange(GetByteList(record.RecordHeader, fragmentBytes.Count));
                data.AddRange(fragmentBytes);
            }

            return data;
        }
        
        private void Error(char[] sig, _ErrorCode errorCode = 0, string extra = "")
        {
            Error(sig.ToString(), errorCode, extra);
        }
        private void Error(string sig = "", _ErrorCode errorCode = 0, string extra = "")
        {
            if (sig != "" && errorCode != 0)
            {
                Logger.Log.Error("[" + file.Position + "] " + errorCode + " " + sig + extra + " : " + path);
            }
            else if (sig != "")
            {
                Logger.Log.Error("[" + file.Position + "] " + " Invalid " + sig + extra + " : " + path);
            }
            else if (errorCode != 0)
            {
                Logger.Log.Error("[" + file.Position + "] " + errorCode + extra + " : " + path);
            }
        }
    }
}
