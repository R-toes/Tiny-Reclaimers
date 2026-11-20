using System.Collections.Generic;
using NUnit.Framework;
using TrashTD.Playtest;

namespace TrashTD.Tests
{
    /// <summary>The playtest system's pure parts: the config, the JSON the game writes, and the offline queue.</summary>
    public class PlaytestTests
    {
        private const string Base = "https://script.google.com/macros/s/AKfycbxyz123";
        private const string Exec = Base + "/exec";

        // ------------------------------------------------------------------ config

        [TestCase(Exec, Exec)]
        [TestCase(Exec + "?foo=1", Exec)]
        [TestCase(Exec + "#top", Exec)]
        [TestCase(Exec + "/", Exec)]
        [TestCase(Base + "/dev", Exec)]
        [TestCase("  " + Exec + "  ", Exec)]
        public void NormalizeEndpointUrl_EndsInExec(string input, string expected)
        {
            Assert.AreEqual(expected, PlaytestConfig.NormalizeEndpointUrl(input));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a url")]
        [TestCase("http://script.google.com/macros/s/AKfycbxyz/exec")]
        [TestCase("https://example.com/macros/s/AKfycbxyz/exec")]
        [TestCase("https://script.google.com/macros/s/AKfycbxyz/other")]
        [TestCase("https://script.google.com/macros/s//exec")]
        [TestCase("https://docs.google.com/forms/d/e/1FAIpQLSabc/viewform")]
        public void NormalizeEndpointUrl_RefusesAnythingThatIsNotAWebAppAddress(string input)
        {
            Assert.AreEqual(string.Empty, PlaytestConfig.NormalizeEndpointUrl(input));
        }

        [Test]
        public void Parse_ReadsAConfigAndTidiesTheAddress()
        {
            PlaytestConfig config = PlaytestConfig.Parse("{\"endpointUrl\":\"" + Base + "/dev\",\"sendFromEditor\":true}");

            Assert.IsTrue(config.IsComplete);
            Assert.AreEqual(Exec, config.endpointUrl);
            Assert.IsTrue(config.sendFromEditor);
        }

        [Test]
        public void Parse_DevelopmentRunsDoNotSendUnlessAskedTo()
        {
            Assert.IsFalse(PlaytestConfig.Parse("{\"endpointUrl\":\"" + Exec + "\"}").sendFromEditor);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("this is not json")]
        [TestCase("{}")]
        [TestCase("{\"endpointUrl\":\"\"}")]
        [TestCase("{\"endpointUrl\":\"https://example.com/exec\"}")]
        public void Parse_GivesAnIncompleteConfig_ForMissingOrBadText(string json)
        {
            Assert.IsFalse(PlaytestConfig.Parse(json).IsComplete);
        }

        // ------------------------------------------------------------------ json

        [Test]
        public void Json_WritesFlatValues()
        {
            string text = new PlaytestJson().Add("a", "x").Add("n", 3).Add("f", 1.5f).Add("b", true).ToString();
            Assert.AreEqual("{\"a\":\"x\",\"n\":3,\"f\":1.5,\"b\":true}", text);
        }

        [Test]
        public void Json_EmptyObject()
        {
            Assert.AreEqual("{}", new PlaytestJson().ToString());
        }

        [Test]
        public void Json_EscapesQuotesBackslashesAndNewlines()
        {
            string text = new PlaytestJson().Add("c", "say \"hi\"\\\nnext\ttab").ToString();
            Assert.AreEqual("{\"c\":\"say \\\"hi\\\"\\\\\\nnext tab\"}", text);
        }

        [Test]
        public void Json_CutsLongText()
        {
            string text = new PlaytestJson().Add("c", new string('x', 1000)).ToString();
            Assert.AreEqual(PlaytestJson.MaxTextLength + "{\"c\":\"\"}".Length, text.Length);
        }

        [Test]
        public void Json_WritesCountsAndLists_AndSkipsEmptyOnes()
        {
            var counts = new Dictionary<string, int> { { "Guard", 2 }, { "Sniper", 1 } };
            string text = new PlaytestJson().AddCounts("deployed", counts).AddList("picks", new List<string> { "Guard", "Sniper" })
                .AddCounts("none", new Dictionary<string, int>()).AddList("nothing", new List<string>()).ToString();
            Assert.AreEqual("{\"deployed\":{\"Guard\":2,\"Sniper\":1},\"picks\":[\"Guard\",\"Sniper\"]}", text);
        }

        [Test]
        public void Json_NeverWritesNaN()
        {
            string text = new PlaytestJson().Add("f", float.NaN).Add("i", float.PositiveInfinity).ToString();
            Assert.AreEqual("{\"f\":0,\"i\":0}", text);
        }

        // ------------------------------------------------------------------ queue

        private static PlaytestEntry Entry(string name) => new PlaytestEntry { session = "s", platform = "p", build = "b", evt = name, stage = "x", data = "{}" };

        [Test]
        public void Queue_KeepsOrder_AndCompleteRemovesTheFirst()
        {
            var queue = new PlaytestQueue();
            queue.Enqueue(Entry("one"));
            queue.Enqueue(Entry("two"));
            Assert.AreEqual("one", queue.Peek().evt);
            queue.Complete();
            Assert.AreEqual("two", queue.Peek().evt);
            queue.Complete();
            Assert.IsNull(queue.Peek());
            queue.Complete(); // nothing left: no error
        }

        [Test]
        public void Queue_DropsTheOldest_WhenFull()
        {
            var queue = new PlaytestQueue();
            for (int i = 0; i < PlaytestQueue.MaxEntries + 5; i++) queue.Enqueue(Entry("e" + i));
            Assert.AreEqual(PlaytestQueue.MaxEntries, queue.Count);
            Assert.AreEqual("e5", queue.Peek().evt);
        }

        [Test]
        public void Queue_AFailedEventGoesToTheBack_SoItDoesNotBlockTheRest()
        {
            var queue = new PlaytestQueue();
            queue.Enqueue(Entry("bad"));
            queue.Enqueue(Entry("good"));
            Assert.IsFalse(queue.Fail());
            Assert.AreEqual("good", queue.Peek().evt);
            Assert.AreEqual(2, queue.Count);
        }

        [Test]
        public void Queue_GivesUpOnAnEventThatKeepsFailing()
        {
            var queue = new PlaytestQueue();
            queue.Enqueue(Entry("bad"));
            bool dropped = false;
            for (int i = 0; i < PlaytestQueue.MaxAttempts; i++) dropped = queue.Fail();
            Assert.IsTrue(dropped);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void Queue_SurvivesBeingSavedAndLoaded()
        {
            var queue = new PlaytestQueue();
            queue.Enqueue(Entry("one"));
            queue.Enqueue(Entry("two"));
            queue.Fail();

            PlaytestQueue loaded = PlaytestQueue.FromJson(queue.ToJson());
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual("two", loaded.Peek().evt);
            Assert.AreEqual(1, loaded.items[1].attempts);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{{ nope")]
        [TestCase("[]")]
        public void Queue_DamagedFileGivesAnEmptyQueue(string json)
        {
            Assert.AreEqual(0, PlaytestQueue.FromJson(json).Count);
        }

        [Test]
        public void Fields_AreTheSixNamesTheScriptReads_InOrder()
        {
            var entry = new PlaytestEntry { session = "abc-1234", platform = "Android", build = "0.1", evt = "stage_end", stage = "STAGE 1", data = "{\"a\":1}" };

            List<KeyValuePair<string, string>> fields = PlaytestQueue.BuildFields(entry);

            Assert.AreEqual(6, fields.Count);
            Assert.AreEqual(new KeyValuePair<string, string>("session", "abc-1234"), fields[0]);
            Assert.AreEqual(new KeyValuePair<string, string>("platform", "Android"), fields[1]);
            Assert.AreEqual(new KeyValuePair<string, string>("build", "0.1"), fields[2]);
            Assert.AreEqual(new KeyValuePair<string, string>("event", "stage_end"), fields[3]);
            Assert.AreEqual(new KeyValuePair<string, string>("stage", "STAGE 1"), fields[4]);
            Assert.AreEqual(new KeyValuePair<string, string>("data", "{\"a\":1}"), fields[5]);
        }

        [Test]
        public void Fields_FillEmptyValuesWithADash()
        {
            var entry = new PlaytestEntry { session = "s", platform = "p", build = "b", evt = "e", stage = null, data = "" };

            List<KeyValuePair<string, string>> fields = PlaytestQueue.BuildFields(entry);

            Assert.AreEqual("-", fields[4].Value);
            Assert.AreEqual("-", fields[5].Value);
        }

        // ------------------------------------------------------------------ tracker

        [TestCase(0, 0f, 0)]
        [TestCase(60, 1f, 60)]
        [TestCase(30, 1f, 30)]
        [TestCase(100, 0f, 0)]
        [TestCase(90, 2f, 45)]
        public void AverageFps_IsFramesOverSeconds(int frames, float seconds, int expected)
        {
            Assert.AreEqual(expected, PlaytestTracker.AverageFps(frames, seconds));
        }
    }
}
