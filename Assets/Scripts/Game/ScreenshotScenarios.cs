using System;
using System.Collections;
using System.IO;
using ShapeShifter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShapeShifter.Game
{
    /// <summary>
    /// Opt-in capture of README screenshots: <c>-screenshot &lt;folder&gt; -scenario title,golem,...</c>.
    /// Without the -screenshot argument nothing is created, so normal runs and WebGL builds are
    /// unaffected. Inside the editor (ProjectBuilder.CaptureScreenshots) the camera and the HUD are
    /// rendered off screen at 1280x720 and blended, for machines that cannot run the player.
    /// Adding <c>-frames N [-fps F]</c> records N frames per scenario at a fixed timestep instead of
    /// one shot, into <c>&lt;folder&gt;/&lt;scenario&gt;/frame_0000.png...</c>; the <c>gameplay</c>
    /// scenario lets the autopilot run the track while it records (for the README GIF).
    /// </summary>
    public sealed class ScreenshotScenarios : MonoBehaviour
    {
        public const string AllScenarios = "title,golem,bird,fish,fox,characters,pause,nameentry,leaderboard,gameover";
        const int Width = 1280;
        const int Height = 720;
        const int Seed = 2021;

        string folder;
        string[] scenarios;
        int frames;
        int fps = 15;
        RenderTexture sceneTarget;
        RenderTexture uiTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            string target = Argument("-screenshot");
            if (string.IsNullOrEmpty(target)) return;
            var host = new GameObject(nameof(ScreenshotScenarios)).AddComponent<ScreenshotScenarios>();
            host.folder = target;
            host.scenarios = (Argument("-scenario") ?? AllScenarios).Split(',');
            host.frames = int.TryParse(Argument("-frames"), out int frames) ? frames : 0;
            host.fps = int.TryParse(Argument("-fps"), out int fps) && fps > 0 ? fps : 15;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            var director = FindAnyObjectByType<GameDirector>();
            director.ScriptedInput = true;
            if (Application.isEditor) RenderOffscreen(director);
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (string raw in scenarios)
            {
                string scenario = raw.Trim();
                UnityEngine.Random.InitState(scenario.GetHashCode());
                Prepare(director);
                yield return Stage(director, scenario);
                if (frames > 0)
                {
                    yield return Record(director, scenario);
                    continue;
                }
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(folder, scenario + ".png"), shot.EncodeToPNG());
                Destroy(shot);
                Debug.Log($"Saved screenshot {scenario}.png");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit();
#endif
        }

        /// <summary>A believable save: some coins, a bought skin and a few rivals on the board.</summary>
        static void Prepare(GameDirector director)
        {
            director.UseMemoryStore();
            director.AutoPlay = false;
            director.Hold = false;
            director.NextSeed = Seed;
            director.Wallet.Deposit(560);
            director.Wallet.Buy("moss");
            director.Wallet.Select("moss");
            string[] names = { "TRY", "FOX", "OWL", "ROK", "EEL" };
            int[] scores = { 9840, 7315, 5120, 3388, 1904 };
            for (int i = 0; i < names.Length; i++) director.Leaderboard.Submit(names[i], scores[i], scores[i] / 4);
        }

        static IEnumerator Stage(GameDirector director, string scenario)
        {
            switch (scenario)
            {
                case "title":
                    director.ToTitle();
                    director.AdvanceUntil(r => r.X > 150f, 60f);
                    yield return Wait(1.2f);
                    break;
                case "golem":
                    yield return Reach(director, ObstacleKind.Wall, 0.3f, 0.12f);
                    break;
                case "bird":
                    yield return Reach(director, ObstacleKind.Chasm, 0.45f, 0.2f);
                    break;
                case "fish":
                    yield return Reach(director, ObstacleKind.Flood, 0.35f, 0.2f);
                    break;
                case "fox":
                    yield return Reach(director, ObstacleKind.Thicket, 0.45f, 0.2f);
                    break;
                case "characters":
                    director.ToTitle();
                    director.AdvanceUntil(r => r.X > 60f, 60f);
                    director.ShowCharacters(2);
                    yield return Wait(0.6f);
                    break;
                case "gameplay":
                    director.StartRun();
                    director.AutoPlay = true;
                    // Past the first 400 m, where the lesson banners stop and the track has sped up.
                    director.AdvanceUntil(r => r.Distance > 430f, 120f);
                    director.Hold = true;
                    director.Hud.HideBanner();
                    yield return Wait(0.3f);
                    break;
                case "pause":
                    director.StartRun();
                    director.AutoPlay = true;
                    director.AdvanceUntil(r => r.X > 420f, 120f);
                    yield return Wait(0.3f);
                    director.TogglePause();
                    yield return Wait(0.4f);
                    break;
                case "nameentry":
                    yield return EndRun(director, 900f);
                    director.TypeLetter('S');
                    director.TypeLetter('A');
                    director.TypeLetter('M');
                    yield return Wait(0.3f);
                    break;
                case "leaderboard":
                    yield return EndRun(director, 900f);
                    director.TypeLetter('S');
                    director.TypeLetter('A');
                    director.TypeLetter('M');
                    director.SubmitName();
                    yield return Wait(0.4f);
                    break;
                case "gameover":
                    yield return EndRun(director, 180f);
                    // Enter a name, then go back from the board to the results card.
                    if (director.Screen == GameScreen.NameEntry) director.SubmitName();
                    director.Hud.Page.Back?.Invoke();
                    yield return Wait(0.4f);
                    break;
                default:
                    Debug.LogWarning($"Unknown screenshot scenario '{scenario}'.");
                    break;
            }
        }

        /// <summary>
        /// Runs the seeded track with the autopilot until the runner is a given share into the first
        /// obstacle of a kind, then holds the simulation while effects play for a moment.
        /// </summary>
        static IEnumerator Reach(GameDirector director, ObstacleKind kind, float into, float settle)
        {
            director.StartRun();
            director.AutoPlay = true;
            director.Run.Track.EnsureUntil(600f);
            Obstacle target = null;
            foreach (Obstacle o in director.Run.Track.Obstacles)
            {
                if (o.Kind == kind)
                {
                    target = o;
                    break;
                }
            }
            float x = target.Start + target.Length * into;
            director.AdvanceUntil(r => r.X >= x, 120f);
            director.Hold = true;
            director.Hud.HideBanner();
            yield return Wait(settle);
        }

        /// <summary>
        /// Writes the given number of frames. A held run (gameplay) is stepped by hand, a fixed
        /// number of simulation steps per frame, so the recording keeps real speed however slowly
        /// the frames are written.
        /// </summary>
        IEnumerator Record(GameDirector director, string scenario)
        {
            string target = Path.Combine(folder, scenario);
            Directory.CreateDirectory(target);
            int steps = Mathf.Max(1, Mathf.RoundToInt(1f / (fps * GameDirector.StepTime)));
            Time.captureFramerate = fps;
            for (int i = 0; i < frames; i++)
            {
                if (director.Hold && director.Run != null && !director.Run.Over)
                {
                    for (int s = 0; s < steps; s++) director.StepOnce();
                }
                Texture2D shot;
                if (Application.isEditor)
                {
                    yield return null;
                    shot = CaptureOffscreen();
                }
                else
                {
                    yield return new WaitForEndOfFrame();
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                }
                File.WriteAllBytes(Path.Combine(target, $"frame_{i:0000}.png"), shot.EncodeToPNG());
                Destroy(shot);
            }
            Time.captureFramerate = 0;
            Debug.Log($"Saved {frames} frames of {scenario}");
        }

        static IEnumerator EndRun(GameDirector director, float distance)
        {
            director.StartRun();
            director.AutoPlay = true;
            director.AdvanceUntil(r => r.Distance >= distance, 300f);
            director.AutoPlay = false;
            director.EndRunNow();
            yield return Wait(1.6f);
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void RenderOffscreen(GameDirector director)
        {
            sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Camera.main.targetTexture = sceneTarget;
            var document = director.Hud.GetComponent<UIDocument>();
            PanelSettings panel = Instantiate(document.panelSettings);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;
            document.panelSettings = panel;
        }

        Texture2D CaptureOffscreen()
        {
            Color[] scene = Read(sceneTarget);
            Color[] ui = Read(uiTarget);
            for (int i = 0; i < scene.Length; i++)
            {
                Color over = ui[i];
                Color result = Color.Lerp(scene[i], new Color(over.r, over.g, over.b), over.a);
                result.a = 1f;
                scene[i] = result;
            }
            var shot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            shot.SetPixels(scene);
            shot.Apply();
            return shot;
        }

        static Color[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Color[] pixels = texture.GetPixels();
            Destroy(texture);
            return pixels;
        }

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
