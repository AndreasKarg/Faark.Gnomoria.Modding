using System;
using System.Collections.Generic;
using System.Linq;
using Faark.Gnomoria.Modding;
using Faark.Util;

namespace GnomoriaModUI
{
    //[Serializable]
    public class ModdingEnvironmentWriter
    {
        private readonly ModdingEnvironmentConfiguration _config;
        private readonly GnomoriaExeInjector _gameInjector;
        private readonly Injector _libInjector;
        private readonly IMod[] _allModsToProcess;
        private readonly IMod[] _allPossibleDependencies;
        private readonly List<IMod> _processedMods = new List<IMod>();//order does matter.
        //private HashSet<IMod> processedMods = new HashSet<IMod>();
        private readonly HashSet<IMod> _currentlyProcessing = new HashSet<IMod>();

        private void ProcessMod(IMod mod)
        {
            if (_processedMods.Contains(mod))
            {
                return;
            }

            if (_currentlyProcessing.Contains(mod))
            {
                throw new InvalidOperationException("Can't process mod [" + mod.Name + "], already processing it. Circular dependency?");
            }

            _currentlyProcessing.Add(mod);
            mod.Initialize_PreGeneration();

            if (mod.InitBefore.Any())
            {
                throw new NotImplementedException("Mod.InitAfter and Mod.InitBefore are not yet supported. Sorry.");
            }

            foreach (var dependency in mod.Dependencies)
            {
                ProcessMod(_allModsToProcess.Union(_allPossibleDependencies).Single(depModInstance => depModInstance.GetType() == dependency.Type));
            }

            foreach (var change in mod.Modifications)
            {
                if (_gameInjector.AssemblyContainsType(change.TargetType))
                {
                    _gameInjector.Inject_Modification(change);
                }
                else if (_libInjector.AssemblyContainsType(change.TargetType))
                {
                    _libInjector.Inject_Modification(change);
                }
                else
                {
                    throw new InvalidOperationException("Cannot change behavoir of type [" + change.TargetType + "]!");
                }
            }
            _processedMods.Add(mod);
            _currentlyProcessing.Remove(mod);
        }

        internal ModdingEnvironmentWriter(IMod[] modsToUse, IMod[] dependenciesToUse, bool useHiDefProfile)
        {

            _config = ModdingEnvironmentConfiguration.Create();

            ModEnvironment.RequestSetupDataReset();

            _allModsToProcess = modsToUse;
            _allPossibleDependencies = dependenciesToUse;

            var sourceExe = ModManager.GameDirectory.ContainingFile(ModManager.OriginalExecutable);
                //new System.IO.FileInfo(System.IO.Path.Combine(base_directoy.FullName, source_exe_name));
            var moddedExe = ModManager.GameDirectory.ContainingFile(ModManager.ModdedExecutable);
                //new System.IO.FileInfo(System.IO.Path.Combine(base_directoy.FullName, modded_exe_name));
            var sourceLib = ModManager.GameDirectory.ContainingFile(ModManager.OriginalLibrary);
            var moddedLib = ModManager.GameDirectory.ContainingFile(ModManager.ModdedLibrary);

            _gameInjector = new GnomoriaExeInjector(sourceExe);
            _libInjector = new Injector(sourceLib);
            _config.Hashes.SourceExecutable = sourceExe.GenerateMD5Hash();
            _config.Hashes.SourceLibrary = sourceLib.GenerateMD5Hash();

            // may switch those 2 later to have it outside...
            _gameInjector.Inject_SetContentRootDirectoryToCurrentDir_InsertAtStartOfMain();
            _gameInjector.Inject_CallTo_ModRuntimeController_Initialize_AtStartOfMain(ModManager.GameDirectory.ContainingFile(ModManager.ModController));
            //game_injector.Inject_TryCatchWrapperAroundEverthingInMain_WriteCrashLog();
            //game_injector.Inject_CurrentAppDomain_AddResolveEventAtStartOfMain();
            _gameInjector.Inject_SaveLoadCalls();
            //game_injector.Inject_TryCatchWrapperAroundGnomanEmpire_LoadGame();
            _gameInjector.Debug_ManipulateStuff();

            if (useHiDefProfile)
            {
                _gameInjector.Inject_AddHighDefXnaProfile();
            }

            foreach (var mod in modsToUse)
            {
                ProcessMod(mod);
            }

            // This appears to sort the processed mods by their dependencies and generates an assembly load order from them.
            var allLoadedStuff = _processedMods.Select(mod => Tuple.Create(mod, mod.Dependencies.Union(mod.InitAfter.Where(befor => _processedMods.Contains(befor.GetInstance()))).Select(type => type.GetInstance())));
            var processedModsSortedByDependencyAndInitAfter = DependencySort.Sort(allLoadedStuff);

            _config.SetModReferences(processedModsSortedByDependencyAndInitAfter.Select(mod => new ModReference(mod)).ToArray());

            //Mono.Cecil.WriterParameters
            _gameInjector.Write(moddedExe);
            _libInjector.Write(moddedLib);
            _config.Hashes.ModdedExecutable = moddedExe.GenerateMD5Hash();
            _config.Hashes.ModdedLibrary = moddedLib.GenerateMD5Hash();
        }

        public void SaveEnvironmentConfiguration(System.IO.FileInfo xmlConfigFile)
        {
            _config.Save(xmlConfigFile);
        }
    }

    internal class ModdingEnvironmentConfiguration : ModEnvironmentConfiguration
    {
        public static ModdingEnvironmentConfiguration Create()
        {
            return new ModdingEnvironmentConfiguration();
        }

        public void SetModReferences(ModReference[] refs)
        {
            ModReferences = refs;
        }

        public void Save(System.IO.FileInfo xmlConfigFile)
        {
            Save(new ModEnvironmentConfiguration(this), xmlConfigFile);
        }

        public static ModEnvironmentConfiguration LoadOrCreate(System.IO.FileInfo fileToLoad)
        {
            return fileToLoad.Exists ? Load(fileToLoad) : Create();
        }
    }
}
