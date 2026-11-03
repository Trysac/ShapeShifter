using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShapeShifter.Core;
using ShapeShifter.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShapeShifter.Tests
{
    /// <summary>The game's flow in the real scene: title, run, shifting, pause, game over, name entry and leaderboard.</summary>
    public class FlowTests
    {
        GameDirector director;

        IEnumerator Load()
        {
            SceneManager.LoadScene("ShapeShifter");
            yield return null;
            yield return null;
            director = Object.FindAnyObjectByType<GameDirector>();
            Assert.That(director, Is.Not.Null);
            director.UseMemoryStore();
            director.ScriptedInput = true;
            director.NextSeed = 2021;
            director.ToTitle();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            Assert.That(director.Hud.MenuOpen, Is.True, "the title menu is showing");
            Assert.That(director.Demo, Is.True, "a demo run plays behind the title");
        }

        static IEnumerator Wait(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds) yield return null;
        }

        static int Item(GameDirector director, string label) =>
            director.Hud.Page.Items.FindIndex(i => i.Label == label);

        [UnityTest]
        public IEnumerator TheTitleOffersPlayCharactersLeaderboardAndSettings()
        {
            yield return Load();
            string[] labels = director.Hud.Page.Items.Select(i => i.Label).ToArray();
            Assert.That(labels.Take(4), Is.EqualTo(new[] { "Play", "Characters", "Leaderboard", "Settings" }));
            float x = director.Run.X;
            yield return Wait(0.4f);
            Assert.That(director.Run.X, Is.GreaterThan(x), "the demo runner keeps running");
        }

        [UnityTest]
        public IEnumerator PlayStartsARunThatMovesAndShiftsOnInput()
        {
            yield return Load();
            director.Hud.Page.Items[Item(director, "Play")].Activate();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Demo, Is.False);
            Assert.That(director.Hud.MenuOpen, Is.False);
            Assert.That(director.Run.Track.Seed, Is.EqualTo(2021));
            Assert.That(director.Run.Health.Hearts, Is.EqualTo(5));
            yield return Wait(0.3f);
            Assert.That(director.Run.X, Is.GreaterThan(1f));
            director.Press(Form.Golem, false);
            yield return Wait(0.1f);
            Assert.That(director.Run.Form, Is.EqualTo(Form.Golem));
        }

        [UnityTest]
        public IEnumerator TheFirstWallIsAnnouncedAndTheGolemSmashesIt()
        {
            yield return Load();
            director.StartRun();
            Obstacle wall = director.Run.Track.Obstacles.First();
            Assert.That(wall.Kind, Is.EqualTo(ObstacleKind.Wall));
            director.AdvanceUntil(r => r.X > wall.TelegraphX + 1f, 30f);
            Assert.That(director.Hud.BannerText, Does.Contain("GOLEM"));
            director.Press(Form.Golem, false);
            director.AdvanceUntil(r => r.X > wall.End + 0.5f, 10f);
            Assert.That(wall.Smashed, Is.True);
            Assert.That(wall.Failed, Is.False);
            Assert.That(director.Run.Health.Hearts, Is.EqualTo(5));
            Assert.That(director.Run.Score.Streak, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseFreezesTheRunAndResumeContinuesIt()
        {
            yield return Load();
            director.StartRun();
            yield return Wait(0.3f);
            director.TogglePause();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused));
            Assert.That(director.Hud.Page.Title, Is.EqualTo("PAUSED"));
            float x = director.Run.X;
            float elapsed = director.Run.Elapsed;
            yield return Wait(0.4f);
            Assert.That(director.Run.X, Is.EqualTo(x), "the runner stops");
            Assert.That(director.Run.Elapsed, Is.EqualTo(elapsed), "the clock stops");
            director.TogglePause();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Hud.MenuOpen, Is.False);
            yield return Wait(0.3f);
            Assert.That(director.Run.X, Is.GreaterThan(x));
        }

        [UnityTest]
        public IEnumerator AGameOverBanksTheCoinsAndPutsTheNameOnTheLeaderboard()
        {
            yield return Load();
            director.StartRun();
            director.AutoPlay = true;
            director.AdvanceUntil(r => r.Distance > 300f, 60f);
            director.AutoPlay = false;
            int coins = director.Run.Score.Coins;
            int score = director.Run.Score.Total;
            Assert.That(coins, Is.GreaterThan(0));

            director.TogglePause();
            director.Hud.Page.Items[Item(director, "End run")].Activate();
            Assert.That(director.Run.Over, Is.True);
            yield return Wait(1.6f);
            Assert.That(director.Screen, Is.EqualTo(GameScreen.NameEntry), "an empty board takes any score");
            Assert.That(director.Wallet.Coins, Is.EqualTo(coins), "the run's coins are banked");

            director.TypeLetter('z');
            director.TypeLetter('o');
            director.TypeLetter('e');
            Assert.That(director.EnteredName, Is.EqualTo("ZOE"));
            director.SubmitName();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Leaderboard));
            Assert.That(director.LastRank, Is.EqualTo(0));
            Assert.That(director.Leaderboard.Entries[0].Name, Is.EqualTo("ZOE"));
            Assert.That(director.Leaderboard.Entries[0].Score, Is.EqualTo(score));

            director.Hud.Page.Back();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.GameOver));
            Assert.That(director.Hud.Page.Subtitle, Is.EqualTo("#1 on the leaderboard"));
            director.Hud.Page.Items[Item(director, "Run again")].Activate();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Playing));
            Assert.That(director.Run.Score.Total, Is.LessThan(score), "a fresh run");
        }

        [UnityTest]
        public IEnumerator CoinsBuyAndWearASkinOnTheCharactersScreen()
        {
            yield return Load();
            director.Wallet.Deposit(Skins.All[1].Price + 10);
            director.Hud.Page.Items[Item(director, "Characters")].Activate();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Characters));
            director.Hud.Page.Items[0].Adjust(1);
            Assert.That(director.Hud.Page.Items[0].Label, Does.StartWith("Buy Moss"));
            director.Hud.Page.Items[0].Activate();
            Assert.That(director.Wallet.Owns("moss"), Is.True);
            Assert.That(director.Wallet.Selected, Is.EqualTo("moss"));
            Assert.That(director.Wallet.Coins, Is.EqualTo(10));
            Assert.That(director.Hud.Page.Items[0].Label, Is.EqualTo("Wearing Moss"));
            director.Hud.Page.Back();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Title));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsChangedFromThePauseMenuAreKept()
        {
            yield return Load();
            director.StartRun();
            director.TogglePause();
            director.Hud.Page.Items[Item(director, "Settings")].Activate();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Settings));
            int music = director.Settings.MusicVolume;
            director.Hud.Page.Items[0].Adjust(-1);
            Assert.That(director.Settings.MusicVolume, Is.EqualTo(music - 1));
            director.Hud.Page.Back();
            Assert.That(director.Screen, Is.EqualTo(GameScreen.Paused), "back returns to the pause menu");
            yield return null;
        }
    }
}
