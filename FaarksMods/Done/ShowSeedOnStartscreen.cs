using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Faark.Gnomoria.Modding;

namespace Faark.Gnomoria.Mods
{
    /// <summary>
    /// Displays the seed for that automatically generated world on the start screen. You can click it to create a new world with that seed.
    /// </summary>
    public class ShowSeedOnMainMenu : Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                return new IModification[]{
                    new MethodHook(
                        typeof(Game.GUI.MainMenuWindow).GetConstructor(new[] { typeof(Game.GUI.Controls.Manager) }),
                        Method.Of<Game.GUI.MainMenuWindow, Game.GUI.Controls.Manager>(OnAfter_MainMenuWindow_Created)
                        ),
                    new MethodHook(
                        typeof(Game.GnomanEmpire).GetMethod("PreviewMap", BindingFlags.Instance| BindingFlags.Public),
                        Method.Of<Game.GnomanEmpire, Game.CreateWorldOptions, bool, int>(OnAfter_PreviewMap)
                        )
                };
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
                return
                    "Displays the seed for that automatically generated world on the start screen. You can click it to create a new world with that seed.";
            }
        }

        private static string _seed;
        private static Game.GUI.Controls.Label _versionLabel;
        private static Game.GUI.Controls.Label _seedLabel;
        private static uint _lastSeed;

        public static void OnAfter_MainMenuWindow_Created(Game.GUI.MainMenuWindow self, Game.GUI.Controls.Manager mgr)
        {
            _seedLabel = null;
            _versionLabel = (Game.GUI.Controls.Label)typeof(Game.GUI.MainMenuWindow)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(field => field.FieldType == typeof(Game.GUI.Controls.Label))
                .GetValue(self);
            TryLabelUpdate();
        }

        public static void OnAfter_PreviewMap(Game.GnomanEmpire self, Game.CreateWorldOptions worldOptions, bool clear, int xyScale)
        {
            var task = (System.Threading.Tasks.Task)typeof(Game.GnomanEmpire)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                .Single(field => field.FieldType == typeof(System.Threading.Tasks.Task))
                .GetValue(self);
            task.ContinueWith(t =>
            {
                _seed = (_lastSeed = self.Map.WorldSeed).ToString(CultureInfo.InvariantCulture);
                TryLabelUpdate();
            });
        }

        private static void TryLabelUpdate()
        {
            if ((_seed == null) || (_versionLabel == null)) return;

            int right;

            if (_seedLabel == null)
            {
                _seedLabel = new Game.GUI.Controls.Label(_versionLabel.Manager);
                _seedLabel.Init();
                _seedLabel.Anchor = Game.GUI.Controls.Anchors.Left | Game.GUI.Controls.Anchors.Bottom;
                _seedLabel.Top = _versionLabel.Top;
                var defaultColor = _seedLabel.TextColor = _versionLabel.TextColor;
                _seedLabel.ToolTip = new Game.GUI.Controls.ToolTip(_versionLabel.Manager) { Text = "Create a new world with this Seed." };
                _seedLabel.Passive = false;
                _seedLabel.CanFocus = true;
                /*seed_label.MouseMove += new Game.GUI.Controls.MouseEventHandler((sender, args) =>
                    {
                        (sender as Game.GUI.Controls.Label).TextColor = Microsoft.Xna.Framework.Color.LightGreen;
                    });*/
                _seedLabel.MouseOver += (sender, args) =>
                {
                    var label = sender as Game.GUI.Controls.Label;
                    if (label != null)
                        label.TextColor = Microsoft.Xna.Framework.Color.LightGreen;
                };

                _seedLabel.MouseOut += (sender, args) =>
                {
                    var label = sender as Game.GUI.Controls.Label;
                    if (label != null)
                        label.TextColor = defaultColor;
                };

                // Todo: Cleanup
                _seedLabel.Click += (sender, args) =>
                {
                    Game.GnomanEmpire.Instance.GuiManager.MenuStack.PushWindow(
                        new Game.GUI.AdvancedSetupWindow(
                            ((Game.GUI.Controls.Control)sender).Manager, new Game.CreateWorldOptions
                            { 
                                Seed = _lastSeed,
                                KingdomName = Game.GnomanEmpire.Instance.World.LanguageManager.RandomFactionName(Game.GnomanEmpire.Instance.World.AIDirector.FactionDefs[0].Language)
                            })
                        );
                };

                _versionLabel.Parent.Add(_seedLabel);
                right = _versionLabel.Left + _versionLabel.Width;
                _versionLabel.Text += "; ";
                _versionLabel.Width = (int)_versionLabel.Skin.Layers[0].Text.Font.Resource.MeasureString(_versionLabel.Text).X + 2;
            }
            else
            {
                right = _seedLabel.Left + _seedLabel.Width;
            }

            _seedLabel.Text = _seed;
            _seedLabel.Width = (int)_seedLabel.Skin.Layers[0].Text.Font.Resource.MeasureString(_seedLabel.Text).X + 2;
            _seedLabel.Left = right - _seedLabel.Width;
            _versionLabel.Left = _seedLabel.Left - _versionLabel.Width;
            _seed = null;
            _versionLabel = null;
        }
    }

}
