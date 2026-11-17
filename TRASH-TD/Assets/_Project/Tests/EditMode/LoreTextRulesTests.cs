using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashTD.Data;
using TrashTD.Editor;

namespace TrashTD.Tests
{
    /// <summary>The rules behind the Lore & Text Sheet window (LoreTextRules): checks, find and replace, CSV.</summary>
    public class LoreTextRulesTests
    {
        private static readonly string[] NoTerms = new string[0];

        private static bool HasIssue(List<LoreIssue> issues, LoreIssueSeverity severity, string containing)
        {
            return issues.Any(i => i.severity == severity && i.message.Contains(containing));
        }

        // ---------- checks ----------

        [Test]
        public void Check_EmptyText_IsAWarningOnlyWhenRequired()
        {
            Assert.IsTrue(HasIssue(LoreTextRules.Check("", 100, true, NoTerms), LoreIssueSeverity.Warning, "Empty"));
            Assert.IsTrue(HasIssue(LoreTextRules.Check("   ", 100, true, NoTerms), LoreIssueSeverity.Warning, "Empty"));
            Assert.AreEqual(0, LoreTextRules.Check("", 100, false, NoTerms).Count);
        }

        [Test]
        public void Check_FlagsSpacingAndLength()
        {
            Assert.IsTrue(HasIssue(LoreTextRules.Check(" padded ", 100, true, NoTerms), LoreIssueSeverity.Info, "start or end"));
            Assert.IsTrue(HasIssue(LoreTextRules.Check("two  spaces", 100, true, NoTerms), LoreIssueSeverity.Info, "double space"));
            Assert.IsTrue(HasIssue(LoreTextRules.Check(new string('a', 61), 60, true, NoTerms), LoreIssueSeverity.Info, "Long: 61"));
            Assert.AreEqual(0, LoreTextRules.Check(new string('a', 60), 60, true, NoTerms).Count, "Exactly at the limit is fine.");
            Assert.AreEqual(0, LoreTextRules.Check(new string('a', 5000), 0, true, NoTerms).Count, "A limit of 0 means no limit.");
        }

        [Test]
        public void Check_FlagsOldNames_AsWholeWordsCaseSensitively()
        {
            string[] old = { "Ignisaur", "Bulkhead" };
            Assert.IsTrue(HasIssue(LoreTextRules.Check("Once the Ignisaur burned.", 100, true, old), LoreIssueSeverity.Warning, "Ignisaur"));
            Assert.IsTrue(HasIssue(LoreTextRules.Check("Ignisaur's flame.", 100, true, old), LoreIssueSeverity.Warning, "Ignisaur"));
            Assert.AreEqual(0, LoreTextRules.Check("He sealed the bulkhead door.", 100, true, old).Count, "An ordinary lowercase word is left alone.");
            Assert.AreEqual(0, LoreTextRules.Check("The Bulkheads rattled.", 100, true, old).Count, "Not a whole word.");
        }

        [Test]
        public void Check_FlagsPlaceholderText()
        {
            Assert.IsTrue(HasIssue(LoreTextRules.Check("TODO write this", 100, true, NoTerms), LoreIssueSeverity.Warning, "placeholder"));
            Assert.IsTrue(HasIssue(LoreTextRules.Check("Lorem ipsum dolor", 100, true, NoTerms), LoreIssueSeverity.Warning, "placeholder"));
        }

        [Test]
        public void WordCount_CountsWords()
        {
            Assert.AreEqual(0, LoreTextRules.WordCount(null));
            Assert.AreEqual(0, LoreTextRules.WordCount("  "));
            Assert.AreEqual(4, LoreTextRules.WordCount("A cinder  drake\nburned"));
        }

        // ---------- find and replace ----------

