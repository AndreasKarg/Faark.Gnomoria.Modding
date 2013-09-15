using System;
using System.Collections.Generic;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game.GUI.Controls;
using Microsoft.Xna.Framework;

namespace Faark.Gnomoria.Mods
{
    /// <summary>
    /// Makes the right click menu a little smoother to use.
    /// </summary>
    public class SmoothedRightClickSubmenus : Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                return new IModification[]{
                    new MethodHook(
                        typeof(ContextMenu).GetMethod("Show", new[] { typeof(Control), typeof(int), typeof(int) }),
                        Method.Of<ContextMenu, Control, int, int>(ContextMenu_Show)
                        ),
                    new MethodHook(
                        typeof(ContextMenu).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic),
                        Method.Of<ContextMenu, MouseEventArgs, bool>(ContextMenu_OnMouseMove),
                        MethodHookType.RunBefore,
                        MethodHookFlags.CanSkipOriginal
                        ),
                    new MethodHook(
                        typeof(ContextMenu).GetMethod("OnMouseOut", BindingFlags.Instance | BindingFlags.NonPublic),
                        Method.Of<ContextMenu, MouseEventArgs>(ContextMenu_OnMouseOut)
                        ),
                    new MethodHook(
                        typeof(ContextMenu).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic),
                        Method.Of<ContextMenu, GameTime>(ContextMenu_Update)
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
                return "Makes the right click menu a little smoother to use.";
            }
        }

        private static MethodInfo _contextMenuOnMouseMoveFunc;
        private static PropertyInfo _menuBaseParentMenu;
        private class MoveTowardsVaildater
        {
            public readonly Point UpperBound;
            public readonly Point LowerBound;

            public Point LastPos;
            public MoveTowardsVaildater(Rectangle targetRect, Point absMousePosition)
            {
                var xpos = absMousePosition.X < targetRect.Left ? targetRect.Left : targetRect.Right;
                UpperBound = new Point(xpos, targetRect.Top);
                LowerBound = new Point(xpos, targetRect.Bottom);

                LastPos = absMousePosition;
            }

            private static float Sign(Point p1, Point p2, Point p3)
            {
                return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
            }

            private static bool IsPointInTri(Point pt, Point v1, Point v2, Point v3)
            {
                bool b1 = Sign(pt, v1, v2) < 0.0f;
                bool b2 = Sign(pt, v2, v3) < 0.0f;
                bool b3 = Sign(pt, v3, v1) < 0.0f;

                return ((b1 == b2) && (b2 == b3));
            }

            public bool IsMovingTowards(Point absMousePosition)
            {
                var ret = IsPointInTri(absMousePosition, UpperBound, LowerBound, LastPos);
                LastPos = absMousePosition;
                /*if (Math.Abs(XPos - abs_mouse_position.X) < Math.Abs(XPos - LastPos.X))
                {
                    var moved_distance = new Point(LastPos.X - abs_mouse_position.X, LastPos.Y - abs_mouse_position.Y);
                    var trg_distance
                    if( 
                    return true;
                }*/
                return ret;
            }
        }
        private static ContextMenu _lastOpenedContextMenu;
        private static ContextMenu _lastOpeningMenu;
        private static MoveTowardsVaildater _lastOpenedMoveValidater;
        private static Point _lastMousePosition;
        private static DateTime _openMenuWhenMouseStandingUntil;
        private static MouseEventArgs _lastMouseEventArgs;

        public override void Initialize_PreGame()
        {
            _menuBaseParentMenu = typeof(MenuBase).GetProperty("ParentMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            _contextMenuOnMouseMoveFunc = typeof(ContextMenu).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void ContextMenu_Show(ContextMenu self, Control sender, int x, int y)
        {
            _lastOpenedContextMenu = self;
            _lastOpenedMoveValidater = new MoveTowardsVaildater(self.AbsoluteRect, _lastMousePosition);
            _openMenuWhenMouseStandingUntil = DateTime.MaxValue;
            _lastOpeningMenu = (ContextMenu)_menuBaseParentMenu.GetValue(self, new object[] { });

            //RuntimeModController.WriteLogO(self, LastOpeningMenu);
            //LastOpenedMenuRect = self.AbsoluteRect;
        }

        public static bool ContextMenu_OnMouseMove(ContextMenu self, MouseEventArgs args)
        {
            _lastMouseEventArgs = args;
            var currentMousePos = _lastMousePosition = new Point(self.AbsoluteLeft + args.Position.X, self.AbsoluteTop + args.Position.Y);

            if (_lastOpenedContextMenu == null) return false;
            if (_lastOpenedContextMenu == self) return false;

            if (_lastOpenedMoveValidater.LastPos == currentMousePos)
            {
                //should only happen if called by our update-hack...
            }
            else if (_lastOpenedMoveValidater.IsMovingTowards(currentMousePos))
            {
                //RuntimeModController.WriteScreen("Skipped, " + DateTime.Now.Ticks);
                _openMenuWhenMouseStandingUntil = DateTime.Now + TimeSpan.FromMilliseconds(100);

                return true;
            }
            //RuntimeModController.WriteScreen(null, "Passed, " + DateTime.Now.Ticks);
            return false;
        }

        public static void ContextMenu_OnMouseOut(ContextMenu self, MouseEventArgs args)
        {
            if (_lastOpeningMenu != self) return;

            //RuntimeModController.WriteScreen(null, null, null, "Out, " + DateTime.Now.Ticks);
            _lastOpenedContextMenu = null;
            _lastOpeningMenu = null;
        }

        public static void ContextMenu_Update(ContextMenu self, GameTime gt)
        {
            if ((_lastOpenedContextMenu == null) || (_lastOpeningMenu != self)) return;
            if (DateTime.Now <= _openMenuWhenMouseStandingUntil) return;

            //RuntimeModController.WriteLogO("CLICKING", self);

            _contextMenuOnMouseMoveFunc.Invoke(self, new object[] { _lastMouseEventArgs });
            _contextMenuOnMouseMoveFunc.Invoke(self, new object[] { _lastMouseEventArgs });
            //ContextMenu_OnClickFunc.Invoke(self, new object[] { new MouseEventArgs(default(Microsoft.Xna.Framework.Input.MouseState), MouseButton.None, 0, Point.Zero) });
            //RuntimeModController.WriteScreen(null, null, "Timeclick, " + DateTime.Now.Ticks);

            _openMenuWhenMouseStandingUntil = DateTime.MaxValue;
        }
    }
}
