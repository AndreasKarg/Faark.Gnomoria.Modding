using System;
using System.Collections.Generic;
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
    /// This mod adds a "prev" and "next" button to the character window. UI class doesn't like insta-clicking though, however.
    /// </summary>
    public class NextCharButtonInCharWindow : Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                yield return new MethodHook(
                    typeof(ViewCharacterUI).GetConstructor(new[] { typeof(Manager), typeof(Character) }),
                    Method.Of<ViewCharacterUI, Manager, Character>(OnCreate_ViewCharacterUI)
                    );
            }
        }

        private static FieldInfo _tabbedWindowTabControlFieldInfo;

        public override void Initialize_PreGame()
        {
            _tabbedWindowTabControlFieldInfo = typeof(TabbedWindow).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(f => f.FieldType == typeof(TabControl));
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
                return "This mod adds a \"prev\" and \"next\" button to the character window. UI class doesn't like insta-clicking though, however.";
            }
        }

        private static Button _currentPrevButton; 
        private static Button _currentNextButton;

        private static T GetPrev<T>(IEnumerable<T> list, T item )
        {
            T currentPrev = default(T);
            foreach (var cur in list)
            {
                if (EqualityComparer<T>.Default.Equals(cur, item))
                {
                    return currentPrev;
                }
                
                currentPrev = cur;
            }
            return default(T);
        }

        private static T GetNext<T>(IEnumerable<T> list,T item )
        {
            bool isMatch = false;

            foreach (var cur in list)
            {
                if (isMatch)
                {
                    return cur;
                }
                
                if (EqualityComparer<T>.Default.Equals(cur, item))
                {
                    isMatch = true;
                }
            }
            return default(T);
        }

        public static void ToggleTo(ViewCharacterUI old, Manager mgr, Character newChar)
        {
            var hud = GnomanEmpire.Instance.GuiManager.HUD;
            var tc = (TabControl)_tabbedWindowTabControlFieldInfo.GetValue(old);
            var selPage = tc.SelectedIndex;
            if (hud.ActiveWindow == old)
            {
                hud.PopActiveWindow();  
            }
            //old.Close();
            var newWindow = new ViewCharacterUI(mgr, newChar);
            GnomanEmpire.Instance.GuiManager.HUD.ShowWindow(newWindow);
            newWindow.Focused = true;
            var newTc = (TabControl)_tabbedWindowTabControlFieldInfo.GetValue(newWindow);
            newTc.SelectedIndex = selPage;
        }

        public static void OnCreate_ViewCharacterUI(ViewCharacterUI self, Manager mgr, Character chr)
        {
            var chrList = GnomanEmpire.Instance.World.AIDirector.PlayerFaction.Members.Select( m => m.Value ).OrderBy( m => m.Mind.Profession.Title );
            if (!chrList.Contains(chr))
            {
                return;
            }

            var centerWindow = self.ClientWidth / 2;

            var next = GetNext(chrList, chr);
            var buttonNext = _currentNextButton = new Button(mgr);
            buttonNext.Init();
            buttonNext.Text = "N";
            buttonNext.Width = 30;
            buttonNext.Anchor = Anchors.Bottom;
            buttonNext.Left = centerWindow + 3;
            buttonNext.Top = self.ClientHeight - buttonNext.Height - 3;
            buttonNext.Click += (sender, args) =>
            {
                args.Handled = true;
                ToggleTo(self, mgr, next);
                if (_currentNextButton.Enabled)
                {
                    _currentNextButton.Focused = true;
                }
            };

            if (next == null)
            {
                buttonNext.Enabled = false;
            }
            else
            {
                buttonNext.ToolTip = new ToolTip(mgr) { Text = "Next: " + next.NameAndTitle() };
            }

            var prev = GetPrev(chrList, chr);
            var buttonPrev = _currentPrevButton = new Button(mgr);
            buttonPrev.Init();
            buttonPrev.Text = "P";
            buttonPrev.Width = 30;
            buttonPrev.Anchor = Anchors.Bottom;
            buttonPrev.Top = self.ClientHeight - buttonPrev.Height - 3;
            buttonPrev.Left = centerWindow - buttonPrev.Width - 3;
            
            // Todo: Find out why resharper complains about the implicit closure
            buttonPrev.Click += (sender, args) =>
            {
                args.Handled = true;
                ToggleTo(self, mgr, prev);
                if (_currentPrevButton.Enabled)
                {
                    _currentPrevButton.Focused = true;
                }
            };

            if (prev == null)
            {
                buttonPrev.Enabled = false;
            }
            else
            {
                buttonPrev.ToolTip = new ToolTip(mgr) { Text = "Prev: " + prev.NameAndTitle() };
            }
            self.Closed += (sender, args) =>
            {
                if (_currentNextButton == buttonNext)
                    _currentNextButton = null;
                if (_currentPrevButton == buttonPrev)
                    _currentPrevButton = null;
            };

            self.Add(buttonNext);
            self.Add(buttonPrev);
        }
    }
#endif
}