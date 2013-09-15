//#define NO_ASSEMBLY_LOADING
// see also injector.cs, modfinder.cs


using System;
using System.Runtime.Serialization;
using System.IO;
using Faark.Util;

namespace Faark.Gnomoria.Modding
{
    [DataContract]
    public class ModEnvironmentConfiguration
    {
        [DataContract]
        public class HashData
        {

            [DataMember]
            public String SourceExecutable;
            [DataMember]
            public String ModdedExecutable;
            [DataMember]
            public String SourceLibrary;
            [DataMember]
            public String ModdedLibrary;
            public HashData()
            {
            }
            public HashData(HashData from)
            {
                SourceExecutable = from.SourceExecutable;
                ModdedExecutable = from.ModdedExecutable;
                SourceLibrary = from.SourceLibrary;
                ModdedLibrary = from.ModdedLibrary;
            }
        }

        [DataMember]
        public Microsoft.Xna.Framework.Graphics.GraphicsProfile GraphicsProfile { get; protected set; }
        [DataMember]
        public bool ClearLogsOnRebuild { get; protected set; }

        private HashData _hashes;
        [DataMember]
        public HashData Hashes
        {
            get
            {
                return _hashes ?? (_hashes = new HashData());
            }
            private set
            {
                _hashes = value;
            }
        }

        private ModReference[] _modReferences;
        [DataMember(Name = "Mods")]
        public ModReference[] ModReferences {
            get
            {
                return _modReferences ?? (_modReferences = new ModReference[0]);
            }
            protected set
            {
                _modReferences = value;
            }
        }

        public static ModEnvironmentConfiguration Load(FileInfo xmlConfigFile)
        {
            var dcserializer = new DataContractSerializer(typeof(ModEnvironmentConfiguration));
            //var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ModEnvironmentConfiguration));
            using (var fstream = xmlConfigFile.OpenRead())
            {
                var mec = (ModEnvironmentConfiguration)dcserializer.ReadObject(fstream);
                return mec;
            }
        }

        protected static void Save(ModEnvironmentConfiguration self, FileInfo xmlConfigFile)
        {
            // http://stackoverflow.com/questions/2129414/how-to-insert-xml-comments-in-xml-serialization

            var dcserializer = new DataContractSerializer(typeof(ModEnvironmentConfiguration));
            //var serializer = new System.Xml.Serialization.XmlSerializer(typeof(ModEnvironmentConfiguration));
            using (var wstream = xmlConfigFile.Open(FileMode.Create))
            using (var writer = System.Xml.XmlWriter.Create(wstream, new System.Xml.XmlWriterSettings { Indent = true }))
            {
                writer.WriteStartDocument();
                writer.WriteComment("DO NOT CHANGE ANYTHING IN THIS FILE WITHOUT DELETING GnomoriaModded.exe!");//\nThis will make the EXE be recreated, since otherwise the game will crash.
                //serializer.Serialize(writer, self);
                dcserializer.WriteObject(writer, self);
                writer.WriteEndDocument();
                writer.Flush();
                writer.Close();
            }
        }

        public bool CheckFilesValid(
            FileInfo sourceExe,
            FileInfo moddedExe,
            FileInfo sourceLib,
            FileInfo moddedLib
            )
            //System.IO.DirectoryInfo base_directoy, String source_exe_name, String modded_exe_name)
        {
            return
                (Hashes.SourceExecutable != null)
                && (Hashes.ModdedExecutable != null)
                && (Hashes.SourceLibrary != null)
                && (Hashes.ModdedLibrary != null)
                && sourceExe.Exists
                && sourceLib.Exists
                && moddedExe.Exists
                && moddedLib.Exists
                && (sourceExe.GenerateMD5Hash() == Hashes.SourceExecutable)
                && (moddedExe.GenerateMD5Hash() == Hashes.ModdedExecutable)
                && (sourceLib.GenerateMD5Hash() == Hashes.SourceLibrary)
                && (moddedLib.GenerateMD5Hash() == Hashes.ModdedLibrary);

            /*var source_exe = base_directoy.GetFiles().Single(file => file.Name.ToUpper() == source_exe_name.ToUpper());
            var modded_exe = base_directoy.GetFiles().SingleOrDefault(file => file.Name.ToUpper() == modded_exe_name.ToUpper());

            return
                (source_exe.GenerateMD5Hash() == SourceExecutableHash)
                && (modded_exe.GenerateMD5Hash() == ModdedExecutableHash);
            */
        }

        public ModEnvironmentConfiguration(ModEnvironmentConfiguration from)
        {
            _hashes = new HashData(from.Hashes);
            ModReferences = from.ModReferences;
            ClearLogsOnRebuild = from.ClearLogsOnRebuild;
            GraphicsProfile = from.GraphicsProfile;
        }

        protected ModEnvironmentConfiguration()
        {
            GraphicsProfile = Microsoft.Xna.Framework.Graphics.GraphicsProfile.Reach;
            ClearLogsOnRebuild = true;
        }
    }
}
