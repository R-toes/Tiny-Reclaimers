using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;

namespace TrashTD.Tests
{
    /// <summary>Acid low ground (the old Trap tile) and acid high ground: where units can stand and walk, and the acid they take.</summary>
    public class AcidTileTests
    {
        private static GridCell Cell(TileType type)
        {
            return new GridCell(0, 0, type, Vector3.zero);
        }

        [Test]
        public void SavedTileValues_DidNotMove()
        {
            // StageData.tileLayout stores these numbers: renaming Trap to AcidLowGround must not renumber anything.
            Assert.AreEqual(6, (int)TileType.AcidLowGround, "The old Trap value.");
            Assert.AreEqual(7, (int)TileType.Hole);
            Assert.AreEqual(8, (int)TileType.AcidHighGround, "New tiles are added at the end.");
        }

        [Test]
        public void AcidLowGround_IsAWalkableLane_ForMeleeOperatorsOnly()
        {
            GridCell cell = Cell(TileType.AcidLowGround);
            Assert.IsTrue(cell.IsWalkable);
            Assert.IsTrue(cell.CanDeploy(OperatorPosition.Melee));
            Assert.IsFalse(cell.CanDeploy(OperatorPosition.Ranged), "Like plain low ground, melee operators only.");
            Assert.IsTrue(cell.CanDeploy(OperatorPosition.Any));
            Assert.IsTrue(cell.IsAcidHazard);
            Assert.AreEqual(AcidTileRules.DamagePerTick, cell.AcidDamagePerTick);
        }

        [Test]
        public void EachAcidTile_AllowsTheSameOperatorsAsItsPlainGround()
        {
            foreach (OperatorPosition position in new[] { OperatorPosition.Melee, OperatorPosition.Ranged, OperatorPosition.Any })
            {
                Assert.AreEqual(Cell(TileType.LowGround).SupportsPosition(position), Cell(TileType.AcidLowGround).SupportsPosition(position), "Acid low ground vs low ground, " + position);
                Assert.AreEqual(Cell(TileType.HighGround).SupportsPosition(position), Cell(TileType.AcidHighGround).SupportsPosition(position), "Acid high ground vs high ground, " + position);
            }
        }

        [Test]
        public void AcidHighGround_IsHighGroundWithAcid()
        {
            GridCell cell = Cell(TileType.AcidHighGround);
            Assert.IsFalse(cell.IsWalkable, "Enemies cannot walk on high ground.");
            Assert.IsTrue(cell.IsAirPassable);
            Assert.IsTrue(cell.CanDeploy(OperatorPosition.Ranged));
            Assert.IsFalse(cell.CanDeploy(OperatorPosition.Melee), "Like plain high ground, ranged operators only.");
            Assert.IsTrue(cell.CanDeploy(OperatorPosition.Any));
            Assert.IsTrue(cell.IsAcidHazard, "A ranged operator standing here takes acid damage.");
            Assert.AreEqual(AcidTileRules.DamagePerTick, cell.AcidDamagePerTick);
        }

        [Test]
        public void PlainGroundTiles_AreNotAcid()
        {
            Assert.IsFalse(Cell(TileType.LowGround).IsAcidHazard);
            Assert.IsFalse(Cell(TileType.HighGround).IsAcidHazard);
            Assert.AreEqual(0, Cell(TileType.HighGround).AcidDamagePerTick);
        }

        [Test]
        public void TileTypeRules_AnswerForBothAcidTiles()
        {
            Assert.IsTrue(TileTypeRules.IsAcid(TileType.AcidLowGround));
            Assert.IsTrue(TileTypeRules.IsAcid(TileType.AcidHighGround));
            Assert.IsFalse(TileTypeRules.IsAcid(TileType.LowGround));
            Assert.IsTrue(TileTypeRules.IsHighGround(TileType.HighGround));
            Assert.IsTrue(TileTypeRules.IsHighGround(TileType.AcidHighGround));
            Assert.IsFalse(TileTypeRules.IsHighGround(TileType.AcidLowGround));
        }

        [Test]
        public void SetTileType_UsesTheSameWalkabilityAsTheConstructor()
        {
            foreach (TileType type in System.Enum.GetValues(typeof(TileType)))
            {
                GridCell changed = Cell(TileType.Blocked);
                changed.SetTileType(type);
                Assert.AreEqual(Cell(type).IsWalkable, changed.IsWalkable, type + " must be walkable (or not) the same way however the cell got that type.");
            }
        }
    }
}
