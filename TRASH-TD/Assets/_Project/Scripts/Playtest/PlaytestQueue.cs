using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrashTD.Playtest
{
    /// <summary>One event waiting to be sent: the six columns of the form, and how many times sending it has failed.</summary>
    [Serializable]
    public sealed class PlaytestEntry
    {
        public string session;
        public string platform;
        public string build;
        public string evt;
        public string stage;
        public string data;
        public int attempts;
    }

    /// <summary>
    /// The events not sent yet. It is saved to disk so a phone that is offline (or closed mid-stage) delivers them next
    /// time. Bounded: when full the oldest event is dropped, and an event that fails too often is given up on, so a
    /// broken form can never grow the file without end.
    /// </summary>
    [Serializable]
    public sealed class PlaytestQueue
    {
        public const int MaxEntries = 300;
        public const int MaxAttempts = 6;

        public List<PlaytestEntry> items = new List<PlaytestEntry>();

        public int Count => items.Count;

        /// <summary>The next event to send, or null.</summary>
        public PlaytestEntry Peek() => items.Count > 0 ? items[0] : null;

        public void Enqueue(PlaytestEntry entry)
        {
            if (entry == null) return;
            items.Add(entry);
            while (items.Count > MaxEntries) items.RemoveAt(0);
        }

        /// <summary>The first event was delivered.</summary>
        public void Complete()
        {
            if (items.Count > 0) items.RemoveAt(0);
        }

        /// <summary>
        /// Sending the first event failed. It moves behind the others so one bad event cannot block the rest; after
        /// <see cref="MaxAttempts"/> failures it is dropped. Returns true when it was dropped.
        /// </summary>
        public bool Fail()
        {
            if (items.Count == 0) return false;
            PlaytestEntry entry = items[0];
            items.RemoveAt(0);
            entry.attempts++;
            if (entry.attempts >= MaxAttempts) return true;
            items.Add(entry);
            return false;
        }

        public string ToJson() => JsonUtility.ToJson(this);

        /// <summary>Reads a saved queue. Missing or damaged text gives an empty queue.</summary>
        public static PlaytestQueue FromJson(string json)
        {
            try
            {
                PlaytestQueue queue = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<PlaytestQueue>(json);
                if (queue == null || queue.items == null) return new PlaytestQueue();
                queue.items.RemoveAll(entry => entry == null);
                while (queue.items.Count > MaxEntries) queue.items.RemoveAt(0);
                return queue;
            }
            catch (Exception)
            {
                return new PlaytestQueue();
            }
        }

        /// <summary>
        /// The (name, value) pairs posted to the web app for one event. The names are the ones the Apps Script reads
        /// (session, platform, build, event, stage, data). Empty values become "-" so no cell is left blank.
        /// </summary>
        public static List<KeyValuePair<string, string>> BuildFields(PlaytestEntry entry)
        {
            string Or(string value) => string.IsNullOrEmpty(value) ? "-" : value;
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("session", Or(entry.session)),
                new KeyValuePair<string, string>("platform", Or(entry.platform)),
                new KeyValuePair<string, string>("build", Or(entry.build)),
                new KeyValuePair<string, string>("event", Or(entry.evt)),
                new KeyValuePair<string, string>("stage", Or(entry.stage)),
                new KeyValuePair<string, string>("data", Or(entry.data))
            };
        }
    }
}
