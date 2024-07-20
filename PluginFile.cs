using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SkyrimPluginTextEditor
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
            AccessError = -10,
            StringMissing = -20,
            ILDLStringMissing = -21,
            InvalidStrings = -22,
            StringWrongEOF = -23,
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
        public string ReadStringToNull(int offset = 0)
        {
            List<byte> bytes = new List<byte>();
            do
            {
                byte[] b = ReadBytes(); //read 1 byte
                bytes.Add(b[0]);
                if (b[0] == 0)
                    break;
            }
            while (true);
            return GetString(bytes.ToArray());
        }
        public string GetString(byte[] bytes)
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
        public byte[] GetBytes(char[] data)
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
        public byte[] GetBytes(UInt16 data)
        {
            return BitConverter.GetBytes(data);
        }
        public byte[] GetBytes(Int16 data)
        {
            return BitConverter.GetBytes(data);
        }
        public byte[] GetBytes(UInt32 data)
        {
            return BitConverter.GetBytes(data);
        }
        public byte[] GetBytes(Int32 data)
        {
            return BitConverter.GetBytes(data);
        }
        public byte[] GetBytes(float data)
        {
            return BitConverter.GetBytes(data);
        }
        public byte[] GetBytes(string data)
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
        public byte[] SetNullEndOfString(byte[] bytes)
        {
            if (bytes.Last() == 0)
                return bytes; 
            byte[] newBytes = new byte[bytes.Length + 1];
            bytes.CopyTo(newBytes, 0);
            return newBytes;
        }

        public List<byte> GetByteList(char[] data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(UInt16 data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(Int16 data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(UInt32 data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(Int32 data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(float data) { return GetBytes(data).ToList(); }
        public List<byte> GetByteList(string data) { return data != null ? GetBytes(data).ToList() : new List<byte>(); }
        public List<byte> GetByteList(byte[] data) { return data.ToList(); }

        protected byte[] ReadBytes(int count = 1, int offset = 0)
        {
            if (count < 0)
                return new byte[0];
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
        public bool IsCompressed (_Record record) { 
            return (record.RecordFlags & (UInt32)_RecordFlags.Compressed) == (UInt32)_RecordFlags.Compressed; 
        }

        //EDID - EditorID
        //FULL - Name
        //MODL, MOD2, MOD3, MOD4, MOD5 - Mesh Path
        //TX00, TX01, TX02, TX03, TX04, TX05, TX06, TX07 - Texture Path
        public class _PluginDataBase
        {
            public char[] Signature; //4
        }
        public class _PluginDataBase16 : _PluginDataBase
        {
            public UInt16 DataSize; //2
        }
        public class _PluginDataBase32 : _PluginDataBase
        {
            public UInt32 DataSize; //4
        }
        public class _PluginDataStruct : _PluginDataBase32
        {
            //public char[] Signature; //4
            //public UInt32 DataSize; //4, header + FrontFragment + MasterPlugin + BackFragment
            public UInt32 RecordFlags; //4
            public UInt32 FormID; //4
            public UInt16 TimeStamp; //2
            public UInt16 VersionInfo1; //2
            public UInt16 FormVersion; //2
            public UInt16 VersionInfo2; //2
            public _HEDR Header;
            public List<_Record_Fragment> FrontFragment; //OFST, DELE, CNAM, SNAM
            public List<_MAST> MasterPlugin;
            public List<_Record_Fragment> BackFragment; //ONAM, SCRN, INTV, INCC
            public List<_GRUP> Group;
        }
        public class _HEDR : _PluginDataBase16
        {
            //public char[] Signature; //4
            //public UInt16 DataSize; //2, version + recordnumber + nextobjectid
            public float Version; //4
            public UInt32 RecordNumber; //4
            public UInt32 NextObjectID; //4
        }
        public class _MAST : _PluginDataBase16
        {
            //public char[] Signature; //4
            //public UInt16 DataSize; //2, MasterPlugin only
            public byte[] MasterPlugin; //size is DataSize;
            public char[] DATA_Signature; //4
            public UInt16 DATA_DataSize; //2
            public byte[] DATA_Unk; //00 00 00 00 00 00 00 00

            //Below is not included in the file, just for parse
            public int IsEditable;
        }
        public class _GRUP : _PluginDataBase32
        {
            //public char[] Signature; //4, GRUP
            //public UInt32 DataSize; //4, Signiture + DataSize + RecordType + GroupType + TimeStamp + Version + Unk + Record
            public byte[] GroupInfo; //4, usually Record Type(char[4]), but in multiple layer GRUP, just byte code
            public UInt32 GroupType; //4
            public UInt16 TimeStamp; //2
            public UInt16 Version; //2
            public UInt32 Unk; //4
            public List<object> Record; //usually _Record but CELL uses another GRUP
        }
        public class _Record : _PluginDataBase32
        {
            //public char[] Signature; //4
            //public UInt32 DataSize; //4, all _Record_Fragment size without _Recordheader
            public UInt32 RecordFlags; //4
            public UInt32 FormID; //4
            public UInt16 TimeStamp; //2
            public UInt16 VersionInfo1; //2
            public UInt16 FormVersion; //2
            public UInt16 VersionInfo2; //2
            public List<_Record_Fragment> Fragment;

            //Below is not included in the file, just for parse
            public bool IsCompressed;
            //.e.g compressed
            //_RecordHeader RecordHeader; //DataSize = UncompressedSize + compressedData
            //UInt32 UncompressedSize;
            //byte[] compressedData; //Compressed Fragments
        }
        public class _Record_Fragment : _PluginDataBase16
        {
            //public char[] Signature; //4
            //public UInt16 DataSize; //2 only data
            public byte[] Data;

            //Below is not included in the file, just for parse
            public int IsEditable;
        }
        public string GetFilePath() { return path; }
        public string GetFileName() { return name; }

        protected string path;
        protected string name;
        protected _PluginDataStruct plugin;
        protected LocalizeData localizeData;
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

        public _Record GetRecord(UInt32 formID)
        {
            if (plugin == null)
                return null;
            foreach (var g in plugin.Group)
            {
                _Record find = GetRecord(formID, g);
                if (find != null)
                    return find;
            }
            return null;
        }
        private _Record GetRecord(UInt32 formID, _GRUP group)
        {
            foreach (var rg in group.Record)
            {
                _Record find = null;
                if (rg is _GRUP)
                {
                    find = GetRecord(formID, rg as _GRUP);
                    if (find != null)
                        return find;
                }
                else
                {
                    _Record r = rg as _Record;
                    if (r.FormID == formID)
                        return r;
                }
            }
            return null;
        }
        public List<_Record> GetRecords(string signiture = "0000")
        {
            if (plugin == null || signiture.Length != 4)
                return new List<_Record>(); ;
            return GetRecords(signiture.ToCharArray());
        }
        public List<_Record> GetRecords(char[] signiture)
        {
            List<_Record> finds = new List<_Record>();
            if (plugin == null || signiture.Length != 4)
                return finds;
            foreach (var g in plugin.Group)
            {
                finds.AddRange(GetRecords(signiture, g));
            }
            return finds;
        }
        private List<_Record> GetRecords(char[] signiture, _GRUP group)
        {
            List<_Record> finds = new List<_Record>();
            foreach (var rg in group.Record)
            {
                if (rg is _GRUP)
                {
                    finds.AddRange(GetRecords(signiture, rg as _GRUP));
                }
                else
                {
                    _Record r = rg as _Record;
                    if (r.Signature == signiture || signiture == "0000".ToCharArray())
                        finds.Add(r);
                }
            }
            return finds;
        }
        public List<_Record> GetRecordsByfrag(char[] frag_sig)
        {
            List<_Record> finds = new List<_Record>();
            if (plugin == null || frag_sig.Length != 4)
                return finds;
            var records = GetRecords();
            foreach (var record in records)
            {
                if (record.Fragment.Any(x => x.Signature == frag_sig))
                    finds.Add(record);
            }
            return finds;
        }
        public _GRUP GetGroup(string groupInfo)
        {
            if (plugin == null || groupInfo.Length != 4)
                return null;
            return GetGroup(GetBytes(groupInfo));
        }
        public _GRUP GetGroup(char[] groupInfo)
        {
            if (plugin == null || groupInfo.Length != 4)
                return null;
            return GetGroup(GetBytes(groupInfo));
        }
        public _GRUP GetGroup(byte[] groupInfo)
        {
            if (plugin == null || groupInfo.Length != 4)
                return null;
            foreach (var g in plugin.Group)
            {
                _GRUP find = GetGroup(groupInfo, g);
                if (find != null) 
                    return find;
            }
            return null;
        }
        private _GRUP GetGroup(byte[] groupInfo, _GRUP group)
        {
            foreach (var rg in group.Record)
            {
                if (rg is _GRUP)
                {
                    _GRUP g = rg as _GRUP;
                    if (g.GroupInfo == groupInfo)
                        return g;
                    GetGroup(groupInfo, rg as _GRUP);
                }
            }
            return null;
        }
        public List<_Record_Fragment> GetFragments(char[] signiture)
        {
            List<_Record_Fragment> finds = new List<_Record_Fragment>();
            if (plugin == null || signiture.Length != 4)
                return finds;

            var records = GetRecords();
            foreach (var record in records)
            {
                finds.AddRange(record.Fragment.FindAll(x => x.Signature == signiture));
            }
            return finds;
        }
        public List<_Record_Fragment> GetFragments(UInt32 formID)
        {
            if (plugin == null)
                return new List<_Record_Fragment>();
            var record = GetRecord(formID);
            if (record != null)
                return record.Fragment;
            return new List<_Record_Fragment>();
        }
        public List<_Record_Fragment> GetFragments(char[] record_sig, char[] frag_sig)
        {
            List<_Record_Fragment> finds = new List<_Record_Fragment>();
            if (plugin == null || record_sig.Length != 4 || frag_sig.Length != 4)
                return finds;
            var records = GetRecords(record_sig);
            foreach (var record in records)
            {
                finds.AddRange(record.Fragment.FindAll(x => x.Signature == frag_sig));
            }
            return finds;
        }
        public string GetStringData(_Record_Fragment frag)
        {
            if (IsLocalized() && localizeData != null && frag.Data.Length == 4)
            {
                string str = localizeData.GetStringByID(frag.Data);
                if (str != null)
                    return str;
            }
            return BitConverter.ToString(frag.Data);
        }
        public bool SetStringData(_Record_Fragment frag, string str)
        {
            if (IsLocalized() && localizeData != null && frag.Data.Length == 4)
            {
                if (localizeData.SetStringByID(frag.Data, str))
                    return true;
            }
            frag.Data = GetBytes(str);
            return true;
        }
    }

    public class LocalizeData : PluginStreamBase
    {
        public class _LocalizeDataStruct
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
            public UInt32 Pos; //String start position in Strings data, similar to string's ID
        }
        public class _LocalizeData //ILSTRINGS, DLSTRINGS
        {
            public UInt32 Size; //Data size
            public UInt32 Pos; //String start position in Strings data, similar to string's ID
            public string Data;
        }

        public string GetStringByID(byte[] ID)
        {
            return GetStringByID(BitConverter.ToUInt32(ID));
        }
        public string GetStringByID(UInt32 ID) 
        {
            int index = -1;
            if (STRINGS != null && STRINGS.Entry != null)
                index = STRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                UInt32 pos = STRINGS.Entry[index].Pos;
                int sindex = STRINGS.Strings.FindIndex(x => x.Pos == pos);
                if (sindex != -1)
                    return STRINGS.Strings[sindex].Data;
                else
                    return "No String Error";
            }

            index = -1;
            if (ILSTRINGS != null && ILSTRINGS.Entry != null)
                index = ILSTRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                UInt32 pos = ILSTRINGS.Entry[index].Pos;
                int sindex = ILSTRINGS.Strings.FindIndex(x => x.Pos == pos);
                if (sindex != -1)
                    return ILSTRINGS.Strings[sindex].Data;
                else
                    return "No String Error";
            }

            index = -1;
            if (DLSTRINGS != null && DLSTRINGS.Entry != null)
                index = DLSTRINGS.Entry.FindIndex(x => x.ID == ID);
            if (index != -1)
            {
                UInt32 pos = DLSTRINGS.Entry[index].Pos;
                int sindex = DLSTRINGS.Strings.FindIndex(x => x.Pos == pos);
                if (sindex != -1)
                    return DLSTRINGS.Strings[sindex].Data;
                else
                    return "No String Error";
            }
            return null;
        }

        public bool SetStringByID(byte[] ID, string Text)
        {
            return SetStringByID(BitConverter.ToUInt32(ID), Text);
        }
        public bool SetStringByID(UInt32 ID, string Text)
        {
            if (STRINGS != null)
            {
                int index = STRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    int sindex = STRINGS.Strings.FindIndex(x => x.Pos == STRINGS.Entry[index].Pos);
                    if (sindex == -1)
                        return false;
                    STRINGS.Strings[sindex].Data = Text;
                    return true;
                }
            }
            if (ILSTRINGS != null)
            {
                int index = ILSTRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    int sindex = ILSTRINGS.Strings.FindIndex(x => x.Pos == ILSTRINGS.Entry[index].Pos);
                    if (sindex == -1)
                        return false;
                    ILSTRINGS.Strings[sindex].Data = Text;
                    return true;
                }
            }
            if (DLSTRINGS != null)
            {
                int index = DLSTRINGS.Entry.FindIndex(x => x.ID == ID);
                if (index != -1)
                {
                    int sindex = DLSTRINGS.Strings.FindIndex(x => x.Pos == DLSTRINGS.Entry[index].Pos);
                    if (sindex == -1)
                        return false;
                    DLSTRINGS.Strings[sindex].Data = Text;
                    return true;
                }
            }
            return false;
        }

        private _LocalizeDataStruct STRINGS = null;
        private _LocalizeDataStruct ILSTRINGS = null;
        private _LocalizeDataStruct DLSTRINGS = null;

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

            string stringsPath = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".STRINGS";
            string ilstringsPath = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".ILSTRINGS";
            string dlstringsPath = BaseDirectory + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".DLSTRINGS";

            if (!Util.CanRead(stringsPath) || !Util.CanRead(ilstringsPath) || !Util.CanRead(dlstringsPath))
                return _ErrorCode.AccessError;

            path = stringsPath;
            if (!File.Exists(path))
                return _ErrorCode.StringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    STRINGS = new _LocalizeDataStruct();
                    STRINGS.IsSTRINGS = true;
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(STRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }

            path = ilstringsPath;
            if (!File.Exists(path))
                ErrorCode = _ErrorCode.ILDLStringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    ILSTRINGS = new _LocalizeDataStruct();
                    ILSTRINGS.IsSTRINGS = false;
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(ILSTRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }

            path = dlstringsPath;
            if (!File.Exists(path))
                ErrorCode = _ErrorCode.ILDLStringMissing;
            else
            {
                using (file = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    DLSTRINGS = new _LocalizeDataStruct();
                    DLSTRINGS.IsSTRINGS = false;
                    Logger.Log.Info("Read : " + path);
                    ErrorCode = ParseStringData(DLSTRINGS);
                    Logger.Log.Info(ErrorCode + " : " + path);
                }
            }
            return ErrorCode;
        }
        private _ErrorCode ParseStringData(_LocalizeDataStruct localData)
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
            long firstStringsPos = file.Position;
            if (localData.IsSTRINGS)
            {
                do
                {
                    if (IsEndedSize(pos))
                        break;
                    else if (IsExceedSize(pos))
                    {
                        Error(_ErrorCode.StringWrongEOF);
                        return _ErrorCode.StringWrongEOF;
                    }

                    _LocalizeData data = new _LocalizeData();
                    data.Size = 0;
                    data.Pos = (UInt32)(file.Position - firstStringsPos);
                    data.Data = ReadStringToNull();
                    localData.Strings.Add(data);
                }
                while(true);
            }
            else
            {
                firstStringsPos += 4; //add first data size
                do
                {
                    if (IsEndedSize(pos))
                        break;
                    else if (IsExceedSize(pos))
                    {
                        Error(_ErrorCode.StringWrongEOF);
                        return _ErrorCode.StringWrongEOF;
                    }

                    _LocalizeData data = new _LocalizeData();
                    data.Size = ReadUInt32();
                    data.Pos = (UInt32)(file.Position - firstStringsPos);
                    data.Data = ReadString((int)data.Size);
                    localData.Strings.Add(data);
                }
                while(true);
            }
            return _ErrorCode.Passed;
        }

        public bool Write()
        {
            return Write(BaseDirectory, true);
        }
        public bool Write(string folder)
        {
            return Write(folder, false);
        }
        public bool Write(bool fileBackup)
        {
            return Write(BaseDirectory, fileBackup);
        }
        public bool Write(string folder, bool fileBackup)
        {
            if (fileBackup)
                StringsBackup();

            string stringsPath = folder + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".STRINGS";
            string ilstringsPath = folder + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".ILSTRINGS";
            string dlstringsPath = folder + PluginName + "_" + Config.GetSingleton.GetStringLanguage() + ".DLSTRINGS";

            if (!Util.CanWrite(stringsPath) || !Util.CanWrite(ilstringsPath) || !Util.CanWrite(dlstringsPath))
                return false;

            if (STRINGS != null)
            {
                path = stringsPath;
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(STRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }

            if (ILSTRINGS != null)
            {
                path = ilstringsPath;
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(ILSTRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }

            if (DLSTRINGS != null)
            {
                path = dlstringsPath;
                using (file = new FileStream(path, FileMode.Truncate, FileAccess.Write))
                {
                    List<byte> fileData = GetByteList(DLSTRINGS);
                    file.Write(fileData.ToArray(), 0, fileData.Count);
                }
                Logger.Log.Info("Write done : " + path);
            }

            return true;
        }

        private List<byte> GetByteList(_LocalizeDataStruct localize)
        {
            List<byte> data = new List<byte>();
            List<byte> entry = new List<byte>();
            List<byte> strings = new List<byte>();

            List<int> newStringPosData = new List<int>();
            if (localize.IsSTRINGS) //strings data
            {
                foreach (var s in localize.Strings)
                {
                    newStringPosData.Add(strings.Count);
                    strings.AddRange(GetByteList(s.Data));
                }
            }
            else
            {
                bool firstString = true;
                foreach (var s in localize.Strings)
                {
                    List<byte> strBytes = GetByteList(s.Data);
                    strings.AddRange(GetByteList(strBytes.Count));
                    if (firstString)
                        newStringPosData.Add(0);
                    else
                        newStringPosData.Add(strings.Count);
                    strings.AddRange(strBytes);
                }
            }

            foreach (var e in localize.Entry) //entry data
            {
                entry.AddRange(GetByteList(e.ID));
                int index = localize.Strings.FindIndex(x => x.Pos == e.Pos);
                if (index == -1 || index >= newStringPosData.Count)
                {
                    Logger.Log.Error("Found invalid String Pos Data in writing, so skipping... : " + path);
                    continue;
                }
                entry.AddRange(GetByteList(newStringPosData[index]));
            }

            data.AddRange(GetByteList(localize.Header, strings.Count));
            data.AddRange(entry);
            data.AddRange(strings);
            return data;
        }
        private List<byte> GetByteList(_LocalizeHeader header, int stringsSize)
        {
            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(header.Count));
            header.Size = (UInt32)stringsSize;
            data.AddRange(GetByteList(header.Size));
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

    public class PluginFile : PluginData
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

        bool IsEdited = false;

        public PluginFile(string pluginPath) : base(pluginPath)
        {

        }

        public _ErrorCode Read()
        {
            if (readed)
                return _ErrorCode.Readed;

            if (!Util.CanRead(path))
                return _ErrorCode.AccessError;

            readed = true;

            _ErrorCode ErrorCode = _ErrorCode.Passed;
            _ErrorCode LocalizedErrorCode = _ErrorCode.Passed;

            file = new FileStream(path, FileMode.Open, FileAccess.Read);
            long sizePos = 0;

            Logger.Log.Info("Read : " + path);

            _PluginDataStruct newPlugin = new _PluginDataStruct();
            string sig = "";
            string RecordSig = "";

            sig = ReadSignature();
            if (sig != "TES4") //not support plugin
            {
                Error(sig, _ErrorCode.UnSupported);
                return _ErrorCode.UnSupported;
            }
            RecordSig = sig;
            newPlugin.Signature = sig.ToCharArray();
            newPlugin.DataSize = ReadUInt32();
            newPlugin.RecordFlags = ReadUInt32();
            fileFlags = (_FileFlags)newPlugin.RecordFlags;
            if (IsLocalized())
            {
                localizeData = new LocalizeData(path);
                LocalizedErrorCode = localizeData.Read();
            }
            newPlugin.FormID = ReadUInt32();
            newPlugin.TimeStamp = ReadUInt16();
            newPlugin.VersionInfo1 = ReadUInt16();
            newPlugin.FormVersion = ReadUInt16();
            newPlugin.VersionInfo2 = ReadUInt16();

            sizePos = GetSizePosition(newPlugin.DataSize);

            newPlugin.Header = new _HEDR();
            sig = ReadSignature();
            if (sig != "HEDR") //invalid plugin data structure
            {
                Error(sig, _ErrorCode.Invalid);
                return _ErrorCode.Invalid;
            }
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
                if (sig != "OFST" && sig != "DELE" && sig != "CNAM" && sig != "SNAM" && sig != "XXXX")
                    break;

                _Record_Fragment fragment = new _Record_Fragment();
                ErrorCode = Read(fragment, RecordSig, ref sig);
                newPlugin.FrontFragment.Add(fragment);
                if (ErrorCode < 0)
                {
                    Error("FrontFragment", ErrorCode);
                    return ErrorCode; //invalid or unsupported or wrong end or end of file
                }
                if (sig == "XXXX") //Offset data size fragment
                {
                    _Record_Fragment XXXX = new _Record_Fragment();
                    sig = ReadSignature();
                    ErrorCode = Read(XXXX, 2, BitConverter.ToInt32(fragment.Data, 0), ref sig);
                    newPlugin.FrontFragment.Add(fragment);
                    if (ErrorCode < 0)
                    {
                        Error(RecordSig, ErrorCode);
                        return ErrorCode;
                    }
                }
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
                {
                    Error("MAST", ErrorCode);
                    return ErrorCode; //invalid or unsupported or wrong end or end of file
                }

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
                if (sig != "ONAM" && sig != "SCRN" && sig != "INTV" && sig != "INCC" && sig != "XXXX")
                {
                    isFirst = true;
                    break;
                }
                _Record_Fragment fragment = new _Record_Fragment();
                ErrorCode = Read(fragment, RecordSig, ref sig);
                newPlugin.BackFragment.Add(fragment);
                if (ErrorCode < _ErrorCode.Passed)
                {
                    Error("", ErrorCode);
                    return ErrorCode; //invalid or unsupported or wrong end or end of file
                }
                if (sig == "XXXX") //Offset data size fragment
                {
                    _Record_Fragment XXXX = new _Record_Fragment();
                    sig = ReadSignature();
                    ErrorCode = Read(XXXX, 2, BitConverter.ToInt32(fragment.Data, 0), ref sig);
                    newPlugin.BackFragment.Add(fragment);
                    if (ErrorCode < 0)
                    {
                        Error(RecordSig, ErrorCode);
                        return ErrorCode;
                    }
                }
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
                        sig = ReadSignature();
                    if (sig != "GRUP")
                    {
                        Error("GRUP", _ErrorCode.Invalid);
                        return _ErrorCode.Invalid;
                    }
                }

                _GRUP group = new _GRUP();
                ErrorCode = Read(group, ref sig);
                newPlugin.Group.Add(group);
                if (ErrorCode < 0)
                {
                    Error("GRUP", ErrorCode);
                    return ErrorCode; //invalid or unsupported file
                }

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
                Error("DATA", _ErrorCode.Invalid);
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
            group.GroupInfo = ReadBytes(4);
            string type = GetString(group.GroupInfo);
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
                {
                    Error("GRUP", _ErrorCode.Invalid);
                    return _ErrorCode.Invalid;
                }

                sig = ReadSignature();
                if (sig == "GRUP")
                {
                    _GRUP new_group = new _GRUP();
                    ErrorCode = Read(new_group, ref sig);
                    group.Record.Add(new_group);
                    if (ErrorCode < 0)
                    {
                        Error("GRUP", ErrorCode);
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
                        Error(GetString(group.GroupInfo), ErrorCode);
                        return ErrorCode; //invalid or unsupported file
                    }
                }
            } while (true);
            return _ErrorCode.Passed; //passed
        }
        protected _ErrorCode Read(_Record record, ref string sig)
        {
            record.Signature = sig.ToCharArray();
            string RecordSig = sig;
            record.DataSize = ReadUInt32();
            record.RecordFlags = ReadUInt32();
            record.FormID = ReadUInt32();
            record.TimeStamp = ReadUInt16();
            record.VersionInfo1 = ReadUInt16();
            record.FormVersion = ReadUInt16();
            record.VersionInfo2 = ReadUInt16();

            _ErrorCode ErrorCode = _ErrorCode.Passed;
            var pos = GetSizePosition(record.DataSize);

            record.Fragment = new List<_Record_Fragment>();
            record.IsCompressed = IsCompressed(record);
            if (record.IsCompressed)
            {
                UInt32 DecompressedSize = ReadUInt32();
                byte[] compressedData = ReadBytes((Int32)record.DataSize - 4);
                using (var compressedStream = new MemoryStream(compressedData))
                using (var decompressedStream = new MemoryStream())
                {
                    using (var uncompress = new InflaterInputStream(compressedStream))
                        uncompress.CopyTo(decompressedStream);

                    if (decompressedStream.Length != DecompressedSize)
                    {
                        Error(record.Signature, ErrorCode, "Compressed Stream Size");
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
                            Error(record.Signature, ErrorCode, "Compressed Stream");
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
                    {
                        Error(RecordSig, ErrorCode);
                        return ErrorCode;
                    }

                    if (sig == "XXXX") //XXXX is data size fragment
                    {
                        _Record_Fragment XXXX = new _Record_Fragment();
                        sig = ReadSignature();
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
                    editable.Text = localizeData.GetStringByID(editable.ID);
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
            if (TextOrig == Text)
                return true;
            editableList[index].Text = Text;
            IsEdited = true;
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
            IsEdited = true;
            return true;
        }
        public void ApplyEditableDatas()
        {
            if (!IsEdited)
                return;

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
                    ApplyEditableDatas_Record(item as _Record);
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
                if (!localizeData.SetStringByID(editableList[index].ID, editableList[index].Text))
                {
                    Logger.Log.Error("[" + editableList[index].RecordType + "|" + editableList[index].FragmentType + "]" + " Cannot edit localize " + editableList[index].ID + "|" + editableList[index].Text + " : " + path);
                }
                return;
            }
            fragment.Data = GetBytes(editableList[index].Text);
        }

        public bool Write()
        {
            return Write(path, true);
        }
        public bool Write(string filePath)
        {
            return Write(filePath, false);
        }
        public bool Write(bool fileBackup)
        {
            return Write(path, fileBackup);
        }
        public bool Write(string filePath, bool fileBackup)
        {
            if (!IsEdited)
                return true;

            if (!Util.CanWrite(filePath))
                return false;

            List<byte> fileData = new List<byte>();

            List<byte> pluginheader = new List<byte>();
            pluginheader.AddRange(GetByteList(plugin.Header));
            pluginheader.AddRange(GetByteList(plugin.FrontFragment));
            foreach (_MAST mast in plugin.MasterPlugin)
            {
                pluginheader.AddRange(GetByteList(mast));
            }
            pluginheader.AddRange(GetByteList(plugin.BackFragment));
            plugin.DataSize = (UInt32)pluginheader.Count;
            fileData.AddRange(GetByteList(plugin.Signature));
            fileData.AddRange(GetByteList(plugin.DataSize));
            fileData.AddRange(GetByteList(plugin.RecordFlags));
            fileData.AddRange(GetByteList(plugin.FormID));
            fileData.AddRange(GetByteList(plugin.TimeStamp));
            fileData.AddRange(GetByteList(plugin.VersionInfo1));
            fileData.AddRange(GetByteList(plugin.FormVersion));
            fileData.AddRange(GetByteList(plugin.VersionInfo2));
            fileData.AddRange(pluginheader);

            foreach (_GRUP group in plugin.Group)
            {
                fileData.AddRange(GetByteList(group));
            }

            if (fileBackup)
                PluginBackup();

            using (file = new FileStream(filePath, File.Exists(filePath) ? FileMode.Truncate : FileMode.Create, FileAccess.Write))
                file.Write(fileData.ToArray(), 0, fileData.Count);
            Logger.Log.Info("Write done : " + path);

            if (localizeData != null)
                return localizeData.Write(fileBackup);
            return true;
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

            if (new string(fragment.Signature) != "XXXX")
                data.AddRange(GetByteList(fragment.Data));
            return data;
        }
        public List<byte> GetByteList(_Record_Fragment fragment, ref int dataSize) //for XXXX next record
        {
            fragment.DataSize = 0;
            dataSize = fragment.Data.Length;

            List<byte> data = new List<byte>();
            data.AddRange(GetByteList(fragment.Signature));
            data.AddRange(GetByteList(fragment.DataSize));
            data.AddRange(GetByteList(fragment.Data));
            return data;
        }
        public List<byte> GetByteList(List<_Record_Fragment> fragments)
        {
            List<byte> data = new List<byte>();
            for (int i = 0; i < fragments.Count; i++)
            {
                data.AddRange(GetByteList(fragments[i]));
                if (new string(fragments[i].Signature) == "XXXX" && i+1 < fragments.Count) //XXXX next record
                {
                    i++;
                    int dataSize = 0;
                    var xxxx = GetByteList(fragments[i], ref dataSize);
                    data.AddRange(GetByteList(dataSize));
                    data.AddRange(xxxx);
                }
            }
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
            data.AddRange(GetByteList(group.GroupInfo));
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
            fragmentBytes.AddRange(GetByteList(record.Fragment));

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

                record.DataSize = (UInt32)compressedData.Count;
                data.AddRange(GetByteList(record.Signature));
                data.AddRange(GetByteList(record.DataSize));
                data.AddRange(GetByteList(record.RecordFlags));
                data.AddRange(GetByteList(record.FormID));
                data.AddRange(GetByteList(record.TimeStamp));
                data.AddRange(GetByteList(record.VersionInfo1));
                data.AddRange(GetByteList(record.FormVersion));
                data.AddRange(GetByteList(record.VersionInfo2));
                data.AddRange(compressedData);
            }
            else
            {
                record.DataSize = (UInt32)fragmentBytes.Count;
                data.AddRange(GetByteList(record.Signature));
                data.AddRange(GetByteList(record.DataSize));
                data.AddRange(GetByteList(record.RecordFlags));
                data.AddRange(GetByteList(record.FormID));
                data.AddRange(GetByteList(record.TimeStamp));
                data.AddRange(GetByteList(record.VersionInfo1));
                data.AddRange(GetByteList(record.FormVersion));
                data.AddRange(GetByteList(record.VersionInfo2));
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
