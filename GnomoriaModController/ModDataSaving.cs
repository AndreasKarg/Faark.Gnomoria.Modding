using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Runtime.Serialization;
using System.IO;

namespace Faark.Gnomoria.Modding
{
    public class ModSaveData
    {
        /// <summary>
        /// Contains some serialization stuff. That is actually pretty tricky...
        /// </summary>
        private class DataItem
        {
            public readonly string Key;
            public readonly object Value;
            public readonly bool IsInvalid;
            /*
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                var hs = new HashSet<Mod>();
                try
                {
                    info.AddValue("Key", key);
                    info.AddValue("Type", value.GetType().FullName);
                    info.AddValue("Value", value);
                }
                catch (SerializationException err)
                {
                    throw err;
                }
                catch (Exception err)
                {
                    throw err;
                }
            }
            public msd_dataItem(SerializationInfo info, StreamingContext context)
            {
                key = info.GetString("Key");
                var type = info.GetString("Type");
                value = info.GetValue("Value", Type.GetType(type));
            }
             */

            public DataItem(string k, object v)
            {
                Key = k;
                Value = v;
            }

            public DataItem(XElement el)
            {
                try
                {
                    // Todo: handle invalid data properly!!
                    Key = el.Attribute("Key").Value;
                    var type = Type.GetType(el.Attribute("Type").Value, true);
                    //if( type.IsPrimitive )
                    // TODO: Complete member initialization#
                    var dcs = new DataContractSerializer(type, null, int.MaxValue, false, false, null, ModSaveFile.GetDataContractResolver());
                    var tmpReader = el.CreateReader();
                    tmpReader.MoveToContent();
                    Value = dcs.ReadObject(tmpReader, false);
                }
                catch (Exception)
                {
                    IsInvalid = true;
                }
            }

            public void WriteSaveData(System.Xml.XmlWriter writer)
            {
                //We simply skip null values;
                if (Value == null)
                    return;

                writer.WriteStartElement("Data");
                writer.WriteAttributeString("Key", Key);

                // Todo: does this create a security hole? Though mods can do everything anyway, this may abuse a mod to do sth harmful. Consider it!
                var assemblyQualifiedName = Value.GetType().AssemblyQualifiedName;
                Debug.Assert(assemblyQualifiedName != null, "assemblyQualifiedName != null");
                writer.WriteAttributeString("Type", assemblyQualifiedName);

                var dcs = new DataContractSerializer(Value.GetType(), "", "", null, Int32.MaxValue, false, false, null, ModSaveFile.GetDataContractResolver());
                dcs.WriteObjectContent(writer, Value);

                //var dcjs = new System.Runtime.Serialization.Json.DataContractJsonSerializer(value.GetType()

                writer.WriteEndElement();
            }
        }

        private readonly Dictionary<String, DataItem> _data = new Dictionary<string, DataItem>();
        private bool _hasData;
        private readonly XElement _unserializedData;
        /*//[DataMember(Name = "Data")]
        private msd_dataItem[] serializableData
        {
            get
            {
                return data.Select(kvp => new msd_dataItem(kvp.Key, kvp.Value)).ToArray();
            }
            set
            {
                data = new Dictionary<string, object>();
                foreach (var d in value)
                {
                    if (d.value != null && (d.key != null))
                    {
                        data.Add(d.key, d.value);
                    }
                }
            }
        }*/

        public bool HasAnyData
        {
            get
            {
                return _hasData;
            }
        }

        public bool HasData(string key)
        {
            return _data.ContainsKey(key) && !_data[key].IsInvalid;
        }

        public object this[string key]
        {
            get
            {
                DataItem di;
                if (_data.TryGetValue(key, out di))
                {
                    return di.Value;
                }

                throw new ArgumentException("No data found for key [" + key + "]");
            }
            set
            {
                var di = new DataItem(key, value);
                _data[di.Key] = di;
                _hasData = true;
            }
        }

        public T GetData<T>(string key)
        {
            var val = this[key];
            if (val is T)
            {
                return (T)val;
            }

            throw new Exception("Data [" + key + "] is not of type [" + typeof(T) + "], not [" + ((val == null) ? "NULL" : val.GetType().ToString()) + "]");
        }

        public T GetData<T>(string key, T def)
        {
            return HasData(key) ? GetData<T>(key) : def;
        }

        public void GetData<T>(string key, Action<T> to, T def = default(T))
        {
            if (HasData(key))
            {
                to(GetData<T>(key));
            }
            else
            {
                to(def);
            }
        }

        public String GetString(string key)
        {
            return GetData<string>(key);
        }

        public String GetString(string key, String def)
        {
            return GetData(key, def);
        }

        public void SetData<T>(string key, T data)
        {
            this[key] = data;
        }

        public void SetString(string key, string value)
        {
            SetData(key, value);
        }

