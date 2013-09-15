
using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Faark.Util;

namespace Faark.Gnomoria.Modding
{
    /// <summary>
    /// Serves as some kind of "mod registery". Since we have to make sure that every mod is instanciated only once,
    /// it will take care of creating them via Activator.
    /// 
    /// Mods can also 
    /// </summary>
    public static class ModEnvironment
    {
        public enum EnvironmentStatus { Unspecified, /*ModSelection,*/ InGame }
        public static EnvironmentStatus Status { get; internal set; }

        public class ModCollection : IEnumerable<IMod>
        {

            private readonly Dictionary<Type, IMod> _loadedMods = new Dictionary<Type, IMod>();

            public IMod Get(ModType type)
            {
                var sysType = type.Type;
                IMod mod;
                if (_loadedMods.TryGetValue(sysType, out mod))
                {
                    return mod;
                }
                mod = _loadedMods[sysType] = (IMod)Activator.CreateInstance(sysType);
                return mod;
            }

            public T1 Get<T1>() where T1: IMod, new()
            {
                IMod mod;
                if (_loadedMods.TryGetValue(typeof(T1), out mod))
                {
                    return (T1)mod;
                }
                var newMod = new T1();
                _loadedMods[typeof(T1)] = newMod;
                return newMod;
            }

            public bool Has(ModType type)
            {
                return _loadedMods.ContainsKey(type.Type);
            }

            public IMod this[ModType type]
            {
                get
                {
                    return Get(type);
                }
            }

            public IMod this[Type type]
            {
                get
                {
                    return Get(new ModType(type));
                }
            }

            public IEnumerator<IMod> GetEnumerator()
            {
                // Todo: Find out whether this is equivalent to el.GetEnumerator()
                return _loadedMods.Select(el => el.Value).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private static readonly ModCollection _mods = new ModCollection();
        public static ModCollection Mods
        {
            get
            {
                return _mods;
            }
        }

        public static event EventHandler ResetSetupData;
        public static void RequestSetupDataReset()
        {
            ResetSetupData.TryRaise(null);
        }

    }
}
