//#define NO_ASSEMBLY_LOADING
// see also modding.cs, injector.cs

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Faark.Util;

namespace GnomoriaModUI
{
    internal class LocalModsFinder
    {
        public event EventHandler<EventArgs<IMod>> OnDependencyFound;
        public virtual void DependencyFound(IMod mod)
        {
            OnDependencyFound.TryRaise(this, mod);
        }

        public event EventHandler<EventArgs<IMod>> OnModFound;
        public virtual void ModFound(IMod mod)
        {
            OnModFound.TryRaise(this, mod);
        }
        public event EventHandler OnSearchEnded;

        private readonly List<IMod> _foundDependencies = new List<IMod>();
        private readonly List<IMod> _foundMods = new List<IMod>();

        public virtual void SearchEnded()
        {
            OnSearchEnded.TryRaise(this);
        }

        private void ProcessModType(ModType modType, bool isDep = false)
        {
            try
            {
                var mod = ModEnvironment.Mods[modType];
                if (isDep)
                {
                    _foundDependencies.Add(mod);
                }
                else
                {
                    _foundMods.Add(mod);
                }
                mod.Initialize_ModDiscovery();

                foreach (var dep in mod.Dependencies)
                {
                    if (_foundMods.Count(fmod => fmod.GetType() == dep.Type) > 0) continue;

                    if (_foundDependencies.Count(fdep => fdep.GetType() == dep.Type) <= 0)
                    {
                        ProcessModType(dep, true);
                    }
                }

                if (isDep)
                {
                    DependencyFound(mod);
                }
                else
                {
                    ModFound(mod);
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
                //to log...
            }
        }
        public void RunSync(System.IO.DirectoryInfo gnomoriaBase)
        {
            try
            {
                //Make sure all assemblys are loaded & can be used by/referenced our mods 
                //processModType(typeof(Faark.Gnomoria.Modding.DemoMods.PopulationUI_SyncTabScrolls));
                //processModType(typeof(Faark.Gnomoria.Modding.DemoMods.Game_CreateBackupSavegame));


                var modDir = gnomoriaBase.GetDirectories().Single(sub => sub.Name.ToUpper() == "MODS");
                var modFiles = modDir.GetFiles("*.dll");
                var modAssemblies = new List<Assembly>();

                foreach (var modFile in modFiles)
                {
                    var uri = new Uri(modFile.FullName).AbsoluteUri;
                    var zone = System.Security.Policy.Zone.CreateFromUrl(uri);
                    //MessageBox.Show(zone.SecurityZone + "\n" + uri);
                    var maySecurityProblems = zone.SecurityZone == System.Security.SecurityZone.Internet || zone.SecurityZone == System.Security.SecurityZone.Untrusted;

                    try
                    {
                        modAssemblies.Add(Assembly.LoadFrom(modFile.FullName));
                    }
                    catch (BadImageFormatException)
                    {
                        // Todo: Skipping is the curr solution for non-net-dlls. find a better one or just remove once we use net-dir again.
                    }
                    catch (Exception err)
                    {
                        var msg = "Error loading file: "+modFile.FullName+"\n\n";
                        if (maySecurityProblems)
                        {
                            msg += "This file could be blocked due to security stuff. Try right click it in explorer to open its properties. Check the general tab for 'Security' at the bottom.\n\n\n";
                        }
                        msg += err.ToString();
                        MessageBox.Show(msg);
                    }
                }

                foreach (var assembly in modAssemblies)
                {
                    try
                    {
                        var modTypes = assembly.GetTypes().Where(t =>
                        {
                            return typeof(IMod).IsAssignableFrom(t) && !(typeof(SupportMod).IsAssignableFrom(t));
                        });

                        foreach (var modType in modTypes)
                        {
                            ProcessModType(new ModType(modType));
                        }
                    }
                    catch (ReflectionTypeLoadException)
                    {
                        continue;
                    }
                    catch (Exception err)
                    {
                        MessageBox.Show(err.ToString());
                    }
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.ToString());
            }

            SearchEnded();
        }

        public void RunAsync()
        {
            throw new NotImplementedException();
        }
    }
}