        public void ClearData(string key)
        {
            _data.Remove(key);
            _hasData = _data.Count > 0;
        }

        //[DataMember(Name="ModType")]
        public String ModType { get; private set; }
        public IMod LoadedMod { get; private set; }

        public bool IsModLoaded
        {
            get { return LoadedMod != null; }
        }


        /*
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ModType", ModType);
            info.AddValue("Data", data.Select(kvp => new msd_dataItem() { key = kvp.Key, value = kvp.Value }).ToArray());
            return;
        }
        */
        public ModSaveData(IMod mod)
        {
            ModType = mod.GetType().FullName;
            LoadedMod = mod;
        }



        /*
        public ModSaveData(SerializationInfo info, StreamingContext context)
        {
            ModType = (string)info.GetValue("ModType", typeof(string));
            LoadedMod = RuntimeModController.ActiveMods.SingleOrDefault(mod => mod.GetType().FullName == ModType);
            if (IsModLoaded)
            {
                data = new Dictionary<string, object>();
                var datas = (msd_dataItem[])info.GetValue("Data", typeof(msd_dataItem[]));
                foreach (var d in datas)
                {
                    data.Add(d.key, d.value);
                }
            }
        }
        */

        public ModSaveData(XElement modElement)
        {
            ModType = modElement.Attribute("ModType").Value;
            LoadedMod = RuntimeModController.ActiveMods.SingleOrDefault(mod => mod.GetType().FullName == ModType);
            if (IsModLoaded)
            {
                foreach (var el in modElement.Elements("Data"))
                {
                    var di = new DataItem(el);
                    if (!di.IsInvalid)
                    {
                        _data.Add(di.Key, di);
                    }
                }
            }
            else
            {
                _unserializedData = new XElement(modElement);
            }
        }

        public void WriteSaveData(System.Xml.XmlWriter writer)
        {
            if (IsModLoaded)
            {
                writer.WriteStartElement("Mod");
                writer.WriteAttributeString("ModType", ModType);
                foreach (var dataElement in _data)
                {
                    dataElement.Value.WriteSaveData(writer);
                }
                writer.WriteEndElement();
            }
            else
            {
                _unserializedData.WriteTo(writer);
            }

        }
    }
    /// <summary>
    /// Actually just a collection of ModSaveData. Also takes care of some serialization stuff.
    /// </summary>
    [DataContract]
    class ModSaveFile
    {
        Dictionary<IMod, ModSaveData> _modSaveData;
        Dictionary<string, ModSaveData> _unloadedSaveData;
        List<ModSaveData> _allSaveData;

        [DataMember(Name = "SavedModData")]
        private ModSaveData[] AllSavedDataAsArray
        {
            get
            {
                return _allSaveData.Where(d => !d.IsModLoaded || d.HasAnyData).ToArray();
            }
            set
            {
                _allSaveData = value.ToList();
                _modSaveData = new Dictionary<IMod, ModSaveData>();
                _unloadedSaveData = new Dictionary<string, ModSaveData>();
                foreach (var el in _allSaveData)
                {
                    if (el.IsModLoaded)
                        _modSaveData.Add(el.LoadedMod, el);
                    else
                        _unloadedSaveData.Add(el.ModType, el);
                }
            }
        }

        public ModSaveData GetDataFor(IMod mod)
        {
            ModSaveData result;
            if (_modSaveData.TryGetValue(mod, out result))
            {
                return result;
            }
            var textKey = mod.GetType().FullName;
            if (_unloadedSaveData.TryGetValue(textKey, out result))
            {
// ReSharper disable HeuristicUnreachableCode
                Debug.Fail("This should never happen!");
                throw new InvalidOperationException("This should never happen!");
// ReSharper restore HeuristicUnreachableCode

                /*
                #warning this should actually never happen anymore, since we look up existing mods while loading anyway.
                unloadedSaveData.Remove(textKey);
                modSaveData.Add(mod, result);
                return result;
                */
            }
            result = new ModSaveData(mod);
            _modSaveData.Add(mod, result);
            _allSaveData.Add(result);
            return result;
        }

        #region Initialization
        public void Init()
        {
            _modSaveData = new Dictionary<IMod, ModSaveData>();
            _unloadedSaveData = new Dictionary<string, ModSaveData>();
            _allSaveData = new List<ModSaveData>();
        }

        [OnDeserializing]
        public void Init(StreamingContext context)
        {
            Init();
        }

        internal ModSaveFile()
        {
            Init();
        }
        #endregion

        #region Actual serialization calls
        internal class ModDataContractResolver : DataContractResolver
        {
            private string typeToString(Type t)
            {
                Debug.Assert(t.AssemblyQualifiedName != null, "t.AssemblyQualifiedName != null");
                return Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(t.AssemblyQualifiedName)).Replace("=", "");
            }

