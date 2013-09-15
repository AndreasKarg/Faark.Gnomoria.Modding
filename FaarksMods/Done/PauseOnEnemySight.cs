﻿using System;
using System.Collections.Generic;
using Faark.Gnomoria.Modding;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// This mods prevents the game from focusing the enemy, once a new one is spotted.
    /// </summary>
    public class DontGotoOnEnemySightPause : Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                return new IModification[]{
                    new MethodHook(
                        typeof(Game.Character).GetMethod("Spotted"),
                        Method.Of<Game.Character>(Before_Spotted),
                        MethodHookType.RunBefore
                        ),
                    new MethodHook(
                        typeof(Game.Character).GetMethod("Spotted"),
                        Method.Of<Game.Character>(After_Spotted),
                        MethodHookType.RunAfter
                        ),
                    new MethodHook(
                        typeof(Game.Camera).GetMethod("MoveTo", new[] { typeof(Microsoft.Xna.Framework.Vector3), typeof(bool) }),
                        Method.Of<Game.Camera, Microsoft.Xna.Framework.Vector3, bool, bool>(Camera_MoveTo),
                        MethodHookType.RunBefore,
                        MethodHookFlags.CanSkipOriginal
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
                return "This mods prevents the game from focusing the enemy, once a new one is spotted.";
            }
        }

        private static bool _allowCameraMoveTo = true;
        public static void Before_Spotted(Game.Character self)
        {
            _allowCameraMoveTo = false;
        }
        public static void After_Spotted(Game.Character self)
        {
            _allowCameraMoveTo = true;
        }
        public static bool Camera_MoveTo(Game.Camera self, Microsoft.Xna.Framework.Vector3 pos, bool stopFollowing)
        {
            return !_allowCameraMoveTo;
        }
    }
#endif
}

