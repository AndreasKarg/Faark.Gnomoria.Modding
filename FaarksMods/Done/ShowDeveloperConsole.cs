using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game;
using Game.GUI;
using Microsoft.Xna.Framework;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// This mod allows access to the developer consol and item spawn menu.
    /// </summary>
    public class ShowDeveloperConsole: Mod
    {
        public override string Author
        {
            get
            {
                return "Faark";
            }
        }

        public override string Name
        {
            get
            {
                return "Show Developer Tools";
            }
        }

        public override string Description
        {
            get
            {
                return "Allows you to show the games own, though every limited developer console as well as an UI to spawn items. Use it at your own risk.";
            }
        }

        public override IEnumerable<IModification> Modifications
        {
            get
            {
                yield break;
            }
        }

        public override IEnumerable<ModDependency> Dependencies
        {
            get
            {
                yield return Modding.HelperMods.ModRightClickMenu.Instance;
            }
        }

        public override void Initialize_PreGeneration()
        {
            Modding.HelperMods.ModRightClickMenu.AddItem("Toggle Developer Console", ToggleDeveloperConsole);
            Modding.HelperMods.ModRightClickMenu.AddItem("Item spawn menu", ItemSpawnMenu);
            base.Initialize_PreGeneration();
        }

        private static ConsoleWindow _console;
        public static void ToggleDeveloperConsole()
        {
            if ((_console != null) && GnomanEmpire.Instance.GuiManager.Manager.Controls.Contains(_console))
            {
                GnomanEmpire.Instance.GuiManager.Remove(_console);
                _console = null;
            }
            else
            {
                GnomanEmpire.Instance.GuiManager.Add(_console = new ConsoleWindow(GnomanEmpire.Instance.GuiManager.Manager));
            }
        }

        public static void ItemSpawnMenu() 
        {
            var pos = (Vector3)typeof(RightClickMenu)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(f => f.FieldType == typeof(Vector3))
                .GetValue(
                    typeof(HUD)
                        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .Single(f => f.FieldType == typeof(RightClickMenu))
                        .GetValue(
                            typeof(InGameHUD)
                                .GetFields(BindingFlags.NonPublic| BindingFlags.Instance)
                                .Single(f => f.FieldType == typeof(HUD))
                                .GetValue(GnomanEmpire.Instance.GuiManager.HUD)
                        )
                );
            GnomanEmpire.Instance.GuiManager.HUD.ShowWindow(new SpawnItemUI(GnomanEmpire.Instance.GuiManager.Manager, pos));
        }
    }
#endif
}