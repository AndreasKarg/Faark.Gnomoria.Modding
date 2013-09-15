
using System;
using System.Linq;
using System.Runtime.Serialization;
using Faark.Util;
using System.IO;

namespace Faark.Gnomoria.Modding
{
    [DataContract]
    public class ModType
    {

        [DataMember(Name = "ModType")]
        private String _modTypeName;
        private Type _modType;


        public Type TryGetType()
        {
            if (_modType != null)
                return _modType;
            if (_modTypeName != null)
            {
                return _modType = Type.GetType(
                    _modTypeName,
                    an => AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(la => la.FullName == an.FullName),
                    (a, tn, casesense) => a.GetType(tn, false, true),
                    false
                    );
            }
            return null;
        }

        public Type Type
        {
            get
            {
                if (_modType != null)
                    return _modType;
                if (_modTypeName != null)
                {
                    return _modType = Type.GetType(
                        _modTypeName,
                        an => AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(la => la.FullName == an.FullName),
                        (a, tn, casesense) => a.GetType(tn, false, true),
                        true
                        );
                }
                return null;
            }
        }

        public String TypeName
        {
            get
            {
                var tryType = TryGetType();
                return tryType != null ? tryType.AssemblyQualifiedName : _modTypeName;
            }
        }

        public IMod GetInstance()
        {
            return ModEnvironment.Mods[this];
        }

        public ModType(IMod mod)
        {
            _modType = mod.GetType();
            _modTypeName = TypeName;
        }

        public ModType(string typeName)
        {
            _modTypeName = typeName;
        }

        public ModType(Type sysType)
        {
            _modType = sysType;
            _modTypeName = TypeName;
        }

        protected ModType() { }

        public static implicit operator ModType(Mod toCreateFrom)
        {
            return new ModType(toCreateFrom);
        }
    }

    [DataContract]
    public class ModReference : ModType
    {
        [DataMember(Name = "Hash")]
        private String _dllHash;
        [DataMember(Name = "AssemblyFileName")]
        private String _assemblyFileName;
        [DataMember(Name = "SetupData", EmitDefaultValue = false)]
        private String _setupData;

        public String Hash
        {
            get { return _dllHash; }
        }

        public String AssemblyFileName
        {
            get { return _assemblyFileName; }
        }

        public FileInfo AssemblyFile
        {
            get
            {
                return new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), _assemblyFileName));
            }
        }

        public String SetupData
        {
            get
            {
                return _setupData;
            }
        }

        protected ModReference() { }
        public ModReference(IMod mod)
            : base(mod)
        {
            _setupData = mod.SetupData;
            _dllHash = new FileInfo(new Uri(Type.Assembly.CodeBase).LocalPath).GenerateMD5Hash();
            string refPath;

            bool pathFound = FileExtensions.GetRelativePath(Directory.GetCurrentDirectory(), Type.Assembly.CodeBase, out refPath);

            _assemblyFileName = pathFound ? refPath : Type.Assembly.CodeBase;
        }

        public static implicit operator ModReference(Mod toCreateFrom)
        {
            return new ModReference(toCreateFrom);
        }
        
    }
}
