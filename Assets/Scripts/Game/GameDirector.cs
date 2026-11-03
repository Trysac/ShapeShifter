using System;
using System.Collections.Generic;
using ShapeShifter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ShapeShifter.Game
{
    public enum GameScreen
    {
        Title,
        Characters,
        Leaderboard,
        Settings,
        Controls,
        Playing,
        Paused,
        NameEntry,
        GameOver,
    }

    /// <summary>
    /// Runs the game: the menus (over a live demo run played by the <see cref="Autopilot"/>),
    /// and the fixed-step simulation of the player's <see cref="Run"/>, turning its events into
    /// sound, particles, shake and HUD text. Coins are banked and the leaderboard offered when a
    /// run ends.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public const float StepTime = 1f / 60f;
        public const string LastNameKey = "ShapeShifter.LastName";
        const float GameOverDelay = 1.3f;
        const float DemoLength = 2500f;

        static readonly Color[] FormTints =
        {
            new Color(1f, 0.6f, 0.3f), new Color(0.45f, 0.75f, 1f), new Color(0.85f, 0.8f, 0.7f), new Color(0.4f, 0.9f, 0.8f),
        };

        [SerializeField] GameArt art;
        [SerializeField] Hud hud;
        [SerializeField] CameraRig cameraRig;

        GameInput input;
        WorldView view;
        Effects effects;
        AudioDirector audioDirector;
        IKeyValueStore store;
        Wallet wallet;
        Leaderboard board;
        Settings settings;
        float accumulator;
        Form? pendingShift;
        bool pendingJump;
        float overTimer;
        bool settingsFromPause;
        int characterIndex;
        readonly char[] letters = new char[Leaderboard.NameLength];
        int letterSlot;
        int typedFrame = -1;
        int lastRank = -1;
        bool newBest;
        readonly HashSet<int> lessonsShown = new HashSet<int>();

        public GameScreen Screen { get; private set; }
        /// <summary>The player's run, or the demo run behind the menus.</summary>
        public Run Run { get; private set; }
        public bool Demo { get; private set; }
        public Wallet Wallet => wallet;
        public Leaderboard Leaderboard => board;
        public Settings Settings => settings;
        public Hud Hud => hud;
        public WorldView View => view;
        /// <summary>For tests and the screenshot tool: ignore devices.</summary>
        public bool ScriptedInput { get; set; }
        /// <summary>Lets the autopilot play the player's run (screenshots, tests).</summary>
        public bool AutoPlay { get; set; }
        /// <summary>Freezes the simulation while the view keeps animating (screenshots).</summary>
        public bool Hold { get; set; }
        /// <summary>The seed of the next run; a fresh one each time when null.</summary>
        public int? NextSeed { get; set; }
        public int LastRank => lastRank;
        public string EnteredName => new string(letters);

        void Awake()
        {
            input = new GameInput();
            view = new GameObject("World View").AddComponent<WorldView>();
            view.Init(art);
            effects = new GameObject("Effects").AddComponent<Effects>();
            effects.Init(art);
            audioDirector = gameObject.AddComponent<AudioDirector>();
            audioDirector.Init();
            hud.Build(art);
            hud.ItemClicked += Activate;
            hud.FormClicked += form =>
            {
                if (Screen == GameScreen.Playing) pendingShift = form;
            };
            hud.JumpClicked += () =>
            {
                if (Screen == GameScreen.Playing) pendingJump = true;
            };
            hud.PauseClicked += () =>
            {
                if (Screen == GameScreen.Playing) TogglePause();
            };
            UseStore(new PlayerPrefsStore());
        }

        void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnText;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnText;
        }

        void Start() => ToTitle();

        /// <summary>Keeps progress in memory only (tests, screenshots).</summary>
        public void UseMemoryStore() => UseStore(new MemoryStore());

        void UseStore(IKeyValueStore newStore)
        {
            store = newStore;
            wallet = new Wallet(store);
            board = new Leaderboard(store);
            settings = new Settings(store);
            ApplySettings();
        }

        void ApplySettings()
        {
            audioDirector.SetVolumes(settings.MusicGain, settings.EffectsGain);
            cameraRig.ShakeEnabled = settings.ScreenShake;
            hud.SetTouch(Settings.ShowTouch(settings.TouchButtons, GameInput.HasTouchScreen));
        }

        // ---- Flow ----

        public void ToTitle()
        {
            StartDemo();
            audioDirector.SetDucked(false);
            audioDirector.PlayMusic(art.TitleMusic);
            ShowTitleMenu();
        }

        void StartDemo()
        {
            Demo = true;
            Hold = false;
            Run = new Run(7);
            BindView();
            hud.ShowPlay(false);
            hud.HideBanner();
        }

        void BindView()
        {
            accumulator = 0f;
            pendingShift = null;
            pendingJump = false;
            effects.Clear();
            hud.ClearFloaters();
            view.Show(Run, wallet.Selected);
            cameraRig.Follow(Run.X);
        }

        public void StartRun()
        {
            Demo = false;
            Hold = false;
            int seed = NextSeed ?? Environment.TickCount;
            Run = new Run(seed);
            BindView();
            lessonsShown.Clear();
            overTimer = 0f;
            lastRank = -1;
            hud.HideMenu();
            hud.HideBanner();
            hud.ShowPlay(true);
            hud.UpdatePlay(Run);
            audioDirector.SetDucked(false);
            audioDirector.PlayMusic(art.RunMusic);
            audioDirector.Play(art.Confirm);
            Screen = GameScreen.Playing;
        }

        public void TogglePause()
        {
            if (Screen == GameScreen.Playing && !Run.Over)
            {
                Screen = GameScreen.Paused;
                hud.HideBanner();
                audioDirector.SetDucked(true);
                ShowPauseMenu();
            }
            else if (Screen == GameScreen.Paused)
            {
                hud.HideMenu();
                audioDirector.SetDucked(false);
                Screen = GameScreen.Playing;
            }
        }

        /// <summary>Ends the player's run now (the pause menu's "End run", tests).</summary>
        public void EndRunNow()
        {
            if (Demo || Run == null || Run.Over) return;
            Run.EndNow();
            hud.HideMenu();
            audioDirector.SetDucked(false);
            Screen = GameScreen.Playing;
            HandleEvents();
        }

        void FinishRun()
        {
            wallet.Deposit(Run.Score.Coins);
            newBest = board.Entries.Count == 0 || Run.Score.Total > board.Entries[0].Score;
            if (board.Qualifies(Run.Score.Total)) ShowNameEntry();
            else ShowResults();
        }

        // ---- Update ----

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (Screen == GameScreen.Playing && !ScriptedInput) ReadPlayInput();
            else if (hud.MenuOpen && !ScriptedInput) MenuInput();

            bool simulate = Run != null && !Hold && (Demo || Screen == GameScreen.Playing);
            if (simulate)
            {
                accumulator += dt;
                while (accumulator >= StepTime)
                {
                    accumulator -= StepTime;
                    StepOnce();
                }
            }
            if (Run != null && Run.Over && !Demo && Screen == GameScreen.Playing)
            {
                overTimer += dt;
                if (overTimer >= GameOverDelay) FinishRun();
            }
            if (Demo && Run != null && Run.Distance > DemoLength) StartDemo();

            if (Run != null) cameraRig.Follow(Run.X);
            view.Tick(cameraRig.FocusX, dt);
            if (!Demo && Run != null) hud.UpdatePlay(Run);
            hud.SetPrompts(input.UsingGamepad);
            hud.Tick(dt, Camera.main);
        }

        void ReadPlayInput()
        {
            if (input.Pause)
            {
                TogglePause();
                return;
            }
            Form? shift = input.ShiftPressed();
            if (shift.HasValue) pendingShift = shift;
            if (input.JumpPressed()) pendingJump = true;
        }

        /// <summary>One fixed step of the current run, with its events shown and heard.</summary>
        public void StepOnce()
        {
            if (Run == null) return;
            RunInput step = Demo || AutoPlay ? Autopilot.Decide(Run) : new RunInput(pendingShift, pendingJump);
            pendingShift = null;
            pendingJump = false;
            Run.Step(StepTime, step);
            HandleEvents();
            if (!Demo) Lessons();
        }

        /// <summary>Steps the simulation (without waiting for frames) until the condition holds or time runs out.</summary>
        public void AdvanceUntil(Func<Run, bool> done, float maxSeconds)
        {
            for (float t = 0f; t < maxSeconds && Run != null && !done(Run) && !Run.Over; t += StepTime) StepOnce();
        }

        /// <summary>Asks for a shift or a jump as the player would (tests, screenshots).</summary>
        public void Press(Form? shift, bool jump)
        {
            if (shift.HasValue) pendingShift = shift;
            if (jump) pendingJump = true;
        }

        void HandleEvents()
        {
            foreach (RunEvent e in Run.Events)
            {
                view.OnEvent(e);
                var at = new Vector2(e.X, e.Y);
                bool loud = !Demo;
                switch (e.Kind)
                {
                    case RunEventKind.Shifted:
                        effects.Ring(at + new Vector2(0f, 1f), FormTints[(int)e.Form]);
                        effects.Dust(at, 2);
                        if (loud) audioDirector.Play(art.ShiftSound(e.Form), 0.8f, 0f);
                        break;
                    case RunEventKind.ShiftBlocked:
                        if (loud) audioDirector.Play(art.Blocked, 0.7f);
                        break;
                    case RunEventKind.Jumped:
                        effects.Dust(at, 2);
                        if (loud) audioDirector.Play(e.Form == Form.Bird ? art.Flap : art.Jump, 0.8f);
                        break;
                    case RunEventKind.Landed:
                        effects.Dust(at, 2);
                        if (loud) audioDirector.Play(art.Land, 0.5f);
                        break;
                    case RunEventKind.Coin:
                        effects.Sparkle(at, new Color(1f, 0.9f, 0.4f));
                        if (loud) audioDirector.Play(art.CoinPickup, 0.6f, 0.02f);
                        break;
                    case RunEventKind.Heart:
                        effects.Sparkle(at, new Color(1f, 0.5f, 0.5f));
                        if (loud)
                        {
                            audioDirector.Play(art.HeartPickup);
                            hud.Float(at, e.Points > 0 ? "+" + e.Points : "+1 HEART", "floater--bonus");
                        }
                        break;
                    case RunEventKind.Hurt:
                        cameraRig.Shake(0.55f);
                        if (loud)
                        {
                            audioDirector.Play(art.Hurt);
                            hud.Hurt();
                            Form? need = FormRules.RequiredForm(e.Obstacle);
                            hud.Float(at + new Vector2(0f, 2f), need.HasValue ? need.Value.ToString().ToUpperInvariant() + " NEEDED" : "JUMP IT", "floater--hurt");
                        }
                        break;
                    case RunEventKind.Fell:
                        cameraRig.Shake(0.4f);
                        if (loud) audioDirector.Play(art.Fall);
                        break;
                    case RunEventKind.Smashed:
                        effects.Rocks(at + new Vector2(0.6f, 0f), e.Obstacle == ObstacleKind.Wall ? 10 : 5);
                        effects.Dust(at, 4);
                        cameraRig.Shake(e.Obstacle == ObstacleKind.Wall ? 0.4f : 0.2f);
                        if (loud) audioDirector.Play(art.Smash);
                        break;
                    case RunEventKind.Passed:
                        if (e.Obstacle == ObstacleKind.Flood) effects.Splash(at);
                        if (e.Obstacle == ObstacleKind.Thicket) effects.Leaves(at, 6);
                        if (loud)
                        {
                            audioDirector.Play(e.Obstacle == ObstacleKind.Flood ? art.Splash : e.Obstacle == ObstacleKind.Thicket ? art.Rustle : art.Pass, 0.6f);
                            hud.Float(at + new Vector2(0f, 2.2f), "+" + e.Points, "floater--bonus");
                            int streak = Run.Score.Streak;
                            if (streak % Tuning.StreakPerMultiplier == 0 && streak / Tuning.StreakPerMultiplier < Tuning.MaxMultiplier)
                            {
                                audioDirector.Play(art.Streak, 0.8f, 0f);
                                hud.Float(at + new Vector2(1.5f, 3.2f), "x" + Run.Score.Multiplier, "floater--bonus");
                            }
                        }
                        break;
                    case RunEventKind.GameOver:
                        cameraRig.Shake(0.8f);
                        if (loud)
                        {
                            audioDirector.PlayMusic(null);
                            audioDirector.Play(art.GameOver);
                            hud.Banner("GAME OVER", $"{Run.Score.Total:N0} points  -  {(int)Run.Distance} m");
                        }
                        break;
                }
            }
        }

        static readonly string[] LessonText =
        {
            "Only the GOLEM smashes walls", "Only the BIRD crosses chasms", "Only the FISH swims floods", "Only the FOX slips through thickets",
        };

        /// <summary>The first obstacle of each kind gets a banner that names its form and key.</summary>
        void Lessons()
        {
            Obstacle next = Run.NextObstacle;
            if (next == null || !next.RequiredForm.HasValue || Run.Distance > 400f) return;
            int index = -1;
            ObstacleKind[] order = { ObstacleKind.Wall, ObstacleKind.Chasm, ObstacleKind.Flood, ObstacleKind.Thicket };
            for (int i = 0; i < order.Length; i++) if (order[i] == next.Kind) index = i;
            if (index < 0 || lessonsShown.Contains(index)) return;
            if ((next.Start - Run.X) / Run.Speed > Tuning.TelegraphSeconds + 0.4f) return;
            lessonsShown.Add(index);
            Form form = next.RequiredForm.Value;
            hud.Banner(LessonText[index], "Press " + KeyFor(form) + " to become the " + form.ToString().ToUpperInvariant(), 2.2f);
        }

        string KeyFor(Form form)
        {
            if (input.UsingGamepad) return new[] { "A", "Y", "B", "X" }[(int)form];
            if (Settings.ShowTouch(settings.TouchButtons, GameInput.HasTouchScreen)) return "its button";
            return new[] { "1 or K", "2 or I", "3 or L", "4 or J" }[(int)form];
        }

        // ---- Menus ----

        void MenuInput()
        {
            if (Screen == GameScreen.NameEntry)
            {
                NameEntryInput();
                return;
            }
            if (Screen == GameScreen.Paused && input.Pause && (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame))
            {
                TogglePause();
                return;
            }
            int v = input.Vertical();
            if (v != 0)
            {
                hud.Move(v);
                audioDirector.Play(art.Click, 0.5f);
            }
            int h = input.Horizontal();
            if (h != 0 && hud.Page.Items.Count > 0)
            {
                MenuItem item = hud.Page.Items[hud.Selected];
                if (item.Adjust != null)
                {
                    item.Adjust(h);
                    hud.Refresh();
                    audioDirector.Play(art.Click, 0.5f);
                }
            }
            if (input.Confirm) Activate(hud.Selected);
            else if (input.Back && hud.Page?.Back != null)
            {
                audioDirector.Play(art.Click, 0.5f);
                hud.Page.Back();
            }
        }

        void Activate(int index)
        {
            if (!hud.MenuOpen || index < 0 || index >= hud.Page.Items.Count) return;
            MenuItem item = hud.Page.Items[index];
            if (!item.Enabled) return;
            audioDirector.Play(art.Confirm, 0.6f);
            item.Activate?.Invoke();
        }

        string Footer() =>
            input.UsingGamepad ? "D-pad: choose   A: select   B: back" : "Arrows: choose   Enter: select   Esc: back   (or use the mouse)";

        void ShowTitleMenu(int selected = 0)
        {
            Screen = GameScreen.Title;
            var page = new MenuPage { Logo = true, Footer = Footer() };
            string best = board.Entries.Count > 0 ? $"Best run {board.Entries[0].Score:N0} by {board.Entries[0].Name}" : "Shift shape to pass what lies ahead";
            page.Body = best + $"      Coins {wallet.Coins:N0}";
            page.Items.Add(new MenuItem { Label = "Play", Activate = StartRun });
            page.Items.Add(new MenuItem { Label = "Characters", Activate = () => ShowCharacters(Skins.IndexOf(wallet.Selected)) });
            page.Items.Add(new MenuItem { Label = "Leaderboard", Activate = () => ShowLeaderboard(GameScreen.Title) });
            page.Items.Add(new MenuItem { Label = "Settings", Activate = () => ShowSettings(false) });
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                page.Items.Add(new MenuItem { Label = "Exit", Activate = Application.Quit });
            }
            hud.ShowMenu(page, selected);
        }

        void ShowPauseMenu(int selected = 0)
        {
            Screen = GameScreen.Paused;
            var page = new MenuPage
            {
                Title = "PAUSED",
                Body = $"{Run.Score.Total:N0} points   {(int)Run.Distance} m   {Run.Score.Coins} coins",
                Footer = Footer(),
                Back = TogglePause,
            };
            page.Items.Add(new MenuItem { Label = "Resume", Activate = TogglePause });
            page.Items.Add(new MenuItem { Label = "Settings", Activate = () => ShowSettings(true) });
            page.Items.Add(new MenuItem { Label = "End run", Activate = EndRunNow });
            page.Items.Add(new MenuItem { Label = "Quit to title", Activate = ToTitle });
            hud.ShowMenu(page, selected);
        }

        public void ShowCharacters(int index)
        {
            int keep = Screen == GameScreen.Characters && hud.MenuOpen ? hud.Selected : 0;
            Screen = GameScreen.Characters;
            characterIndex = Mathf.Clamp(index, 0, Skins.All.Count - 1);
            Skin skin = Skins.All[characterIndex];
            var page = new MenuPage
            {
                Title = "CHARACTERS",
                Subtitle = $"Coins in the bank: {wallet.Coins:N0}",
                Footer = Footer(),
                Back = () => ShowTitleMenu(1),
            };
            page.Extra = area =>
            {
                VisualElement cards = Hud.Box(area, "cards");
                for (int i = 0; i < Skins.All.Count; i++)
                {
                    int captured = i;
                    Skin s = Skins.All[i];
                    bool owned = wallet.Owns(s.Id);
                    VisualElement card = Hud.Box(cards, "card" + (i == characterIndex ? " card--selected" : "") + (owned ? "" : " card--locked"));
                    card.pickingMode = PickingMode.Position;
                    card.RegisterCallback<ClickEvent>(_ => ShowCharacters(captured));
                    Hud.Image(card, "card__preview", art.RunnerSprite(s.Id, Form.Fox, 0));
                    Hud.Text(card, "card__name", s.Name);
                    string price = wallet.Selected == s.Id ? "WEARING" : owned ? "OWNED" : s.Price + " coins";
                    Hud.Text(card, "card__price" + (wallet.Selected == s.Id ? " card__price--worn" : owned ? " card__price--owned" : ""), price);
                }
                VisualElement forms = Hud.Box(area, "forms-preview");
                foreach (Form form in FormRules.All) Hud.Image(forms, "forms-preview__frame", art.RunnerSprite(skin.Id, form, 0));
                Hud.Text(area, "menu__body", skin.Blurb);
            };
            string action;
            bool owns = wallet.Owns(skin.Id);
            if (wallet.Selected == skin.Id) action = $"Wearing {skin.Name}";
            else if (owns) action = $"Wear {skin.Name}";
            else action = wallet.Coins >= skin.Price ? $"Buy {skin.Name} for {skin.Price}" : $"{skin.Name}: {skin.Price - wallet.Coins} more coins";
            page.Items.Add(new MenuItem { Label = action, Activate = ChooseSkin, Adjust = step => ShowCharacters((characterIndex + step + Skins.All.Count) % Skins.All.Count) });
            page.Items.Add(new MenuItem { Label = "Back", Activate = () => ShowTitleMenu(1) });
            hud.ShowMenu(page, keep);
        }

        void ChooseSkin()
        {
            Skin skin = Skins.All[characterIndex];
            if (!wallet.Owns(skin.Id))
            {
                if (wallet.Buy(skin.Id) != PurchaseResult.Bought)
                {
                    audioDirector.Play(art.Blocked);
                    return;
                }
                audioDirector.Play(art.Buy);
            }
            wallet.Select(skin.Id);
            view.SetSkin(skin.Id);
            ShowCharacters(characterIndex);
        }

        public void ShowLeaderboard(GameScreen returnTo)
        {
            Screen = GameScreen.Leaderboard;
            Action back = returnTo == GameScreen.GameOver ? (Action)(() => ShowResults()) : () => ShowTitleMenu(2);
            var page = new MenuPage { Title = "LEADERBOARD", Subtitle = "The ten best runs on this device", Footer = Footer(), Back = back };
            page.Extra = area =>
            {
                VisualElement table = Hud.Box(area, "board");
                Row(table, "board__row board__row--head", "#", "NAME", "SCORE", "DISTANCE");
                if (board.Entries.Count == 0) Hud.Text(table, "menu__body", "No runs yet. Be the first!");
                for (int i = 0; i < board.Entries.Count; i++)
                {
                    LeaderboardEntry e = board.Entries[i];
                    Row(table, "board__row" + (i == lastRank && returnTo == GameScreen.GameOver ? " board__row--mine" : ""),
                        (i + 1).ToString(), e.Name, e.Score.ToString("N0"), e.Distance + " m");
                }
            };
            page.Items.Add(new MenuItem { Label = "Back", Activate = back });
            hud.ShowMenu(page);
        }

        static void Row(VisualElement table, string classes, string rank, string name, string score, string distance)
        {
            VisualElement row = Hud.Box(table, classes);
            Hud.Text(row, "board__rank", rank);
            Hud.Text(row, "board__name", name);
            Hud.Text(row, "board__score", score);
            Hud.Text(row, "board__distance", distance);
        }

        void ShowSettings(bool fromPause, int selected = 0)
        {
            settingsFromPause = fromPause;
            Screen = GameScreen.Settings;
            Action back = () =>
            {
                if (settingsFromPause) ShowPauseMenu(1);
                else ShowTitleMenu(3);
            };
            var page = new MenuPage { Title = "SETTINGS", Footer = Footer(), Back = back };
            page.Items.Add(new MenuItem
            {
                Label = "Music", Value = () => Bar(settings.MusicVolume),
                Adjust = d => { settings.StepMusic(d); ApplySettings(); },
                Activate = () => { settings.StepMusic(settings.MusicVolume >= Settings.MaxVolume ? -Settings.MaxVolume : 1); ApplySettings(); hud.Refresh(); },
            });
            page.Items.Add(new MenuItem
            {
                Label = "Effects", Value = () => Bar(settings.EffectsVolume),
                Adjust = d => { settings.StepEffects(d); ApplySettings(); audioDirector.Play(art.CoinPickup); },
                Activate = () => { settings.StepEffects(settings.EffectsVolume >= Settings.MaxVolume ? -Settings.MaxVolume : 1); ApplySettings(); hud.Refresh(); },
            });
            page.Items.Add(new MenuItem
            {
                Label = "Screen shake", Value = () => settings.ScreenShake ? "On" : "Off",
                Adjust = _ => { settings.ToggleShake(); ApplySettings(); },
                Activate = () => { settings.ToggleShake(); ApplySettings(); hud.Refresh(); },
            });
            page.Items.Add(new MenuItem
            {
                Label = "Touch buttons", Value = () => settings.TouchButtons.ToString(),
                Adjust = d => { settings.StepTouchButtons(d); ApplySettings(); },
                Activate = () => { settings.StepTouchButtons(1); ApplySettings(); hud.Refresh(); },
            });
            page.Items.Add(new MenuItem { Label = "Controls", Activate = ShowControls });
            page.Items.Add(new MenuItem { Label = "Back", Activate = back });
            hud.ShowMenu(page, selected);
        }

        static string Bar(int volume) => new string('|', volume) + new string('.', Settings.MaxVolume - volume);

        void ShowControls()
        {
            Screen = GameScreen.Controls;
            var page = new MenuPage
            {
                Title = "CONTROLS",
                Body =
                    "FOX: 1 or K  -  pad A  -  slips through thickets\n" +
                    "BIRD: 2 or I  -  pad Y  -  glides over chasms\n" +
                    "GOLEM: 3 or L  -  pad B  -  smashes walls and boulders\n" +
                    "FISH: 4 or J  -  pad X  -  swims flooded tunnels\n" +
                    "JUMP over boulders: Space, W or Up  -  pad bumpers or triggers\n" +
                    "PAUSE: Esc or P  -  pad Start\n" +
                    "Mouse and touch: tap a form at the bottom, tap the screen to jump.",
                Footer = Footer(),
                Back = () => ShowSettings(settingsFromPause, 4),
            };
            page.Items.Add(new MenuItem { Label = "Back", Activate = () => ShowSettings(settingsFromPause, 4) });
            hud.ShowMenu(page);
        }

        // ---- Game over, name entry, results ----

        void ShowNameEntry()
        {
            Screen = GameScreen.NameEntry;
            hud.ShowPlay(false);
            hud.HideBanner();
            string last = Leaderboard.CleanName(store.GetString(LastNameKey, "AAA"));
            if (last == "???") last = "AAA";
            last = last.PadRight(Leaderboard.NameLength, 'A');
            for (int i = 0; i < letters.Length; i++) letters[i] = last[i];
            letterSlot = 0;
            audioDirector.Play(art.HighScore);
            var page = new MenuPage
            {
                Title = newBest ? "NEW BEST RUN!" : "A PLACE ON THE BOARD",
                Body = $"{Run.Score.Total:N0} points   {(int)Run.Distance} m   {Run.Score.Coins} coins banked",
                Subtitle = "Enter your initials",
                Footer = input.UsingGamepad ? "D-pad: letter and slot   A: next   Start: done" : "Type, or use the arrows; Enter when done",
            };
            page.Extra = area =>
            {
                VisualElement row = Hud.Box(area, "letters");
                for (int i = 0; i < letters.Length; i++)
                {
                    int slot = i;
                    VisualElement cell = Hud.Box(row, "letter" + (i == letterSlot ? " letter--active" : ""));
                    Label up = Hud.Text(cell, "letter__arrow", "+");
                    up.pickingMode = PickingMode.Position;
                    up.RegisterCallback<ClickEvent>(_ => { letterSlot = slot; StepLetter(1); });
                    Label ch = Hud.Text(cell, "letter__char", letters[i].ToString());
                    ch.pickingMode = PickingMode.Position;
                    ch.RegisterCallback<ClickEvent>(_ => { letterSlot = slot; hud.RefreshExtra(); });
                    Label down = Hud.Text(cell, "letter__arrow", "-");
                    down.pickingMode = PickingMode.Position;
                    down.RegisterCallback<ClickEvent>(_ => { letterSlot = slot; StepLetter(-1); });
                }
            };
            page.Items.Add(new MenuItem { Label = "Done", Activate = SubmitName });
            hud.ShowMenu(page);
        }

        void NameEntryInput()
        {
            if (typedFrame == Time.frameCount) return;
            Keyboard k = Keyboard.current;
            Gamepad p = Gamepad.current;
            int h = 0, v = 0;
            if (k != null)
            {
                if (k.leftArrowKey.wasPressedThisFrame) h = -1;
                if (k.rightArrowKey.wasPressedThisFrame) h = 1;
                if (k.upArrowKey.wasPressedThisFrame) v = 1;
                if (k.downArrowKey.wasPressedThisFrame) v = -1;
                if (k.backspaceKey.wasPressedThisFrame) h = -1;
                if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
                {
                    SubmitName();
                    return;
                }
            }
            if (p != null)
            {
                if (p.dpad.left.wasPressedThisFrame) h = -1;
                if (p.dpad.right.wasPressedThisFrame || p.buttonSouth.wasPressedThisFrame) h = 1;
                if (p.dpad.up.wasPressedThisFrame) v = 1;
                if (p.dpad.down.wasPressedThisFrame) v = -1;
                if (p.buttonEast.wasPressedThisFrame) h = -1;
                if (p.startButton.wasPressedThisFrame || (p.buttonSouth.wasPressedThisFrame && letterSlot == letters.Length - 1))
                {
                    SubmitName();
                    return;
                }
            }
            if (v != 0) StepLetter(v);
            if (h != 0)
            {
                letterSlot = Mathf.Clamp(letterSlot + h, 0, letters.Length - 1);
                audioDirector.Play(art.Click, 0.5f);
                hud.RefreshExtra();
            }
        }

        void StepLetter(int step)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            int i = alphabet.IndexOf(letters[letterSlot]);
            letters[letterSlot] = alphabet[(i + step + alphabet.Length) % alphabet.Length];
            audioDirector.Play(art.Click, 0.5f);
            hud.RefreshExtra();
        }

        void OnText(char c)
        {
            if (Screen != GameScreen.NameEntry || ScriptedInput) return;
            typedFrame = Time.frameCount;
            TypeLetter(c);
        }

        /// <summary>Types one character into the current slot and moves on (keyboard, tests).</summary>
        public void TypeLetter(char c)
        {
            if (Screen != GameScreen.NameEntry) return;
            char upper = char.ToUpperInvariant(c);
            if (!((upper >= 'A' && upper <= 'Z') || (upper >= '0' && upper <= '9'))) return;
            letters[letterSlot] = upper;
            letterSlot = Mathf.Min(letterSlot + 1, letters.Length - 1);
            audioDirector.Play(art.Click, 0.5f);
            hud.RefreshExtra();
        }

        public void SubmitName()
        {
            if (Screen != GameScreen.NameEntry) return;
            string name = new string(letters);
            store.SetString(LastNameKey, name);
            lastRank = board.Submit(name, Run.Score.Total, (int)Run.Distance);
            audioDirector.Play(art.Confirm);
            ShowLeaderboard(GameScreen.GameOver);
        }

        void ShowResults()
        {
            Screen = GameScreen.GameOver;
            hud.ShowPlay(false);
            hud.HideBanner();
            Score s = Run.Score;
            var page = new MenuPage
            {
                Title = "GAME OVER",
                Subtitle = lastRank >= 0 ? $"#{lastRank + 1} on the leaderboard" : board.Entries.Count > 0 ? $"Best run: {board.Entries[0].Score:N0}" : null,
                Body = $"Score {s.Total:N0}\nDistance {(int)s.Distance} m   Coins +{s.Coins} (bank {wallet.Coins:N0})   Best streak {s.BestStreak}",
                Footer = Footer(),
                Back = ToTitle,
            };
            page.Items.Add(new MenuItem { Label = "Run again", Activate = StartRun });
            page.Items.Add(new MenuItem { Label = "Leaderboard", Activate = () => ShowLeaderboard(GameScreen.GameOver) });
            page.Items.Add(new MenuItem { Label = "Characters", Activate = () => ShowCharacters(Skins.IndexOf(wallet.Selected)) });
            page.Items.Add(new MenuItem { Label = "Title", Activate = ToTitle });
            hud.ShowMenu(page);
        }
    }
}
