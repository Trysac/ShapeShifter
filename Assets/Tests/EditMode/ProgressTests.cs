using System.Linq;
using NUnit.Framework;

namespace ShapeShifter.Core.Tests
{
    /// <summary>What is kept between runs: coins and skins, the leaderboard and the settings.</summary>
    public class ProgressTests
    {
        // ---- Wallet and skins ----

        [Test]
        public void TheFirstSkinIsFreeOwnedAndSelected()
        {
            var wallet = new Wallet(new MemoryStore());
            Assert.That(Skins.All.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(Skins.All[0].Price, Is.EqualTo(0));
            Assert.That(wallet.Coins, Is.EqualTo(0));
            Assert.That(wallet.Owns(Skins.All[0].Id), Is.True);
            Assert.That(wallet.Selected, Is.EqualTo(Skins.All[0].Id));
            Assert.That(wallet.Owns(Skins.All[1].Id), Is.False);
            for (int i = 1; i < Skins.All.Count; i++)
            {
                Assert.That(Skins.All[i].Price, Is.GreaterThan(Skins.All[i - 1].Price), "prices rise along the list");
            }
        }

        [Test]
        public void DepositedCoinsAddUpAndPersist()
        {
            var store = new MemoryStore();
            var wallet = new Wallet(store);
            wallet.Deposit(30);
            wallet.Deposit(12);
            wallet.Deposit(-5);
            Assert.That(wallet.Coins, Is.EqualTo(42));
            Assert.That(new Wallet(store).Coins, Is.EqualTo(42));
            Assert.That(store.SaveCount, Is.EqualTo(2), "a negative deposit is ignored");
        }

        [Test]
        public void BuyingASkinCostsItsPriceAndUnlocksItForGood()
        {
            var store = new MemoryStore();
            var wallet = new Wallet(store);
            Skin skin = Skins.All[1];
            wallet.Deposit(skin.Price + 5);
            Assert.That(wallet.Buy(skin.Id), Is.EqualTo(PurchaseResult.Bought));
            Assert.That(wallet.Coins, Is.EqualTo(5));
            Assert.That(wallet.Owns(skin.Id), Is.True);
            var reloaded = new Wallet(store);
            Assert.That(reloaded.Owns(skin.Id), Is.True);
            Assert.That(reloaded.Coins, Is.EqualTo(5));
            Assert.That(reloaded.Buy(skin.Id), Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(reloaded.Coins, Is.EqualTo(5));
        }

        [Test]
        public void ASkinYouCannotAffordStaysLockedAndCostsNothing()
        {
            var wallet = new Wallet(new MemoryStore());
            Skin skin = Skins.All[2];
            wallet.Deposit(skin.Price - 1);
            Assert.That(wallet.Buy(skin.Id), Is.EqualTo(PurchaseResult.NotEnoughCoins));
            Assert.That(wallet.Coins, Is.EqualTo(skin.Price - 1));
            Assert.That(wallet.Owns(skin.Id), Is.False);
            Assert.That(wallet.Buy("no-such-skin"), Is.EqualTo(PurchaseResult.Unknown));
        }

        [Test]
        public void OnlyAnOwnedSkinCanBeSelectedAndTheChoiceIsKept()
        {
            var store = new MemoryStore();
            var wallet = new Wallet(store);
            Skin skin = Skins.All[1];
            Assert.That(wallet.Select(skin.Id), Is.False);
            Assert.That(wallet.Selected, Is.EqualTo(Skins.All[0].Id));
            wallet.Deposit(skin.Price);
            wallet.Buy(skin.Id);
            Assert.That(wallet.Select(skin.Id), Is.True);
            Assert.That(new Wallet(store).Selected, Is.EqualTo(skin.Id));
            Assert.That(Skins.IndexOf(skin.Id), Is.EqualTo(1));
        }

        [Test]
        public void AStoredSelectionOfAnUnknownSkinFallsBackToTheFreeOne()
        {
            var store = new MemoryStore();
            store.SetString(Wallet.SelectedKey, "deleted-skin");
            Assert.That(new Wallet(store).Selected, Is.EqualTo(Skins.All[0].Id));
        }

        // ---- Leaderboard ----

        [Test]
        public void AnEmptyBoardTakesAnyPositiveScore()
        {
            var board = new Leaderboard(new MemoryStore());
            Assert.That(board.Entries, Is.Empty);
            Assert.That(board.Qualifies(1), Is.True);
            Assert.That(board.Qualifies(0), Is.False);
        }

        [Test]
        public void ScoresAreKeptHighestFirstAndATieGoesBelowTheOlderEntry()
        {
            var board = new Leaderboard(new MemoryStore());
            Assert.That(board.Submit("ann", 500, 300), Is.EqualTo(0));
            Assert.That(board.Submit("bob", 900, 600), Is.EqualTo(0));
            Assert.That(board.Submit("cy", 500, 280), Is.EqualTo(2));
            Assert.That(board.Entries.Select(e => e.Name), Is.EqualTo(new[] { "BOB", "ANN", "CY" }));
            Assert.That(board.Entries.Select(e => e.Score), Is.EqualTo(new[] { 900, 500, 500 }));
            Assert.That(board.Entries[0].Distance, Is.EqualTo(600));
        }

        [Test]
        public void TheBoardKeepsTenAndRejectsScoresBelowTheLast()
        {
            var board = new Leaderboard(new MemoryStore());
            for (int i = 1; i <= Leaderboard.Capacity; i++) board.Submit("P" + i, i * 100, i);
            Assert.That(board.Entries.Count, Is.EqualTo(Leaderboard.Capacity));
            Assert.That(board.Qualifies(100), Is.False, "equal to the last is not enough");
            Assert.That(board.Submit("LOW", 50, 1), Is.EqualTo(-1));
            Assert.That(board.Submit("MID", 550, 1), Is.EqualTo(5));
            Assert.That(board.Entries.Count, Is.EqualTo(Leaderboard.Capacity));
            Assert.That(board.Entries.Last().Score, Is.EqualTo(200), "the lowest fell off");
            Assert.That(board.Entries.Any(e => e.Name == "LOW"), Is.False);
        }

        [Test]
        public void TheBoardIsSavedAndLoadedBack()
        {
            var store = new MemoryStore();
            var board = new Leaderboard(store);
            board.Submit("ace", 1234, 800);
            board.Submit("Z9", 77, 40);
            Assert.That(store.SaveCount, Is.EqualTo(2));
            var loaded = new Leaderboard(store);
            Assert.That(loaded.Entries.Select(e => $"{e.Name}:{e.Score}:{e.Distance}"), Is.EqualTo(new[] { "ACE:1234:800", "Z9:77:40" }));
        }

        [Test]
        public void ACorruptSavedBoardKeepsOnlyItsValidEntries()
        {
            var store = new MemoryStore();
            store.SetString(Leaderboard.Key, "AAA:10:5|garbage|BB:x:1|CCC:30:9|::");
            var board = new Leaderboard(store);
            Assert.That(board.Entries.Select(e => e.Name), Is.EqualTo(new[] { "CCC", "AAA" }));
        }

        [TestCase("abc", "ABC")]
        [TestCase("a-b c!d", "ABC")]
        [TestCase("zz9top", "ZZ9")]
        [TestCase("", "???")]
        [TestCase(null, "???")]
        [TestCase("ñé", "???")]
        public void NamesAreUpToThreeCapitalLettersOrDigits(string raw, string clean)
        {
            Assert.That(Leaderboard.CleanName(raw), Is.EqualTo(clean));
        }

        // ---- Settings ----

        [Test]
        public void SettingsStartAtTheirDefaultsAndPersistChanges()
        {
            var store = new MemoryStore();
            var settings = new Settings(store);
            Assert.That(settings.MusicVolume, Is.EqualTo(7));
            Assert.That(settings.EffectsVolume, Is.EqualTo(8));
            Assert.That(settings.ScreenShake, Is.True);
            Assert.That(settings.TouchButtons, Is.EqualTo(TouchButtons.Auto));
            settings.StepMusic(-3);
            settings.StepEffects(5);
            settings.ToggleShake();
            settings.StepTouchButtons(1);
            var loaded = new Settings(store);
            Assert.That(loaded.MusicVolume, Is.EqualTo(4));
            Assert.That(loaded.EffectsVolume, Is.EqualTo(Settings.MaxVolume));
            Assert.That(loaded.ScreenShake, Is.False);
            Assert.That(loaded.TouchButtons, Is.EqualTo(TouchButtons.On));
            Assert.That(loaded.MusicGain, Is.EqualTo(0.4f).Within(1e-5f));
        }

        [Test]
        public void TouchButtonModesCycleBothWays()
        {
            var settings = new Settings(new MemoryStore());
            settings.StepTouchButtons(-1);
            Assert.That(settings.TouchButtons, Is.EqualTo(TouchButtons.Off));
            settings.StepTouchButtons(1);
            settings.StepTouchButtons(1);
            settings.StepTouchButtons(1);
            Assert.That(settings.TouchButtons, Is.EqualTo(TouchButtons.Off));
            Assert.That(Settings.ShowTouch(TouchButtons.Auto, true), Is.True);
            Assert.That(Settings.ShowTouch(TouchButtons.Auto, false), Is.False);
            Assert.That(Settings.ShowTouch(TouchButtons.On, false), Is.True);
            Assert.That(Settings.ShowTouch(TouchButtons.Off, true), Is.False);
        }

        [Test]
        public void StoredVolumesOutOfRangeAreClamped()
        {
            var store = new MemoryStore();
            store.SetInt(Settings.MusicKey, 99);
            store.SetInt(Settings.EffectsKey, -4);
            var settings = new Settings(store);
            Assert.That(settings.MusicVolume, Is.EqualTo(Settings.MaxVolume));
            Assert.That(settings.EffectsVolume, Is.EqualTo(0));
        }
    }
}
