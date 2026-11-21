using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// The phone-performance work on the CRT: shader keywords per style, the single-layer UI rule, the halation mip
    /// levels, the menu render rate, and that every shader variant the game can ask for compiles.
    /// </summary>
    public class CrtPerformanceTests
    {
        private Material material;

        [SetUp]
        public void SetUp()
        {
            material = CrtScreen.CreateMaterial("CrtPerformanceTests", null);
            Assert.IsNotNull(material, "Resources/Shaders/CrtScreen must load");
        }

        [TearDown]
        public void TearDown()
        {
            if (material != null) Object.DestroyImmediate(material);
        }

        // ----- Style keywords -----

        [Test]
        public void FullTv_UsesNeitherStyleKeyword()
        {
            CrtScreen.ApplySettings(material, CrtScreen.Television);
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.ClassicKeyword));
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.CleanKeyword));
            Assert.IsTrue(CrtScreen.IsFullLook(CrtScreen.Television));
        }

        [Test]
        public void Classic_UsesOnlyTheClassicKeyword()
        {
            CrtScreen.ApplySettings(material, CrtScreen.ClassicTelevision);
            Assert.IsTrue(material.IsKeywordEnabled(CrtScreen.ClassicKeyword));
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.CleanKeyword));
            Assert.IsFalse(CrtScreen.IsFullLook(CrtScreen.ClassicTelevision));
        }

        [Test]
        public void Off_UsesOnlyTheCleanKeyword()
        {
            CrtScreen.ApplySettings(material, CrtScreen.Clean);
            Assert.IsTrue(material.IsKeywordEnabled(CrtScreen.CleanKeyword));
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.ClassicKeyword));
            Assert.IsFalse(CrtScreen.IsFullLook(CrtScreen.Clean));
        }

        [Test]
        public void SwitchingStyle_ClearsTheKeywordOfTheOldOne()
        {
            CrtScreen.ApplySettings(material, CrtScreen.ClassicTelevision);
            CrtScreen.ApplySettings(material, CrtScreen.Television);
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.ClassicKeyword));

            CrtScreen.ApplySettings(material, CrtScreen.Clean);
            CrtScreen.ApplySettings(material, CrtScreen.ClassicTelevision);
            Assert.IsFalse(material.IsKeywordEnabled(CrtScreen.CleanKeyword));
            Assert.IsTrue(material.IsKeywordEnabled(CrtScreen.ClassicKeyword));
        }

        // ----- Composite pass -----

        [Test]
        public void CompositePass_RunsForClassicAndFullTv_ButNotForOffOrTheHeatBlurFlare()
        {
            Assert.IsTrue(CrtDisplay.ShouldComposite(true, false, 0f), "Classic and Full TV composite");
            Assert.IsFalse(CrtDisplay.ShouldComposite(true, true, 0f), "Off is cheap enough without it");
            Assert.IsFalse(CrtDisplay.ShouldComposite(true, false, 0.4f), "the heat blur flare uses the two-texture path");
            Assert.IsFalse(CrtDisplay.ShouldComposite(false, false, 0f), "no composite texture, nothing to read");
            Assert.IsTrue(CrtDisplay.ShouldComposite(true, false, 0.0005f), "a blur this faint is invisible");
        }

        // ----- Halation mips -----

        [Test]
        public void GlowLods_AtFourK_MatchTheOriginalTapRadii()
        {
            Vector2 lods = CrtScreen.CalculateGlowLods(1f);
            Assert.AreEqual(2f, lods.x);
            Assert.AreEqual(4f, lods.y);
        }

        [Test]
        public void GlowLods_ShrinkAsTheVirtualScaleGrows_AndStayInRange()
        {
            Vector2 previous = CrtScreen.CalculateGlowLods(1f);
            foreach (float scale in new[] { 1.5f, 2f, 3f, 4f, 8f })
            {
                Vector2 lods = CrtScreen.CalculateGlowLods(scale);
                Assert.LessOrEqual(lods.x, previous.x);
                Assert.LessOrEqual(lods.y, previous.y);
                Assert.GreaterOrEqual(lods.x, 1f, "level 0 would add an unblurred copy");
                Assert.Greater(lods.y, lods.x - 0.01f);
                Assert.LessOrEqual(lods.y, 5f);
                previous = lods;
            }
        }

        // ----- Menu render rate -----

        [Test]
        public void MenuRenderRate_OnlyThrottlesTheIdleMainMenu()
        {
            Assert.AreEqual(1, MobileRenderRate.CalculateInterval(false, 60f, 60), "gameplay is never throttled");
            Assert.AreEqual(1, MobileRenderRate.CalculateInterval(true, 0.2f, 60), "just touched");
            Assert.AreEqual(2, MobileRenderRate.CalculateInterval(true, MobileRenderRate.IdleSeconds, 60));
            Assert.AreEqual(3, MobileRenderRate.CalculateInterval(true, 10f, 90));
            Assert.AreEqual(1, MobileRenderRate.CalculateInterval(true, 10f, 30), "already 30 fps");
        }

        // ----- Shader variants -----

        [Test]
        public void EveryShaderVariantTheGameUses_Compiles()
        {
            Shader shader = material.shader;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                string[] styles = { null, CrtScreen.ClassicKeyword, CrtScreen.CleanKeyword };
                foreach (string style in styles)
                {
                    foreach (bool heat in new[] { false, true })
                    {
                        foreach (bool mipGlow in new[] { false, true })
                        {
                            CrtScreen.SetKeyword(material, CrtScreen.ClassicKeyword, style == CrtScreen.ClassicKeyword);
                            CrtScreen.SetKeyword(material, CrtScreen.CleanKeyword, style == CrtScreen.CleanKeyword);
                            CrtScreen.SetKeyword(material, CrtScreen.HeatKeyword, heat);
                            CrtScreen.SetKeyword(material, CrtScreen.MipGlowKeyword, mipGlow);

                            // Compiles the variant now; errors are reported against the shader
                            material.SetPass(0);
                        }
                    }
                }

                Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader),
                    "CrtScreen.shader reported compile messages: " + DescribeMessages(shader));

                // The composite pass: with and without the heat ripple
                Shader compositeShader = Resources.Load<Shader>(CrtDisplay.CompositeShaderResourcePath);
                Assert.IsNotNull(compositeShader, "Resources/Shaders/CrtComposite must load");
                var compositeMaterial = new Material(compositeShader);
                try
                {
                    foreach (bool heat in new[] { false, true })
                    {
                        CrtScreen.SetKeyword(compositeMaterial, CrtScreen.HeatKeyword, heat);
                        compositeMaterial.SetPass(0);
                    }
                    Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(compositeShader),
                        "CrtComposite.shader reported compile messages: " + DescribeMessages(compositeShader));
                }
                finally
                {
                    Object.DestroyImmediate(compositeMaterial);
                }
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsync;
            }
        }

        private static string DescribeMessages(Shader shader)
        {
            var text = new System.Text.StringBuilder();
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
                text.AppendLine(message.severity + " line " + message.line + ": " + message.message);
            return text.ToString();
        }
    }
}
