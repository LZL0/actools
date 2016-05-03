﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectWrite;
using SpriteTextRenderer;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = SpriteTextRenderer.TextAlignment;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.Kn5SpecificForward {
    public class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public bool AutoRotate { get; set; } = true;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool VisibleUi { get; set; } = true;

        private readonly Kn5 _kn5;
        private readonly CarHelper _carHelper;

        public ForwardKn5ObjectRenderer(string mainKn5Filename) {
            _kn5 = Kn5.FromFile(mainKn5Filename);
            _carHelper = new CarHelper(_kn5);
        }

        public void SelectPreviousSkin() {
            _carHelper.SelectPreviousSkin(DeviceContextHolder);
        }

        public void SelectNextSkin() {
            _carHelper.SelectNextSkin(DeviceContextHolder);
        }

        public void SelectSkin(string skinId) {
            _carHelper.SelectSkin(skinId, DeviceContextHolder);
        }

        [CanBeNull]
        private List<CarLight> _carLights;

        protected override void InitializeInner() {
            base.InitializeInner();

            Kn5MaterialsProvider.Initialize(new MaterialsProviderSimple());
            _carHelper.SetKn5(DeviceContextHolder);
            _carHelper.SkinTextureUpdated += (sender, args) => IsDirty = true;

            var node = Kn5Converter.Convert(_kn5.RootNode);
            Scene.Add(node);

            var asList = node as Kn5RenderableList;
            if (asList != null) {
                Scene.AddRange(_carHelper.LoadAmbientShadows(asList));
                _carHelper.AdjustPosition(asList);
                _carHelper.LoadMirrors(asList);

                _carLights = _carHelper.LoadLights(asList).ToList();
            }

            Scene.UpdateBoundingBox();
            TrianglesCount = node.TrianglesCount;

            Camera = new CameraOrbit(32) {
                Alpha = 0.9f,
                Beta = 0.1f,
                NearZ = 0.2f,
                FarZ = 500f,
                Radius = node.BoundingBox?.GetSize().Length() ?? 4.8f,
                Target = (node.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.05f, 0f)
            };

            _resetCamera = (CameraOrbit)Camera.Clone();
        }

        private float _resetState;
        private CameraOrbit _resetCamera;

        public void ResetCamera() {
            AutoRotate = true;
            _resetState = 1f;
        }

        private TextBlockRenderer _textBlock;

        protected override void ResizeInner() {
            base.ResizeInner();

            if (_textBlock != null) return;
            _textBlock = new TextBlockRenderer(Sprite, "Consolas", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
        }

        public bool CarLightsEnabled {
            get { return _carLights?.FirstOrDefault()?.IsEnabled == true; }
            set {
                if (_carLights == null) return;
                foreach (var light in _carLights) {
                    light.IsEnabled = value;
                }
            }
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(Camera.Position);
        }

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            _textBlock.DrawString($@"
FPS:            {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
FXAA:           {(!UseFxaa ? "No" : "Yes")}
Bloom:          {(!UseBloom ? "No" : "Yes")}
Triangles:      {TrianglesCount:D}".Trim(),
                    new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                    CoordinateType.Absolute);

            if (_carHelper.Skins != null && _carHelper.CurrentSkin != null) {
                _textBlock.DrawString($"{_carHelper.CurrentSkin} ({_carHelper.Skins.IndexOf(_carHelper.CurrentSkin) + 1}/{_carHelper.Skins.Count})", new RectangleF(0f, 0f, Width, Height - 20),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }
        }

        private float _elapsedCamera;

        protected override void OnTick(float dt) {
            const float threshold = 0.001f;
            if (_resetState > threshold) {
                if (!AutoRotate) {
                    _resetState = 0f;
                    return;
                }

                AutoAdjustTarget = true;

                _resetState += (-0f - _resetState) / 10f;
                if (_resetState <= threshold) {
                    AutoRotate = false;
                }

                var cam = CameraOrbit;
                if (cam != null) {
                    cam.Alpha += (_resetCamera.Alpha - cam.Alpha) / 10f;
                    cam.Beta += (_resetCamera.Beta - cam.Beta) / 10f;
                    cam.Radius += (_resetCamera.Radius - cam.Radius) / 10f;
                    cam.FovY += (_resetCamera.FovY - cam.FovY) / 10f;
                }

                _elapsedCamera = 0f;

                IsDirty = true;
            } else if (AutoRotate && CameraOrbit != null) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta += (MathF.Sin(_elapsedCamera * 0.39f) * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;

                IsDirty = true;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = _resetCamera.Target + new Vector3(-0.2f * CameraOrbit.Position.X, -0.1f * CameraOrbit.Position.Y, 0f);
                CameraOrbit.Target += (t - CameraOrbit.Target) / 2f;
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _textBlock);
            _carHelper.Dispose();
            base.Dispose();
        }
    }
}