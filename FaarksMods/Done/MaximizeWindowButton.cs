using System;
using System.Collections.Generic;
using System.Reflection;
using Faark.Gnomoria.Modding;
using Faark.Gnomoria.Modding.ContentMods;
using Game;
using Game.GUI.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Faark.Gnomoria.Mods
{
#if true
    /// <summary>
    /// Adds a button to insta-maximize the current window.
    /// </summary>
    public class MaximizeWindowButton: Mod
    {
        public override IEnumerable<IModification> Modifications
        {
            get
            {
                return new IModification[]{
                    new MethodAddVirtual(
                        typeof(Window),
                        typeof(Control).GetProperty("Resizable").GetSetMethod(),
                        Method.Of<Window, bool>(OnSet_Window_Resizable),
                        MethodHookType.RunBefore
                        ),
                    new MethodHook(
                        typeof(Skin).GetMethod("Init"),
                        Method.Of<Skin>(On_Skin_Init)
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
                return "Adds a button to insta-maximize the current window.";
            }
        }
        private static Window _currentSelf;
        private static Button _currentWinMaxButton;

        private static Texture2D _graphicsTex;

        private static readonly Dictionary<Type, Rectangle> InitialWindowPositions = new Dictionary<Type, Rectangle>();

        public static void On_Skin_Init(Skin self)
        {
            if ((_graphicsTex == null) || (_graphicsTex.GraphicsDevice != GnomanEmpire.Instance.GraphicsDevice) || _graphicsTex.IsDisposed)
            {
                _graphicsTex = CustomTextureManager.GetFromAssemblyResource(Assembly.GetExecutingAssembly(), "Faark.Gnomoria.Mods.Resources.maxButtons.png");
                //Texture2D.FromStream(GnomanEmpire.Instance.GraphicsDevice, Assembly.GetExecutingAssembly().GetManifestResourceStream( "Faark.Gnomoria.Mods.Resources.maxButtons.png"));
            }

            var maxImg = new SkinImage {Resource = _graphicsTex, Name = "Window.MaximizeButton"};
            self.Images.Add(maxImg);

            var mySkinLayer = new SkinLayer
            {
                Name = "Control",
                Alignment = Alignment.MiddleLeft,
                ContentMargins = new Margins(6),
                SizingMargins = new Margins(6),
                Image = maxImg,
                Height = 28,
                Width = 28
            };

            mySkinLayer.States.Disabled.Index = 2;
            mySkinLayer.States.Enabled.Index = 2;
            mySkinLayer.States.Focused.Index = 0;
            mySkinLayer.States.Hovered.Index = 0;
            mySkinLayer.States.Pressed.Index = 2;
            mySkinLayer.Text = new SkinText(self.Controls["Window.CloseButton"].Layers[0].Text);

            var mySkinControl = new SkinControl
            {
                Inherits = "Button",
                ResizerSize = 4,
                DefaultSize = new Size(28, 28),
                Name = "Window.MaximizeButton"
            };
            mySkinControl.Layers.Add(mySkinLayer);
            self.Controls.Add(mySkinControl);
        }

        public static void OnSet_Window_Resizable(Window self, bool newState)
        {
            var oldState = self.Resizable;
            if (newState == oldState) return;

            if (newState)
            {
                _currentSelf = self;
                _currentWinMaxButton = new Button(self.Manager)
                {
                    Skin = new SkinControl(self.Manager.Skin.Controls["Window.MaximizeButton"])
                };

                _currentWinMaxButton.Init();
                _currentWinMaxButton.Detached = true;
                _currentWinMaxButton.CanFocus = false;
                _currentWinMaxButton.Text = null;
                _currentWinMaxButton.Click += (sender, args) =>
                {
                    Rectangle initalPos;
                    if (!InitialWindowPositions.TryGetValue(self.GetType(), out initalPos))
                    {
                        initalPos = InitialWindowPositions[self.GetType()] = new Rectangle(self.Left, self.Top, self.Width, self.Height);
                    }

                    var isMax = true;
                        
                    if (((self.ResizeEdge & Anchors.Top) == Anchors.Top) || ((self.ResizeEdge & Anchors.Bottom) == Anchors.Bottom))
                    {
                        //var h = Math.Min(self.MaximumHeight, self.Manager.TargetHeight);
                        //var half_h = (self.Manager.TargetHeight / 2) - (Math.Min(self.MaximumHeight, self.Manager.TargetHeight) / 2);
                        var top = Math.Max(100, (int)(self.Manager.TargetHeight * 0.1f));
                        var bottom = Math.Max(60, (int)(self.Manager.TargetHeight * 0.09f));
                        var height = Math.Min(self.MaximumHeight, self.Manager.TargetHeight - top - bottom);
                        
                        if ((self.Top == top) && (self.Height == height)) return;

                        isMax = false;
                        self.Top = top;
                        self.Height = height;
                    }

                    if (((self.ResizeEdge & Anchors.Left) == Anchors.Left) || ((self.ResizeEdge & Anchors.Right) == Anchors.Right))
                    {
                        var w = Math.Min((int)(self.Manager.TargetWidth * 0.8f), self.MaximumWidth);
                        var left = (int)(self.Manager.TargetWidth * 0.1f);
                        
                        if ((self.Left == left) && (self.Width == w)) return;

                        self.Left = left;
                        self.Width = w;
                        isMax = false;
                    }

                    if (!isMax) return;
                    
                    self.Top = initalPos.Top;
                    self.Left = initalPos.Left;
                    self.Width = initalPos.Width;
                    self.Height = initalPos.Height;
                };

                var closeSkin = self.Manager.Skin.Controls["Window.MaximizeButton"];
                var skinLayer = closeSkin.Layers["Control"];
                _currentWinMaxButton.Width = skinLayer.Width;
                _currentWinMaxButton.Height = skinLayer.Height - closeSkin.OriginMargins.Vertical;
                _currentWinMaxButton.Left = self.OriginWidth - self.Skin.OriginMargins.Right - skinLayer.Width - closeSkin.OriginMargins.Horizontal + skinLayer.OffsetX - _currentWinMaxButton.Width;
                _currentWinMaxButton.Top = self.Skin.OriginMargins.Top + skinLayer.OffsetY;
                _currentWinMaxButton.Anchor = (Anchors.Top | Anchors.Right);
                self.Add(_currentWinMaxButton, false);
            }
            else
            {
                if (self != _currentSelf) return;
                
                self.Remove(_currentWinMaxButton);
                _currentWinMaxButton = null;
                _currentSelf = null;
            }
        }
    }
#endif
}