        [Test]
        public void CountMatches_RespectsCaseAndWholeWord()
        {
            const string text = "Fire, fire and Fireworks.";
            Assert.AreEqual(2, LoreTextRules.CountMatches(text, "fire", false, true));
            Assert.AreEqual(1, LoreTextRules.CountMatches(text, "fire", true, true));
            Assert.AreEqual(3, LoreTextRules.CountMatches(text, "fire", false, false));
            Assert.AreEqual(0, LoreTextRules.CountMatches(text, "", false, false));
        }

        [Test]
        public void Replace_ReplacesEveryMatch_AndUsesTheReplacementLiterally()
        {
            string result = LoreTextRules.Replace("Pyrolite met Pyrolite.", "Pyrolite", "Mechonk", true, true, out int count);
            Assert.AreEqual("Mechonk met Mechonk.", result);
            Assert.AreEqual(2, count);

            result = LoreTextRules.Replace("cost 5", "5", "$1 and \\n", true, false, out count);
            Assert.AreEqual("cost $1 and \\n", result, "$ and backslashes in the replacement are plain text.");
            Assert.AreEqual(1, count);

            Assert.AreEqual("same", LoreTextRules.Replace("same", "missing", "x", true, true, out count));
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Replace_TreatsTheSearchAsPlainText()
        {
            string result = LoreTextRules.Replace("a.b a-b", "a.b", "X", true, false, out int count);
            Assert.AreEqual("X a-b", result, "The dot is not a wildcard.");
            Assert.AreEqual(1, count);
        }

        // ---------- CSV ----------

        [Test]
        public void Csv_RoundTripsAwkwardText()
        {
            var rows = new List<string[]>
            {
                new[] { "Tab", "Text" },
                new[] { "Operators", "He said \"no\", then left." },
                new[] { "Operators", "Two\nlines\r\nhere" },
                new[] { "Enemies", "  leading space kept" },
                new[] { "Enemies", "" },
                new[] { "Stages", "caf\u00e9 \u2605" }
            };

            List<string[]> back = LoreTextRules.ParseCsv(LoreTextRules.WriteCsv(rows));

            Assert.AreEqual(rows.Count, back.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(rows[i].Length, back[i].Length, "row " + i);
                for (int j = 0; j < rows[i].Length; j++)
                    Assert.AreEqual(LoreTextRules.NormalizeLineEndings(rows[i][j]), back[i][j], "row " + i + " field " + j);
            }
        }

        [Test]
        public void ParseCsv_HandlesBomBlankLinesAndMissingFinalNewline()
        {
            List<string[]> table = LoreTextRules.ParseCsv("\uFEFFa,b\r\n\r\n\"x,y\",z");
            Assert.AreEqual(2, table.Count);
            CollectionAssert.AreEqual(new[] { "a", "b" }, table[0]);
            CollectionAssert.AreEqual(new[] { "x,y", "z" }, table[1]);
            Assert.AreEqual(0, LoreTextRules.ParseCsv("").Count);
        }

        [Test]
        public void ParseCsv_KeepsEmptyFieldsInPlace()
        {
            List<string[]> table = LoreTextRules.ParseCsv("a,,c\n,,\n");
            CollectionAssert.AreEqual(new[] { "a", "", "c" }, table[0]);
            CollectionAssert.AreEqual(new[] { "", "", "" }, table[1]);
        }

        // ---------- the real text ----------

        [Test]
        public void NoUnitText_StillMentionsAnOldName()
        {
            var texts = new List<string>();
            foreach (OperatorData op in UnitIntelRegistry.FindProjectAssets<OperatorData>())
            {
                texts.Add(op.intelLore);
                texts.Add(op.skillDescription);
            }
            foreach (EnemyData enemy in UnitIntelRegistry.FindProjectAssets<EnemyData>())
            {
                texts.Add(enemy.intelLore);
                texts.Add(enemy.abilityDescription);
            }

            foreach (string text in texts)
            {
                List<LoreIssue> issues = LoreTextRules.Check(text, 0, false, LoreTextRules.DefaultOldTerms);
                Assert.IsFalse(issues.Any(i => i.message.Contains("old name")), text);
            }
        }
    }
}