            private string stringToAQN(string text)
            {
                while (text.Length % 4 != 0)
                {
                    text = text + "=";
                }
                return System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(text));
            }

            readonly System.Xml.XmlDictionary _dict = new System.Xml.XmlDictionary();
            public override Type ResolveName(string typeName, string typeNamespace, Type declaredType, DataContractResolver knownTypeResolver)
            {
                try
                {
                    if (typeNamespace == (GetType().Namespace + ".Base64Encoded"))
                    {
                        var txt = stringToAQN(typeName);
                        var ttype = Type.GetType(txt, false);
                        if (ttype != null)
                            return ttype;
                    }
                }
                catch (Exception) // Todo: Clean this up
                {
                }

                // Todo: Find out what to do with that null parameter. (Which must not be null!)
                return knownTypeResolver.ResolveName(typeName, typeNamespace, declaredType, null);// ?? AppDomain.CurrentDomain.GetAssemblies().SelectMany(ass => ass.GetTypes()).FirstOrDefault(t => t.Namespace == typeNamespace && t.Name == typeName);
                // Todo: does this create a security hole? Though mods can do everything anyway, this may abuse a mod to do sth harmful. Consider it!
                // for now i don't think so, since typeName has to be assignable to it. And object or whatsoever...
                // it still could change the mods function. What about just loading stuff that the mod does reference? (eighter by game + modconfig or assembly?)
                // type.IsAssignableFrom
                /*var matches = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(t => t.Name == typeName && t.Namespace == typeNamespace).ToArray();
                matches.Count();
                throw new NotImplementedException();*/
            }

            public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out System.Xml.XmlDictionaryString typeName, out System.Xml.XmlDictionaryString typeNamespace)
            {
                typeName = _dict.Add(typeToString(type));
                typeNamespace = _dict.Add(GetType().Namespace + ".Base64Encoded");
                //typeNamespace = dict.Add(type.Namespace);
                return true;
                //throw new NotImplementedException();
            }
        }

        private static ModDataContractResolver _saveFileCtrRes;
        internal static ModDataContractResolver GetDataContractResolver()
        {
            return _saveFileCtrRes ?? (_saveFileCtrRes = new ModDataContractResolver());
        }

        private static DataContractSerializer _saveFileDcs;

        internal static DataContractSerializer GetDataContractSerializer()
        {
            if (_saveFileDcs != null) return _saveFileDcs;

            _saveFileDcs = new DataContractSerializer(typeof(ModSaveFile), null, Int32.MaxValue, false, false, null, GetDataContractResolver());
            return _saveFileDcs;
        }

        public static ModSaveFile LoadFrom(FileInfo fileName)
        {
            if (!fileName.Exists)
            {
                RuntimeModController.Log.Write("Mod save exists for this world, creating a new one", fileName.Name);
                return new ModSaveFile();
            }

            var doc = XDocument.Load(fileName.FullName);
            var saveFileElement = doc.Element("ModSaveFile");

            Debug.Assert(saveFileElement != null, "doc has Element \"ModSaveFile\"");

            var saveFile = new ModSaveFile
            {
                AllSavedDataAsArray =
                    saveFileElement.Elements("Mod").Select(md => new ModSaveData(md)).ToArray()
            };

            return saveFile;
            /*
            var dcserializer = GetDataContractSerializer();
            //var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ModEnvironmentConfiguration));
            using (var fstream = fileName.OpenRead())
            {
                var msf = (ModSaveFile)dcserializer.ReadObject(fstream);
                return msf;
            }
            */
        }

        public void SaveTo(FileInfo filename)
        {
            //var dcs = GetDataContractSerializer();
            var tmpFile = new FileInfo (filename.FullName + ".temp");
            using (var wstream = tmpFile.Open(FileMode.Create))
            {
                using (var writer = System.Xml.XmlWriter.Create(wstream, new System.Xml.XmlWriterSettings() { Indent = true }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("ModSaveFile");
                    writer.WriteAttributeString("Version", "1");
                    foreach (var md in _modSaveData)
                    {
                        if (md.Value.HasAnyData || !md.Value.IsModLoaded)
                        {
                            md.Value.WriteSaveData(writer);
                        }
                    }
                    //writer.WriteComment("DO NOT CHANGE ANYTHING IN THIS FILE WITHOUT DELETING GnomoriaModded.exe!");//\nThis will make the EXE be recreated, since otherwise the game will crash.
                    //serializer.Serialize(writer, self);
                    //dcs.WriteObject(writer, this);
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                    writer.Flush();
                    writer.Close();
                }
            }
            /*using (var s = new StreamWriter(filename.FullName + ".temp"))
            {
                dcs.WriteObject(s.BaseStream, this);
            }*/
            if (filename.Exists)
            {
                filename.Delete();
            }
            File.Move(filename.FullName + ".temp", filename.FullName);
        }
        #endregion
    }
}
