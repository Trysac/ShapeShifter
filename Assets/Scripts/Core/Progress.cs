using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShapeShifter.Core
{
    /// <summary>Key-value storage (PlayerPrefs in the game, memory in tests).</summary>
    public interface IKeyValueStore
    {
        int GetInt(string key, int fallback);
        string GetString(string key, string fallback);
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void Save();
    }

    public sealed class MemoryStore : IKeyValueStore
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public int SaveCount { get; private set; }

        public int GetInt(string key, int fallback) =>
            values.TryGetValue(key, out string text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        public string GetString(string key, string fallback) => values.TryGetValue(key, out string text) ? text : fallback;

        public void SetInt(string key, int value) => values[key] = value.ToString(CultureInfo.InvariantCulture);

        public void SetString(string key, string value) => values[key] = value ?? "";

        public void Save() => SaveCount++;
    }

    /// <summary>A look for all four forms, bought with coins on the Characters screen.</summary>
    public sealed class Skin
    {
        public Skin(string id, string name, int price, string blurb)
        {
            Id = id;
            Name = name;
            Price = price;
            Blurb = blurb;
        }

        public string Id { get; }
        public string Name { get; }
        public int Price { get; }
        public string Blurb { get; }
    }

    public static class Skins
    {
        public static readonly IReadOnlyList<Skin> All = new[]
        {
            new Skin("ember", "Ember", 0, "The first spirit: a russet fox with a warm glow."),
            new Skin("moss", "Moss", 150, "Green as the forest floor it was born from."),
            new Skin("frost", "Frost", 400, "Pale and quick, from the caves where ice never melts."),
            new Skin("dusk", "Dusk", 800, "Violet like the hour the old shapeshifters wake."),
            new Skin("gilded", "Gilded", 1500, "Every coin you ever spent, worn as a coat."),
        };

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id) return i;
            }
            return -1;
        }

        public static Skin Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? All[index] : null;
        }
    }

    public enum PurchaseResult
    {
        Bought,
        AlreadyOwned,
        NotEnoughCoins,
        Unknown,
    }

    /// <summary>The coins banked from every run, the skins they bought and the one worn.</summary>
    public sealed class Wallet
    {
        public const string CoinsKey = "ShapeShifter.Coins";
        public const string OwnedKey = "ShapeShifter.OwnedSkins";
        public const string SelectedKey = "ShapeShifter.SelectedSkin";

        readonly IKeyValueStore store;
        readonly HashSet<string> owned = new HashSet<string>();

        public Wallet(IKeyValueStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            Coins = Math.Max(0, store.GetInt(CoinsKey, 0));
            foreach (string id in store.GetString(OwnedKey, "").Split(','))
            {
                if (Skins.Find(id) != null) owned.Add(id);
            }
            string selected = store.GetString(SelectedKey, Skins.All[0].Id);
            Selected = Owns(selected) ? selected : Skins.All[0].Id;
        }

        public int Coins { get; private set; }
        public string Selected { get; private set; }

        public bool Owns(string id)
        {
            Skin skin = Skins.Find(id);
            return skin != null && (skin.Price == 0 || owned.Contains(id));
        }

        public void Deposit(int coins)
        {
            if (coins <= 0) return;
            Coins += coins;
            Persist();
        }

        public PurchaseResult Buy(string id)
        {
            Skin skin = Skins.Find(id);
            if (skin == null) return PurchaseResult.Unknown;
            if (Owns(id)) return PurchaseResult.AlreadyOwned;
            if (Coins < skin.Price) return PurchaseResult.NotEnoughCoins;
            Coins -= skin.Price;
            owned.Add(id);
            Persist();
            return PurchaseResult.Bought;
        }

        public bool Select(string id)
        {
            if (!Owns(id)) return false;
            Selected = id;
            Persist();
            return true;
        }

        void Persist()
        {
            store.SetInt(CoinsKey, Coins);
            var ids = new List<string>(owned);
            ids.Sort(StringComparer.Ordinal);
            store.SetString(OwnedKey, string.Join(",", ids));
            store.SetString(SelectedKey, Selected);
            store.Save();
        }
    }

    public readonly struct LeaderboardEntry
    {
        public LeaderboardEntry(string name, int score, int distance)
        {
            Name = name;
            Score = score;
            Distance = distance;
        }

        public string Name { get; }
        public int Score { get; }
        /// <summary>Metres run.</summary>
        public int Distance { get; }
    }

    /// <summary>The ten best runs on this device, highest first, with three-character names.</summary>
    public sealed class Leaderboard
    {
        public const int Capacity = 10;
        public const int NameLength = 3;
        public const string Key = "ShapeShifter.Leaderboard";

        readonly IKeyValueStore store;
        readonly List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

        public Leaderboard(IKeyValueStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            foreach (string record in store.GetString(Key, "").Split('|'))
            {
                string[] parts = record.Split(':');
                if (parts.Length != 3) continue;
                string name = CleanName(parts[0]);
                if (name != parts[0] || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score) || score <= 0) continue;
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int distance)) continue;
                Place(new LeaderboardEntry(name, score, Math.Max(0, distance)));
            }
            Trim();
        }

        public IReadOnlyList<LeaderboardEntry> Entries => entries;

        public bool Qualifies(int score) => score > 0 && (entries.Count < Capacity || score > entries[entries.Count - 1].Score);

        /// <summary>Adds a run and saves the board. Returns its rank (0 is the top), or -1 when it did not qualify.</summary>
        public int Submit(string name, int score, int distance)
        {
            if (!Qualifies(score)) return -1;
            int rank = Place(new LeaderboardEntry(CleanName(name), score, Math.Max(0, distance)));
            Trim();
            store.SetString(Key, Serialize());
            store.Save();
            return rank;
        }

        /// <summary>Up to three capital letters or digits; "???" when nothing usable is left.</summary>
        public static string CleanName(string raw)
        {
            var name = new StringBuilder(NameLength);
            if (raw != null)
            {
                foreach (char c in raw.ToUpperInvariant())
                {
                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) name.Append(c);
                    if (name.Length == NameLength) break;
                }
            }
            return name.Length > 0 ? name.ToString() : "???";
        }

        // Below every entry with the same or a higher score, so older ties stay ahead.
        int Place(LeaderboardEntry entry)
        {
            int i = 0;
            while (i < entries.Count && entries[i].Score >= entry.Score) i++;
            entries.Insert(i, entry);
            return i;
        }

        void Trim()
        {
            if (entries.Count > Capacity) entries.RemoveRange(Capacity, entries.Count - Capacity);
        }

        string Serialize()
        {
            var parts = new List<string>(entries.Count);
            foreach (LeaderboardEntry e in entries)
            {
                parts.Add(e.Name + ":" + e.Score.ToString(CultureInfo.InvariantCulture) + ":" + e.Distance.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join("|", parts);
        }
    }

    public enum TouchButtons
    {
        /// <summary>Shown on touch screens only.</summary>
        Auto,
        On,
        Off,
    }

    /// <summary>Player options, clamped and persisted. Volumes go from 0 to 10.</summary>
    public sealed class Settings
    {
        public const int MaxVolume = 10;
        public const string MusicKey = "ShapeShifter.MusicVolume";
        public const string EffectsKey = "ShapeShifter.EffectsVolume";
        public const string ShakeKey = "ShapeShifter.ScreenShake";
        public const string TouchKey = "ShapeShifter.TouchButtons";

        readonly IKeyValueStore store;

        public Settings(IKeyValueStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            MusicVolume = Clamp(store.GetInt(MusicKey, 7));
            EffectsVolume = Clamp(store.GetInt(EffectsKey, 8));
            ScreenShake = store.GetInt(ShakeKey, 1) != 0;
            int touch = store.GetInt(TouchKey, 0);
            TouchButtons = touch >= 0 && touch <= 2 ? (TouchButtons)touch : TouchButtons.Auto;
        }

        public int MusicVolume { get; private set; }
        public int EffectsVolume { get; private set; }
        public bool ScreenShake { get; private set; }
        public TouchButtons TouchButtons { get; private set; }

        public float MusicGain => MusicVolume / (float)MaxVolume;
        public float EffectsGain => EffectsVolume / (float)MaxVolume;

        public void StepMusic(int delta)
        {
            MusicVolume = Clamp(MusicVolume + delta);
            Persist();
        }

        public void StepEffects(int delta)
        {
            EffectsVolume = Clamp(EffectsVolume + delta);
            Persist();
        }

        public void ToggleShake()
        {
            ScreenShake = !ScreenShake;
            Persist();
        }

        public void StepTouchButtons(int delta)
        {
            TouchButtons = (TouchButtons)(((int)TouchButtons + delta % 3 + 3) % 3);
            Persist();
        }

        /// <summary>Whether the on-screen buttons show, given the mode and whether the device has a touch screen.</summary>
        public static bool ShowTouch(TouchButtons mode, bool touchScreen) => mode == TouchButtons.On || (mode == TouchButtons.Auto && touchScreen);

        void Persist()
        {
            store.SetInt(MusicKey, MusicVolume);
            store.SetInt(EffectsKey, EffectsVolume);
            store.SetInt(ShakeKey, ScreenShake ? 1 : 0);
            store.SetInt(TouchKey, (int)TouchButtons);
            store.Save();
        }

        static int Clamp(int volume) => Math.Max(0, Math.Min(MaxVolume, volume));
    }
}
