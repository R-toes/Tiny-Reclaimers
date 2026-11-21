using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TrashTD.UI
{
    /// <summary>
    /// Shows the whole game on an old CRT TV. Created automatically before the first scene and kept across
    /// scene loads. Every frame, before rendering:
    ///   - the scene's cameras render into a screen-sized game texture instead of the screen;
    ///   - every Screen Space - Overlay canvas becomes Screen Space - Camera on a dedicated UI camera that
    ///     renders into its own transparent UI texture (sorting orders keep working);
    ///   - Classic and Full TV then lay the UI texture over the game texture in one small pass (CrtComposite.shader,
    ///     which also applies the Stage 4 heat ripple to the game only) into a third texture, so the CRT shader reads
    ///     ONE texture per tap instead of two. Full TV builds a mip chain of that texture for its halation. Off, and
    ///     the brief heat-blur flare, skip the pass and let the CRT shader layer the two textures itself
    ///     (see <see cref="ShouldComposite"/>);
    ///   - the CRT shader (CrtScreen.Television settings) draws the result full-screen, on a screen camera that
    ///     renders last so the frame is never a frame behind.
    /// Separate textures instead of a URP camera stack: each camera is a plain base camera drawing to its own
    /// target, which renders reliably with the 2D Renderer. (Drawing the UI camera straight onto the game texture
    /// does not work: URP copies each camera's own intermediate buffer over the target, so the game vanished.)
    /// Because the glass is curved, pointer positions are bent the same way: UI raycasts through
    /// CrtGraphicRaycaster, and gameplay code converts mouse positions with <see cref="ScreenToPicture"/>
    /// before using them with a camera.
    /// </summary>
    [DefaultExecutionOrder(10000)] // after everything else's LateUpdate, so this frame's new canvases are caught
    public sealed class CrtDisplay : MonoBehaviour, ICrtPointerMapper
    {
        private static CrtDisplay instance;

        // The UI camera sits far from any scene content; canvases are placed in front of it
        private static readonly Vector3 UiCameraPosition = new Vector3(20000f, 0f, 0f);
        private const float CanvasPlaneDistance = 10f;

        private static readonly int UiTexId = Shader.PropertyToID("_UiTex");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int HeatBlurId = Shader.PropertyToID("_HeatBlur");
        private static readonly int HeatWarpId = Shader.PropertyToID("_HeatWarp");
        private static readonly int HeatVentXId = Shader.PropertyToID("_HeatVentX");
        private static readonly int HeatVentYId = Shader.PropertyToID("_HeatVentY");
        private static readonly int CrtTimeId = Shader.PropertyToID("_CrtTime");

        internal const string CompositeShaderResourcePath = "Shaders/CrtComposite";

        /// <summary>
        /// Lay the UI over the game in one small pass before the CRT shader, so it reads one texture per tap instead
        /// of two. Set to false to go back to the CRT shader layering the two textures itself (the original way) if a
        /// device draws wrongly.
        /// </summary>
        private const bool CompositeBeforeCrt = true;

        private static float heatWarpStrength;
        private static float heatBlurAmount;

        private CrtScreen.Settings settings = CrtScreen.Television;

        /// <summary>
        /// Whether this frame goes through the composite pass: not for Off (two fetches per pixel is already cheap,
        /// the extra pass would cost more than it saves), and not while the heat-blur flare runs (that blurs the
        /// game after the CRT's scanlines and glow, which the composite cannot reproduce, so the flare uses the
        /// original two-texture path).
        /// </summary>
        internal static bool ShouldComposite(bool compositeReady, bool offStyle, float heatBlur)
        {
            return CompositeBeforeCrt && compositeReady && !offStyle && heatBlur <= 0.001f;
        }

        /// <summary>
        /// The whole game through the chosen CRT style: Full TV (Television), Classic (the first, crisper
        /// version) or Off (passthrough). Full TV keeps its 4K look on every screen (see
        /// CrtScreen.VirtualPictureHeight). The player's brightness applies on top in every style.
        /// </summary>
        private static CrtScreen.Settings GetActiveSettings()
        {
            CrtScreen.Settings baseSettings = GameGraphicsSettings.CrtStyle switch
            {
                CrtStyle.Classic => CrtScreen.ClassicTelevision,
                CrtStyle.Off => CrtScreen.Clean,
                _ => CrtScreen.Television
            };
            baseSettings.opaque = 1f; // full-screen presenter: never show the backbuffer through the picture
            return baseSettings.WithBrightnessScale(GameGraphicsSettings.Brightness);
        }

        private void ApplyGraphicsSettings()
        {
            settings = GetActiveSettings();
            CrtScreen.ApplySettings(material, settings);
            warmUpPending = true;

            // The full-screen presenter never needs the framebuffer behind it, so it can overwrite instead of blend
            if (material != null)
            {
                bool opaque = settings.opaque >= 0.5f;
                material.SetFloat(SrcBlendId, (float)(opaque ? BlendMode.One : BlendMode.SrcAlpha));
                material.SetFloat(DstBlendId, (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
            }
        }
        private Material material;
        private RenderTexture picture;   // the game world (scene cameras)
        private RenderTexture uiPicture; // the converted canvases, transparent where there is no UI
        private RenderTexture composite; // game + UI in one texture, what the CRT shader reads (Classic, Full TV)
        private Material compositeMaterial;
        private Camera screenCamera;     // renders last to the real screen; the CRT canvas is drawn on it
        private Camera uiCamera;         // renders the converted canvases into uiPicture
        private Canvas presentCanvas;
        private RawImage presentImage;
        private Camera[] cameraBuffer = new Camera[8];
        private bool compositeThisFrame; // the composite pass runs after the UI camera this frame
        private bool warmUpPending;      // draw the shader variants this style can switch to once (WarmUpVariants)
        private Rect shownArea = new Rect(-1f, -1f, 0f, 0f);
        private Vector2Int shownScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            heatWarpStrength = 0f;
            heatBlurAmount = 0f;
            if (instance != null) return;

            var go = new GameObject("CrtDisplay");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CrtDisplay>();
        }

        /// <summary>
        /// Test hook: the share (0-1) of sampled pixels in the picture the CRT shows that are not black, or -1 when
        /// there is no picture. Reads the texture back from the GPU, so never call it per frame.
        /// </summary>
        internal static float SampleNonBlackFraction()
        {
            if (instance == null || instance.picture == null || !instance.picture.IsCreated()) return -1f;

            RenderTexture texture = instance.presentImage.texture as RenderTexture;
            if (texture == null || !texture.IsCreated()) texture = instance.picture;
            RenderTexture previous = RenderTexture.active;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            RenderTexture.active = texture;
            readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;

            Color32[] pixels = readable.GetPixels32();
            Destroy(readable);
            int lit = 0, sampled = 0;
            for (int i = 0; i < pixels.Length; i += 7)
            {
                sampled++;
                if (pixels[i].r > 12 || pixels[i].g > 12 || pixels[i].b > 12) lit++;
            }
            return sampled == 0 ? 0f : lit / (float)sampled;
        }

        /// <summary>True while the game is being shown through the CRT.</summary>
        public static bool IsActive => instance != null && instance.isActiveAndEnabled && instance.picture != null;

        /// <summary>Sets the brief Stage 4 heat distortion blur without softening the composited UI.</summary>
        public static void SetHeatBlur(float amount)
        {
            heatBlurAmount = Mathf.Clamp01(amount);
            if (instance == null || instance.material == null) return;
            instance.material.SetFloat(HeatBlurId, heatBlurAmount);
        }

        /// <summary>
        /// Stage 4 heat shimmer: ripples the game picture (never the UI) like air over hot metal. Strength is in
        /// picture UV units (about 0.002 is a gentle ripple), the band is how far up the screen the floor haze
        /// reaches (0-1), and up to four vents add rising columns of shimmer at their viewport positions
        /// (x below 0 = unused). Strength 0 switches it off.
        /// </summary>
        public static void SetHeatWarp(float strength, float bandHeight, Vector4 ventX, Vector4 ventY)
        {
            heatWarpStrength = Mathf.Max(0f, strength);
            if (instance == null || instance.material == null) return;
            instance.material.SetVector(HeatWarpId, new Vector4(heatWarpStrength, Mathf.Clamp01(bandHeight), 0f, 0f));
            instance.material.SetVector(HeatVentXId, ventX);
            instance.material.SetVector(HeatVentYId, ventY);

            Material composition = instance.compositeMaterial;
            if (composition == null) return;
            composition.SetVector(HeatWarpId, new Vector4(heatWarpStrength, Mathf.Clamp01(bandHeight), 0f, 0f));
            composition.SetVector(HeatVentXId, ventX);
            composition.SetVector(HeatVentYId, ventY);
        }

        /// <summary>
        /// Converts a real screen position (mouse, PointerEventData) to the matching position in the game
        /// picture, which is what cameras (ScreenToWorldPoint) and canvases expect. Unchanged when inactive.
        /// </summary>
        public static Vector2 ScreenToPicture(Vector2 screenPosition)
        {
            if (!IsActive) return screenPosition;
            Vector2 size = new Vector2(instance.picture.width, instance.picture.height);
            return Vector2.Scale(CrtScreen.Distort(ScreenToUv(screenPosition), instance.settings.curvature), size);
        }

        /// <summary>
        /// The part of the screen the game is drawn in, in pixels: the whole screen, except on phones where it stays
        /// inside Screen.safeArea so notches, rounded corners and the gesture bar never hide the HUD.
        /// </summary>
        public static Rect PictureRect
        {
            get
            {
                Rect full = new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
                if (!Application.isMobilePlatform) return full;
                Rect safe = Screen.safeArea;
                return safe.width >= 16f && safe.height >= 16f ? safe : full;
            }
        }

        /// <summary>Size in pixels of the game picture (what cameras render into). The screen size when the CRT is inactive.</summary>
        public static Vector2 PictureSize
        {
            get
            {
                if (IsActive) return new Vector2(instance.picture.width, instance.picture.height);
                return new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            }
        }

        /// <summary>A real screen position as 0-1 coordinates inside the picture's rectangle.</summary>
        private static Vector2 ScreenToUv(Vector2 screenPosition)
        {
            Rect rect = PictureRect;
            return new Vector2((screenPosition.x - rect.x) / rect.width, (screenPosition.y - rect.y) / rect.height);
        }

        /// <summary>The CRT's own full-screen canvas; skip it when looking for a canvas to build UI on.</summary>
        public static bool IsDisplayCanvas(Canvas canvas) => instance != null && canvas != null && canvas == instance.presentCanvas;

        /// <summary>First root canvas that game UI can be parented to (never the CRT's own canvas).</summary>
        public static Canvas FindGameCanvas()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>())
            {
                if (canvas.isRootCanvas && !IsDisplayCanvas(canvas) && canvas.renderMode != RenderMode.WorldSpace)
                    return canvas;
            }
            return null;
        }

        public bool IsMappingPointer => isActiveAndEnabled && picture != null;

        public bool TryMapScreenPoint(Vector2 screenPosition, out Vector2 picturePosition)
        {
            picturePosition = default;
            if (!IsMappingPointer) return false;

            Vector2 pictureUv = CrtScreen.Distort(ScreenToUv(screenPosition), settings.curvature);
            if (!CrtScreen.IsOnScreen(pictureUv, settings.cornerRadius)) return false;

            picturePosition = new Vector2(pictureUv.x * picture.width, pictureUv.y * picture.height);
            return true;
        }

        private void Awake()
        {
            material = CrtScreen.CreateMaterial("CRT (Display)", this);
            ApplyGraphicsSettings();
            GameGraphicsSettings.Changed += ApplyGraphicsSettings;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;

            // Without this shader the CRT shader layers the two textures itself, as it always did
            Shader compositeShader = Resources.Load<Shader>(CompositeShaderResourcePath);
            if (compositeShader != null) compositeMaterial = new Material(compositeShader) { name = "CRT (Composite)" };

            // Renders after every other camera, so the CRT shows this frame's game and UI textures
            screenCamera = CreateCamera("CrtScreenCamera", new Vector3(-30000f, 0f, 0f));
            screenCamera.cullingMask = 0;
            screenCamera.clearFlags = CameraClearFlags.SolidColor;
            screenCamera.backgroundColor = Color.black;
            screenCamera.depth = 1000f;

            uiCamera = CreateCamera("CrtUiCamera", UiCameraPosition);
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            uiCamera.cullingMask = ~0;
            uiCamera.depth = 900f;

            var canvasObject = new GameObject("CrtDisplayCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            presentCanvas = canvasObject.GetComponent<Canvas>();
            presentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            presentCanvas.sortingOrder = short.MaxValue;

            var imageObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(canvasObject.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            presentImage = imageObject.GetComponent<RawImage>();
            presentImage.raycastTarget = false;
            presentImage.material = material;

            if (material == null)
            {
                // Without the shader there is nothing to add; leave the game rendering normally
                enabled = false;
                presentCanvas.enabled = false;
                screenCamera.enabled = false;
                uiCamera.enabled = false;
            }
        }

        private Camera CreateCamera(string cameraName, Vector3 position)
        {
            var go = new GameObject(cameraName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.allowMSAA = false;
            cam.allowHDR = false; // 8-bit targets and no post-processing: a float intermediate buffer would only cost a copy
            return cam;
        }

        private void OnDestroy()
        {
            ReleaseTexture(ref picture);
            ReleaseTexture(ref uiPicture);
            ReleaseTexture(ref composite);
            if (material != null) Destroy(material);
            if (compositeMaterial != null) Destroy(compositeMaterial);
            GameGraphicsSettings.Changed -= ApplyGraphicsSettings;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Draws every shader variant the current style can switch to onto a tiny texture, once, so the first time one
        /// is needed (Stage 4's heat pulse is the one that happens mid-game) it is already compiled: the editor shows a
        /// plain placeholder colour for a moment while it compiles a variant in the background, and a phone's graphics
        /// driver stalls compiling it the first time it is drawn.
        /// </summary>
        private void WarmUpVariants()
        {
            warmUpPending = false;
            if (material == null) return;

            RenderTexture previous = RenderTexture.active;
            RenderTexture tiny = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32);
            var scratch = new Material(material);
            Material compositeScratch = compositeMaterial != null ? new Material(compositeMaterial) : null;
            try
            {
                bool off = settings.passthrough >= 0.5f;
                foreach (bool separateUi in new[] { false, true })
                {
                    if (!separateUi && off) continue; // Off never composites
                    CrtScreen.SetKeyword(scratch, CrtScreen.HeatKeyword, separateUi);
                    CrtScreen.SetKeyword(scratch, CrtScreen.MipGlowKeyword, !separateUi && CrtScreen.IsFullLook(settings));
                    scratch.SetTexture(UiTexId, Texture2D.blackTexture);
                    Graphics.Blit(Texture2D.blackTexture, tiny, scratch);
                }

                if (compositeScratch != null && !off)
                {
                    compositeScratch.SetTexture(UiTexId, Texture2D.blackTexture);
                    foreach (bool heat in new[] { false, true })
                    {
                        CrtScreen.SetKeyword(compositeScratch, CrtScreen.HeatKeyword, heat);
                        Graphics.Blit(Texture2D.blackTexture, tiny, compositeScratch);
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tiny);
                Destroy(scratch);
                if (compositeScratch != null) Destroy(compositeScratch);
            }
        }

        private void LateUpdate()
        {
            SyncTextures();
            if (warmUpPending) WarmUpVariants();
            bool useComposite = ShouldComposite(composite != null, settings.passthrough >= 0.5f, heatBlurAmount);
            compositeThisFrame = useComposite;
            bool hasSceneCamera = RedirectCameras();
            ConvertCanvases();

            // No scene camera (e.g. mid scene change): show black rather than a stale frame
            if (!hasSceneCamera)
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = picture;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = previous;
            }

            presentImage.texture = useComposite ? composite : picture;
            FitPictureToSafeArea();
            if (material != null)
            {
                // Composited: one texture, no UI layer. Otherwise the CRT shader layers the UI texture itself.
                if (!useComposite) material.SetTexture(UiTexId, uiPicture);
                CrtScreen.SetKeyword(material, CrtScreen.HeatKeyword, !useComposite);
                CrtScreen.SetKeyword(material, CrtScreen.MipGlowKeyword, useComposite && composite.useMipMap);
            }
            if (useComposite) compositeMaterial.SetFloat(CrtTimeId, Time.unscaledTime);
            CrtScreen.UpdateFrame(material, settings, picture.width, picture.height, CrtScreen.VirtualPictureHeight);
        }

        /// <summary>
        /// The UI camera is the last to draw, so its end is the moment to lay the UI over the game into the composite
        /// texture (warping only the game while the heat effect runs) and, for Full TV, build that texture's mips for
        /// the shader's halation. Both happen before the screen camera draws the CRT.
        /// </summary>
        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != uiCamera || !compositeThisFrame) return;
            if (composite == null || picture == null || uiPicture == null || compositeMaterial == null) return;
            if (!composite.IsCreated() || !picture.IsCreated() || !uiPicture.IsCreated()) return;

            CrtScreen.SetKeyword(compositeMaterial, CrtScreen.HeatKeyword, heatWarpStrength > 0.00001f);
            compositeMaterial.SetTexture(UiTexId, uiPicture);

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(picture, composite, compositeMaterial);
            RenderTexture.active = previous;

            if (composite.useMipMap) composite.GenerateMips();
        }

        /// <summary>The picture fills the safe area only (the whole screen on desktop); the rest stays black.</summary>
        private void FitPictureToSafeArea()
        {
            Rect area = PictureRect;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (area == shownArea && screen == shownScreen) return; // nothing moved: leave the layout alone
            shownArea = area;
            shownScreen = screen;

            RectTransform imageRect = presentImage.rectTransform;
            imageRect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            imageRect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
        }

        /// <summary>Fills <see cref="cameraBuffer"/> with the enabled cameras (no array is allocated) and returns how many.</summary>
        private int GetCameras()
        {
            int count = Camera.allCamerasCount;
            if (cameraBuffer.Length < count) cameraBuffer = new Camera[count * 2];
            return Camera.GetAllCameras(cameraBuffer);
        }

        private void SyncTextures()
        {
            // On a phone the player can draw the picture smaller than the screen (Settings > Display) to save battery
            Rect area = PictureRect;
            float scale = MobileSettings.CurrentPictureScale;
            int width = Mathf.Max(16, Mathf.RoundToInt(area.width * scale));
            int height = Mathf.Max(16, Mathf.RoundToInt(area.height * scale));

            if (picture == null || uiPicture == null || picture.width != width || picture.height != height)
            {
                int cameraCount = GetCameras();
                for (int i = 0; i < cameraCount; i++)
                {
                    Camera cam = cameraBuffer[i];
                    if (picture != null && cam.targetTexture == picture) cam.targetTexture = null;
                    if (uiPicture != null && cam.targetTexture == uiPicture) cam.targetTexture = null;
                }
                ReleaseTexture(ref picture);
                ReleaseTexture(ref uiPicture);
                ReleaseTexture(ref composite); // made again just below

                picture = CreateTexture("CRT_Game", width, height, false);
                uiPicture = CreateTexture("CRT_UI", width, height, false);
                uiCamera.targetTexture = uiPicture;
            }

            // The composite texture exists for Classic and Full TV only, and only Full TV (its halation) has mips
            bool wantComposite = compositeMaterial != null && settings.passthrough < 0.5f;
            bool wantMips = wantComposite && CrtScreen.IsFullLook(settings);
            bool compositeIsRight = composite == null ? !wantComposite : wantComposite && composite.useMipMap == wantMips;
            if (!compositeIsRight)
            {
                ReleaseTexture(ref composite);
                if (wantComposite) composite = CreateTexture("CRT_Composite", width, height, wantMips);
            }
        }

        private static RenderTexture CreateTexture(string textureName, int width, int height, bool mipMaps)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = mipMaps,
                autoGenerateMips = false // built once per frame, after the composite pass (HandleEndCameraRendering)
            };
            texture.Create();
            return texture;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        /// <summary>Points every scene camera that draws to the screen at the game texture.</summary>
        private bool RedirectCameras()
        {
            bool found = false;
            int cameraCount = GetCameras();
            for (int i = 0; i < cameraCount; i++)
            {
                Camera cam = cameraBuffer[i];
                if (cam == screenCamera || cam == uiCamera) continue;
                if (cam.targetTexture != null && cam.targetTexture != picture) continue; // e.g. a CrtScreen monitor

                if (cam.targetTexture != picture) cam.targetTexture = picture;
                if (cam.allowHDR) cam.allowHDR = false; // see CreateCamera
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Moves every overlay canvas into the picture (keeping its sorting), then bends the raycasts of every
        /// canvas drawn there, nested ones with their own raycaster included (e.g. the environment briefing).
        /// </summary>
        private void ConvertCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas == presentCanvas || !canvas.isRootCanvas) continue;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = CanvasPlaneDistance;
            }

            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || canvas == presentCanvas) continue;
                Canvas root = canvas.rootCanvas;
                if (root.renderMode != RenderMode.ScreenSpaceCamera || root.worldCamera != uiCamera) continue;

                GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null || raycaster is CrtGraphicRaycaster) continue;

                bool ignoreReversed = raycaster.ignoreReversedGraphics;
                GraphicRaycaster.BlockingObjects blocking = raycaster.blockingObjects;
                DestroyImmediate(raycaster);

                var bent = canvas.gameObject.AddComponent<CrtGraphicRaycaster>();
                bent.ignoreReversedGraphics = ignoreReversed;
                bent.blockingObjects = blocking;
                bent.Owner = this;
            }
        }
    }
}
