using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Faark.Gnomoria.Modding;

namespace GnomoriaModUI
{
    internal static class GameLauncher
    {
        public static void Run()
        {
            var domainSetup = new AppDomainSetup
            {
                ApplicationBase = Path.Combine(Directory.GetCurrentDirectory(), "Mods"),
                ConfigurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile,
                ApplicationName = AppDomain.CurrentDomain.SetupInformation.ApplicationName,
                LoaderOptimization = LoaderOptimization.SingleDomain
            };
            var ad = AppDomain.CreateDomain("GnomoriaDebugEnvironment", null, domainSetup);
            //ad.ExecuteAssemblyByName(AppDomain.CurrentDomain.FriendlyName, "-NoResolve");
            //ad.ExecuteAssembly("GnomoriaModded.exe");

            var cd = (CustomDomainForModRuntime)ad.CreateInstanceFromAndUnwrap(
                new Uri(typeof(CustomDomainForModRuntime).Assembly.CodeBase).LocalPath,
                typeof(CustomDomainForModRuntime).FullName
                );
            cd.RunGame();
        }
    }

    internal class CustomDomainForModRuntime : MarshalByRefObject
    {
        // Warning: Make sure there are no dependency-related things in here that could trigger an auto load
        string _baseDir;
        private Exception _lastFirstChanceException;
        private bool _currentlyHandlingException;

        public void RunGame()
        {
            //throw new Exception("LOGGING IS OFF!");
            _baseDir = Environment.CurrentDirectory;
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_OnAssemblyResolve;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            var os = Environment.OSVersion;

            if (os.Version.Major > 5)
            {
                AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_OnFirstChanceException;
            }

            try
            {
                var ass = Assembly.Load(File.ReadAllBytes("GnomoriaModded.dll"));
                var ep = ass.EntryPoint;
                //var inst = ass.GetType("Game.GnomanEmpire").GetProperty("Instance").GetGetMethod().Invoke(null, new object[] { });
                //var obj = ass.CreateInstance(ep.Name);
                ep.Invoke(null, new object[] { new[] { "-noassemblyresolve", "-noassemblyloading" } });
            }
            catch (Exception err)
            {
                CustomErrorHandler(err);
            }
        }

        void CurrentDomain_OnFirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            handleStuff_Enter();
            if (!(e.Exception is System.Threading.ThreadAbortException))
            {
                RuntimeModController.Log.Write("FirstChanceException", e.Exception);
                _lastFirstChanceException = e.Exception;
            }
            handleStuff_Leave();
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            handleStuff_Enter();
            if (e.ExceptionObject == _lastFirstChanceException)
            {
                RuntimeModController.Log.Write("First Chance Exception is not handled" + (e.IsTerminating ? ", terminating." : "."));
            }
            else
            {
                RuntimeModController.Log.Write(e.IsTerminating ? "UnhandledException (t)" : "UnhandledException", e.ExceptionObject as Exception);
            }
            handleStuff_Leave();
        }

        private void handleStuff_Enter()
        {
            if (_currentlyHandlingException)
            {
                AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
                AppDomain.CurrentDomain.FirstChanceException -= CurrentDomain_OnFirstChanceException;
                RuntimeModController.Log.Write("Tried to handle an error while already doing this.");
                throw new Exception("Tried to handle an error while already doing it...");
            }
            _currentlyHandlingException = true;
        }

        private void handleStuff_Leave()
        {
            _currentlyHandlingException = false;
        }

        Assembly CurrentDomain_OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var trgName = new AssemblyName(args.Name);

            foreach (var a in assemblies)
            {
                var aN = new AssemblyName(a.FullName);
                if (aN.Name == trgName.Name)
                {
                    return a;
                }
            }

            if (trgName.Name == "gnomorialib")
            {
                return Assembly.Load(File.ReadAllBytes(Path.Combine(_baseDir, trgName.Name + "Modded.dll")));
            }

            var file = Path.Combine(_baseDir, trgName.Name + ".dll");
            if (trgName.Name == "GnomoriaModController")
            {
                return Assembly.Load(File.ReadAllBytes(file));
            }

            return File.Exists(file) ? Assembly.LoadFile(file) : null;
        }

        void CustomErrorHandler(Exception err)
        {
            RuntimeModController.Log.Write("UnhandledException", err);
            MessageBox.Show(
                "Sorry, but Gnomoria has crashed." + Environment.NewLine
                + Environment.NewLine
                + Environment.NewLine
                + "Check out these logfiles for more information:" + Environment.NewLine
                + RuntimeModController.Log.GetLogfile().FullName + Environment.NewLine
                + RuntimeModController.Log.GetGameLogfile().FullName,
                "Gnomoria [modded] has crashed.");
        }
    }
}
