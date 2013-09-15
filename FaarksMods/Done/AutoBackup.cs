using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Faark.Gnomoria.Modding;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// This mod creates a copy of your new save file after every save.
    /// </summary>
    public class Game_CreateBackupSavegame : Mod
    {
        // Todo: Maybe move this class to Game-XX namespace?

        public override IEnumerable<IModification> Modifications
        {
            get
            {
                // this function have to return configuration data about this mod.

                // usually we want to hook into the game at some point, so to let the modui set this up
                // we first need a System.Reflection.MethodBase reference to both the functions we want to hook
                // into and our own that shall be called

                // there are 2 ways to get our method handles. Lets start with using System.Reflection. In that case it is easy,
                // since there is just one SaveGame that is public. Otherwise you would may have to do other stuff, may search in .GetMethods(flags)
                var originalFunction = typeof(Game.GnomanEmpire).GetMethod("SaveGame");

                // But for public static functions, such as all of ours, you should use the following little helper. 
                // The huge advantage: Compile time errors when e.g. renaming the method or just a spelling mistake
                var ourFunction = Method.Of<Task, Game.GnomanEmpire, bool, Task>(OnAfter_SaveGame_Started);

                // and finally we want to return the list of all hooks (currently only one)
                return new IModification[]{
                    new MethodHook(
                        originalFunction,
                        ourFunction
                    )
                };
            }
        }

        // A little effort to make your mod look nice in the mod list.
        public override string Author
        {
            get
            {
                return "Faark";
            }
        }

        public override string Description
        {
            get
            {
                return "Automatic backup of game saves.";
            }
        }

        public static Task OnAfter_SaveGame_Started(Task saveTask, Game.GnomanEmpire self, bool fallenKingdom)
        {
            // This function is hooked into the game as specified by GetConfig().
            // Be careful to exactly match the required structure (see other doc, sth like [RetVal|void]([retVal][this][arguments]) )
            // Currently there isn't any flexibility, so you have to specifiy all that apply
            return saveTask.ContinueWith((task) =>
            {
                try
                {
                    //scaning for the newest dated looks like the easies way to get the latest save, since i havent found any "get current save file"-func
                    var latestSaveFile = new System.IO.DirectoryInfo(Game.GnomanEmpire.SaveFolderPath("Worlds\\"))
                        .GetFiles()
                        .Where(file => file.Extension.ToUpper() == ".SAV")
                        .Aggregate((a, b) =>
                        {
                            return a.LastWriteTime > b.LastWriteTime ? a : b;
                        });

                    Debug.Assert(latestSaveFile.DirectoryName != null, "latestSaveFile.DirectoryName != null");

                    var backupFolder = System.IO.Path.Combine(latestSaveFile.DirectoryName, "Backups");
                    System.IO.Directory.CreateDirectory(backupFolder);
                    System.IO.File.Copy(
                        latestSaveFile.FullName,
                        System.IO.Path.Combine(
                            backupFolder,
                            latestSaveFile.Name.Remove(latestSaveFile.Name.Length - latestSaveFile.Extension.Length)
                            + "_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                            + latestSaveFile.Extension
                            )
                        );
                }
                catch (Exception err)
                {
                    // Todo: Do something meaningful here...
                    RuntimeModController.Log.Write(err);
                }
            });

            //don't want to return a modified value? In that case you have to return the return_val parameter! (usually first argument)
            //return save_task;
        }
    }
#endif
}
