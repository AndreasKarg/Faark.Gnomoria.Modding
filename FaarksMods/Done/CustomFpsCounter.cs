using System;
using System.Collections.Generic;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Game;
using Game.GUI.Controls;
using Microsoft.Xna.Framework;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// A custom FPS counter. It was planned to add more metrics and/or convert it into a graph, but never got around to do it.
    /// </summary>
    public class CustomFpsCounter: Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                yield return new MethodHook(
                    typeof(GnomanEmpire).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance),
                    Method.Of<GnomanEmpire, GameTime>(GnomanEmpire_Update),
                    MethodHookType.RunBefore
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
                return "A custom FPS counter. It was planned to add more metrics and/or convert it into a graph, but never got around to do it.";
            }
        }

        private static readonly TimeSpan UpdateDisplayEvery = TimeSpan.FromMilliseconds(150);
        private static readonly TimeSpan GatherDataTimespanShot = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan GatherDataTimespanLong = TimeSpan.FromMinutes(1);

        private class HCtr
        {
            private TimeSpan _totalTime = TimeSpan.Zero;
            private readonly LinkedList<TimeSpan> _times = new LinkedList<TimeSpan>();
            private readonly TimeSpan _recTime;

            public HCtr(TimeSpan recordTime)
            {
                _recTime = recordTime;
            }

            public double Value
            {
                get
                {
                    return _times.Count / _totalTime.TotalSeconds;
                }
            }

            public string Text
            {
                get
                {
                    return Value.ToString("0.00");
                }
            }

            public void AddFrame(TimeSpan frameTime)
            {
                _times.AddLast(frameTime);
                _totalTime += frameTime;
                while ((_totalTime - _times.First.Value) > _recTime)
                {
                    _totalTime -= _times.First.Value;
                    _times.RemoveFirst();
                }
            }
        }

        private static readonly HCtr ShortRec = new HCtr(GatherDataTimespanShot);
        private static readonly HCtr LongRec = new HCtr(GatherDataTimespanLong);

        private static TimeSpan _nextDisplayUpdate;
        private static Label _fpsDisplay;

        private static void UpdateDisplayedFps()
        {
            if (_fpsDisplay == null || _fpsDisplay.Manager != GnomanEmpire.Instance.GuiManager.Manager)
            {
                /*
                var r = new Random();
                var s = 4096 * 2;
                //var s = 4096;
                var tex = new Microsoft.Xna.Framework.Graphics.Texture2D(GnomanEmpire.Instance.GraphicsDevice, s, s, false, Microsoft.Xna.Framework.Graphics.SurfaceFormat.Color);
                tex.SetData(Enumerable.Repeat(1, s * s).Select(foo => new Color(r.Next(256), r.Next(256), r.Next(256))).ToArray());
                */
                _fpsDisplay = new Label(GnomanEmpire.Instance.GuiManager.Manager);
                _fpsDisplay.Init();
                _fpsDisplay.Anchor = Anchors.Bottom | Anchors.Left;
                _fpsDisplay.Width = 250;
                _fpsDisplay.Height = 25;
                _fpsDisplay.Top = GnomanEmpire.Instance.GuiManager.Manager.ScreenHeight - _fpsDisplay.Height;
                _fpsDisplay.Left = 10;
                //fps_display.Color = ;
                _fpsDisplay.TextColor = Color.LightGreen;
                /*fps_display.Draw += new DrawEventHandler((sender, args) =>
                {
                    args.Renderer.SpriteBatch.Draw(tex, Vector2.Zero, Color.White);
                });*/
                GnomanEmpire.Instance.GuiManager.Add(_fpsDisplay);
            }
            _fpsDisplay.Text = ShortRec.Text + " FPS (" + LongRec.Text + " avg)";
        }

        public static void GnomanEmpire_Update(GnomanEmpire self, GameTime gt)
        {
            ShortRec.AddFrame(gt.ElapsedGameTime);
            LongRec.AddFrame(gt.ElapsedGameTime);
            if (_nextDisplayUpdate < gt.TotalGameTime)
            {
                _nextDisplayUpdate = gt.TotalGameTime + UpdateDisplayEvery;
                UpdateDisplayedFps();
            }
        }
    }
#endif
}
