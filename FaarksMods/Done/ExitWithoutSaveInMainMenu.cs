using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game;
using Game.GUI;
using Game.GUI.Controls;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// This mod allows players to leave the current world without saving
    /// </summary>
    public class ExitWithoutSaveInMainMenu: Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                yield return new MethodHook(
                    typeof(PauseMenu).GetConstructors().First(),
                    Method.Of<PauseMenu, Manager>(OnCreate_PauseMenu)
                    );
            }
        }

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
                return "This mod allows players to leave the current world without saving";
            }
        }

        public static void OnCreate_PauseMenu(PauseMenu self, Manager mgr)
        {
            if (GnomanEmpire.Instance.IsGameOver()) return;

            var panel = typeof(PauseMenu)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(f => f.FieldType == typeof(Panel))
                .GetValue(self)
                as Panel;

            Debug.Assert(panel != null, "panel != null");

            var lastBtn = panel.Controls.Last();
            var button = new Button(mgr);

            button.Init();
            button.Width = 200;
            button.Top = lastBtn.Top + lastBtn.Height + lastBtn.Margins.Bottom + button.Margins.Top - lastBtn.Margins.Top; //yea, lastMarginTop is excluded between save buttons
            button.Left = (panel.Width - button.Width) / 2;
            //button.Margins = new Margins(0, 2, 0, 2);
            button.Text = "Exit (no Save)";
            button.Click += (sender, args) => GnomanEmpire.Instance.MoveToMainMenu();
            panel.Height = button.Top + button.Height;
            panel.Add(button);
        }
    }
#endif
}
