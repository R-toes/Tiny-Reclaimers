using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Editor;

namespace TrashTD.Tests
{
    /// <summary>The maths behind the Range Pattern window (RangePatternLogic), and the range patterns of the real operators.</summary>
    public class RangePatternLogicTests
    {
        private static readonly OperatorFacing[] AllFacings = { OperatorFacing.Right, OperatorFacing.Up, OperatorFacing.Left, OperatorFacing.Down };

        // ---------- facing ----------

        [Test]
        public void Rotate_IsTheRotationTheGameUses()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            var gridObject = new GameObject("RangePatternGrid");
            try
            {
                StageMakerLogic.CreateBlank(stage, 11, 11, TileType.LowGround);
                var grid = gridObject.AddComponent<GridManager>();
                grid.InitializeFromStageData(stage);
                var center = new Vector2Int(5, 5);

                foreach (OperatorFacing facing in AllFacings)
                {
                    for (int x = -3; x <= 3; x++)
                    {
                        for (int y = -3; y <= 3; y++)
                        {
                            var offset = new Vector2Int(x, y);
                            GridCell[] cells = grid.GetCellsInRange(center, new[] { offset }, facing);
                            Assert.AreEqual(1, cells.Length);
                            Assert.AreEqual(center + RangePatternLogic.Rotate(offset, facing), new Vector2Int(cells[0].X, cells[0].Y), "offset " + offset + " facing " + facing);
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(gridObject);
                Object.DestroyImmediate(stage);
            }
        }

        [Test]
        public void Unrotate_UndoesRotate()
        {
            foreach (OperatorFacing facing in AllFacings)
                for (int x = -4; x <= 4; x++)
                    for (int y = -4; y <= 4; y++)
                    {
                        var offset = new Vector2Int(x, y);
                        Assert.AreEqual(offset, RangePatternLogic.Unrotate(RangePatternLogic.Rotate(offset, facing), facing));
                    }
        }

        [Test]
        public void FacingRight_PutsTheFrontTileInFront()
        {
            Assert.AreEqual(Vector2Int.right, RangePatternLogic.Rotate(Vector2Int.right, OperatorFacing.Right));
            Assert.AreEqual(Vector2Int.up, RangePatternLogic.Rotate(Vector2Int.right, OperatorFacing.Up));
            Assert.AreEqual(Vector2Int.left, RangePatternLogic.Rotate(Vector2Int.right, OperatorFacing.Left));
            Assert.AreEqual(Vector2Int.down, RangePatternLogic.Rotate(Vector2Int.right, OperatorFacing.Down));
        }

        // ---------- editing ----------

        [Test]
        public void AddRemoveToggle_KeepAuthoredOrder_AndNeverRepeat()
        {
            Vector2Int[] pattern = { new Vector2Int(2, 0), new Vector2Int(1, 0) };

            Vector2Int[] added = RangePatternLogic.Add(pattern, new Vector2Int(3, 0));
            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 0), new Vector2Int(1, 0), new Vector2Int(3, 0) }, added, "A new tile goes at the end; nothing is sorted.");
            Assert.AreEqual(3, RangePatternLogic.Add(added, new Vector2Int(3, 0)).Length, "Adding a tile that is there changes nothing.");

            Assert.AreEqual(2, RangePatternLogic.Remove(added, new Vector2Int(3, 0)).Length);
            Assert.AreEqual(1, RangePatternLogic.Toggle(pattern, new Vector2Int(2, 0)).Length);
            Assert.AreEqual(3, RangePatternLogic.Toggle(pattern, new Vector2Int(5, 5)).Length);
            Assert.AreEqual(2, pattern.Length, "The original array is not modified.");
        }

