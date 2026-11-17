using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Combat;

namespace TrashTD.Tests
{
    /// <summary>
    /// Projectiles and AoE blasts are pooled: a finished one is deactivated and reused by the next shot, with
    /// everything from its last use reset. EditMode does not run Update, so the tests release them directly.
    /// </summary>
    public class CombatVisualPoolTests
    {
        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void ResetPool<T>() =>
            typeof(T).GetMethod("ResetPool", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private static T FindSingleActive<T>() where T : Object
        {
            T[] found = Object.FindObjectsByType<T>();
            Assert.AreEqual(1, found.Length, $"Expected one active {typeof(T).Name}.");
            return found[0];
        }

        [SetUp]
        [TearDown]
        public void Cleanup()
        {
            foreach (var projectile in Object.FindObjectsByType<CombatProjectileVisual>(FindObjectsInactive.Include))
                Object.DestroyImmediate(projectile.gameObject);
            foreach (var blast in Object.FindObjectsByType<AoeBlastVisual>(FindObjectsInactive.Include))
                Object.DestroyImmediate(blast.gameObject);
            foreach (var particles in Object.FindObjectsByType<PixelParticles>(FindObjectsInactive.Include))
                Object.DestroyImmediate(particles.gameObject);
            ResetPool<CombatProjectileVisual>();
            ResetPool<AoeBlastVisual>();
        }

        [Test]
        public void Projectile_IsReusedAfterLanding_WithItsHaloAndNameReset()
        {
            // An orb has a halo; a plain bolt does not.
            CombatProjectileVisual.Fire(Vector3.zero, new Vector3(3f, 0f, 0f), Color.magenta, 5f, 0.2f, 0.1f,
                style: ProjectileStyle.ArtsOrb);
            CombatProjectileVisual first = FindSingleActive<CombatProjectileVisual>();
            Transform halo = first.transform.Find("Halo");
            Assert.IsNotNull(halo);
            Assert.IsTrue(halo.gameObject.activeSelf);

            Invoke(first, "Release");
            Assert.IsFalse(first.gameObject.activeSelf, "A landed projectile is deactivated, not destroyed.");
            Assert.AreEqual(1, CombatProjectileVisual.PooledCount);

            CombatProjectileVisual.Fire(new Vector3(1f, 1f, 0f), new Vector3(4f, 1f, 0f), Color.white, 5f, 0.2f, 0.1f,
                style: ProjectileStyle.Bolt);
            CombatProjectileVisual second = FindSingleActive<CombatProjectileVisual>();
            Assert.AreSame(first, second, "The next shot reuses the pooled projectile.");
            Assert.AreEqual(0, CombatProjectileVisual.PooledCount);
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.IsFalse(halo.gameObject.activeSelf, "A bolt hides the halo left from the orb.");
            Assert.AreEqual("CombatProjectileVisual", second.gameObject.name);
            Assert.AreEqual(Vector3.one, second.transform.localScale);
            Assert.AreEqual(1f, second.transform.position.x, 0.05f, "It starts from the new shot's origin.");
        }

        [Test]
        public void Projectile_DestroyedWhilePooled_IsSkipped()
        {
            CombatProjectileVisual.Fire(Vector3.zero, Vector3.right, Color.white, 5f, 0.2f, 0.1f);
            CombatProjectileVisual first = FindSingleActive<CombatProjectileVisual>();
            Invoke(first, "Release");
            Object.DestroyImmediate(first.gameObject); // as a scene change would

            CombatProjectileVisual.Fire(Vector3.zero, Vector3.right, Color.white, 5f, 0.2f, 0.1f);
            CombatProjectileVisual second = FindSingleActive<CombatProjectileVisual>();
            Assert.IsTrue(second != null && second.gameObject.activeSelf, "A fresh projectile replaces the destroyed one.");
        }

        [Test]
        public void Blast_IsReusedAfterFinishing_WithOnlyItsOwnPiecesShown()
        {
            // A 3-cell blast first: three pieces and no shockwave ring.
            var cells = new[] { Vector3.zero, Vector3.right, Vector3.left };
            AoeBlastVisual.PlayCells(cells, 1f, Color.red);
            AoeBlastVisual blast = FindSingleActive<AoeBlastVisual>();
            Assert.AreEqual(3, CountActiveChildren(blast.transform, "BlastPiece"));
            Assert.IsNull(blast.transform.Find("Shockwave"));

            Invoke(blast, "Release");
            Assert.IsFalse(blast.gameObject.activeSelf);
            Assert.AreEqual(1, AoeBlastVisual.PooledCount);

            // Then a circle reuses it: one piece and the ring; the two extra cell pieces stay hidden.
            AoeBlastVisual.PlayCircle(new Vector3(2f, 2f, 0f), 1.5f, Color.yellow);
            AoeBlastVisual reused = FindSingleActive<AoeBlastVisual>();
            Assert.AreSame(blast, reused);
            Assert.AreEqual(1, CountActiveChildren(reused.transform, "BlastPiece"));
            Transform piece = FindActiveChild(reused.transform, "BlastPiece");
            Assert.AreEqual(Vector3.zero, piece.localPosition);
            Assert.AreEqual(Vector3.one, piece.localScale, "The cell blast's scale is reset.");
            Transform ring = reused.transform.Find("Shockwave");
            Assert.IsNotNull(ring);
            Assert.IsTrue(ring.gameObject.activeSelf);

            // And a cell blast after the circle hides the ring again.
            Invoke(reused, "Release");
            AoeBlastVisual.PlayCells(cells, 1f, Color.red);
            Assert.AreSame(blast, FindSingleActive<AoeBlastVisual>());
            Assert.IsFalse(ring.gameObject.activeSelf);
            Assert.AreEqual(3, CountActiveChildren(blast.transform, "BlastPiece"));
        }

        private static int CountActiveChildren(Transform parent, string name)
        {
            int count = 0;
            foreach (Transform child in parent)
                if (child.name == name && child.gameObject.activeSelf) count++;
            return count;
        }

        private static Transform FindActiveChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
                if (child.name == name && child.gameObject.activeSelf) return child;
            return null;
        }
    }
}