        [Test]
        public void Remove_TakesEveryCopyOfARepeatedTile()
        {
            Vector2Int[] repeated = { Vector2Int.zero, new Vector2Int(1, 0), Vector2Int.zero, Vector2Int.zero };
            Assert.IsTrue(RangePatternLogic.HasDuplicates(repeated));
            Assert.AreEqual(2, RangePatternLogic.DistinctCount(repeated));

            CollectionAssert.AreEqual(new[] { Vector2Int.zero, new Vector2Int(1, 0) }, RangePatternLogic.RemoveDuplicates(repeated));
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, RangePatternLogic.Remove(repeated, Vector2Int.zero));
        }

        [Test]
        public void UnionAndSubtract_CombineShapes()
        {
            Vector2Int[] line = RangePatternLogic.Line(1, 3);
            Vector2Int[] union = RangePatternLogic.Union(line, RangePatternLogic.Line(2, 5));
            Assert.AreEqual(5, union.Length, "Overlapping tiles are not repeated.");
            Assert.AreEqual(1, RangePatternLogic.Subtract(line, RangePatternLogic.Line(2, 9)).Length);
        }

        [Test]
        public void Transforms_MoveTheWholePattern()
        {
            Vector2Int[] pattern = { new Vector2Int(1, 0), new Vector2Int(2, 1) };

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0), new Vector2Int(2, -1) }, RangePatternLogic.FlipVertical(pattern));
            CollectionAssert.AreEqual(new[] { new Vector2Int(-1, 0), new Vector2Int(-2, 1) }, RangePatternLogic.FlipHorizontal(pattern));
            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1), new Vector2Int(-1, 2) }, RangePatternLogic.RotateQuarterTurn(pattern));
            CollectionAssert.AreEqual(new[] { new Vector2Int(2, 0), new Vector2Int(3, 1) }, RangePatternLogic.Shift(pattern, Vector2Int.right));

            Assert.AreEqual(0, RangePatternLogic.FlipVertical(null).Length, "A missing pattern is an empty one.");
        }

        [Test]
        public void FourQuarterTurns_ComeBackToTheStart()
        {
            Vector2Int[] pattern = { new Vector2Int(1, 0), new Vector2Int(2, 1), new Vector2Int(-1, 3) };
            Vector2Int[] turned = pattern;
            for (int i = 0; i < 4; i++) turned = RangePatternLogic.RotateQuarterTurn(turned);
            CollectionAssert.AreEqual(pattern, turned);
        }

        // ---------- shapes ----------

        [Test]
        public void Shapes_HaveTheTilesTheyShould()
        {
            Assert.AreEqual(3, RangePatternLogic.Line(1, 3).Length);
            Assert.IsFalse(RangePatternLogic.Contains(RangePatternLogic.Line(-1, 1), Vector2Int.zero), "A line never includes the operator's own tile.");
            Assert.AreEqual(8, RangePatternLogic.Cross(2).Length);
            Assert.AreEqual(12, RangePatternLogic.Diamond(2).Length);
            Assert.AreEqual(8, RangePatternLogic.Square(1).Length);
            Assert.AreEqual(24, RangePatternLogic.Square(2).Length);
            Assert.AreEqual(6, RangePatternLogic.ForwardBlock(2, 3).Length);
            Assert.AreEqual(9, RangePatternLogic.Cone(3).Length, "1 + 3 + 5 tiles.");
        }

        [Test]
        public void ForwardBlock_IsCentredOnTheOperatorsRow()
        {
            Vector2Int[] block = RangePatternLogic.ForwardBlock(3, 3);
            Assert.IsTrue(RangePatternLogic.Contains(block, new Vector2Int(3, 1)));
            Assert.IsTrue(RangePatternLogic.Contains(block, new Vector2Int(3, -1)));
            Assert.IsFalse(RangePatternLogic.Contains(block, new Vector2Int(3, 2)));
            Assert.IsFalse(RangePatternLogic.Contains(block, new Vector2Int(0, 0)));
        }

        // ---------- reading ----------

        [Test]
        public void SameCells_IgnoresOrderAndRepeats()
        {
            Assert.IsTrue(RangePatternLogic.SameCells(new[] { new Vector2Int(1, 0), new Vector2Int(2, 0) }, new[] { new Vector2Int(2, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }));
            Assert.IsFalse(RangePatternLogic.SameCells(new[] { new Vector2Int(1, 0) }, new[] { new Vector2Int(2, 0) }));
            Assert.IsTrue(RangePatternLogic.SameCells(null, new Vector2Int[0]));
        }

        [Test]
        public void RadiusAndDescribe_SummarisePatterns()
        {
            Assert.AreEqual(0, RangePatternLogic.Radius(null));
            Assert.AreEqual(8, RangePatternLogic.Radius(new[] { new Vector2Int(8, 0), new Vector2Int(2, -3) }));
            StringAssert.Contains("up to 8 in front", RangePatternLogic.Describe(new[] { new Vector2Int(2, 0), new Vector2Int(8, 0) }));
            Assert.AreEqual("No tiles.", RangePatternLogic.Describe(new Vector2Int[0]));
        }

        [Test]
        public void Validate_FlagsEmptyRepeatedAndFarTiles()
        {
            Assert.AreEqual(1, RangePatternLogic.Validate(new Vector2Int[0]).Count);
            Assert.AreEqual(0, RangePatternLogic.Validate(new[] { new Vector2Int(1, 0) }).Count);
            Assert.AreEqual(1, RangePatternLogic.Validate(new[] { new Vector2Int(1, 0), new Vector2Int(1, 0) }).Count);
            Assert.AreEqual(1, RangePatternLogic.Validate(new[] { new Vector2Int(RangePatternLogic.MaxRadius + 1, 0) }).Count);
        }

        // ---------- the real operators ----------

        [Test]
        public void EveryOperatorsPatterns_FitInTheWindow_AtEveryStar()
        {
            var operators = UnitIntelRegistry.FindProjectAssets<OperatorData>();
            Assert.Greater(operators.Count, 0);

            foreach (OperatorData op in operators)
            {
                foreach (OperatorRarity star in new[] { OperatorRarity.Star1, OperatorRarity.Star2, OperatorRarity.Star3, OperatorRarity.Star4, OperatorRarity.Star5 })
                {
                    Vector2Int[] pattern = op.GetRangePatternForRarity(star);
                    Assert.LessOrEqual(RangePatternLogic.Radius(pattern), RangePatternLogic.MaxRadius, op.operatorName + " at " + star);
                }
                Assert.LessOrEqual(RangePatternLogic.Radius(op.GetTalentRangePattern()), RangePatternLogic.MaxRadius, op.operatorName + " talent range");
            }
        }

        [Test]
        public void BasurocketsFourStarRange_IsReadFromCode()
        {
            OperatorData basurocket = UnitIntelRegistry.FindProjectAssets<OperatorData>().FirstOrDefault(o => o.operatorName == "Basurocket");
            Assert.IsNotNull(basurocket);

            Vector2Int[] baseRange = basurocket.GetRangePatternForRarity(OperatorRarity.Star1);
            Vector2Int[] upgraded = basurocket.GetRangePatternForRarity(OperatorRarity.Star4);
            Assert.IsTrue(RangePatternLogic.SameCells(baseRange, basurocket.rangePattern), "Below the upgrade the asset's field is used.");
            Assert.IsFalse(RangePatternLogic.SameCells(upgraded, basurocket.rangePattern), "At 4 stars the pattern comes from code, which the window shows read-only.");
            Assert.IsTrue(RangePatternLogic.Contains(upgraded, new Vector2Int(8, 0)));
        }
    }
}
