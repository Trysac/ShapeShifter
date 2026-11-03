using System.Collections.Generic;
using ShapeShifter.Core;
using UnityEngine;

namespace ShapeShifter.Game
{
    /// <summary>
    /// Every texture and clip the game uses, wired up by the editor ProjectBuilder. Sheets are
    /// horizontal strips of equal frames drawn by tools/art; <see cref="Frames"/> slices them.
    /// </summary>
    public sealed class GameArt : ScriptableObject
    {
        public const float PixelsPerUnit = 16f;
        public const int RunnerFrame = 32;
        public const int FramesPerForm = 6;

        [Header("Runner sheets, one per skin in Skins.All order")]
        public Texture2D[] Runners = new Texture2D[5];

        [Header("Sheets and sprites")]
        public Texture2D Items;
        public Texture2D Signs;
        public Texture2D Boulder;
        public Texture2D Effects;
        public Texture2D Tiles;
        public Texture2D Thicket;
        public Texture2D ForestFar;
        public Texture2D ForestNear;
        public Texture2D CaveFar;
        public Texture2D CaveNear;
        public Texture2D[] Badges = new Texture2D[4];
        public Texture2D Heart;
        public Texture2D HeartEmpty;
        public Texture2D Coin;
        public Texture2D Icon;

        [Header("Music")]
        public AudioClip TitleMusic;
        public AudioClip RunMusic;

        [Header("Effects")]
        public AudioClip Jump;
        public AudioClip Flap;
        public AudioClip Land;
        public AudioClip[] Shift = new AudioClip[4];
        public AudioClip Blocked;
        public AudioClip CoinPickup;
        public AudioClip HeartPickup;
        public AudioClip Hurt;
        public AudioClip Fall;
        public AudioClip Smash;
        public AudioClip Splash;
        public AudioClip Rustle;
        public AudioClip Pass;
        public AudioClip Streak;
        public AudioClip GameOver;
        public AudioClip HighScore;
        public AudioClip Click;
        public AudioClip Confirm;
        public AudioClip Buy;

        readonly Dictionary<(Texture2D, int, int), Sprite[]> cache = new Dictionary<(Texture2D, int, int), Sprite[]>();
        readonly Dictionary<(Texture2D, int, int, int), Sprite> tiles = new Dictionary<(Texture2D, int, int, int), Sprite>();

        /// <summary>Slices a strip into frames of the given width, pivoted at the bottom centre.</summary>
        public Sprite[] Frames(Texture2D sheet, int frameWidth, int pivotBottom = 1)
        {
            var key = (sheet, frameWidth, pivotBottom);
            if (cache.TryGetValue(key, out Sprite[] sprites)) return sprites;
            int count = sheet.width / frameWidth;
            sprites = new Sprite[count];
            Vector2 pivot = pivotBottom == 1 ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            for (int i = 0; i < count; i++)
            {
                sprites[i] = Sprite.Create(sheet, new Rect(i * frameWidth, 0, frameWidth, sheet.height), pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = $"{sheet.name}_{i}";
            }
            cache[key] = sprites;
            return sprites;
        }

        /// <summary>A 16x16 tile of the tile sheet (row 0 forest, row 1 cave; rows count from the top).</summary>
        public Sprite Tile(int column, int row) => Region(Tiles, column * 16, Tiles.height - (row + 1) * 16, 16, 16);

        public Sprite Region(Texture2D texture, int x, int y, int w, int h)
        {
            var key = (texture, x, y, w * 10000 + h);
            if (tiles.TryGetValue(key, out Sprite sprite)) return sprite;
            sprite = Sprite.Create(texture, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            tiles[key] = sprite;
            return sprite;
        }

        public Sprite Whole(Texture2D texture, bool bottomPivot = false) =>
            bottomPivot ? Frames(texture, texture.width)[0] : Region(texture, 0, 0, texture.width, texture.height);

        public Texture2D RunnerSheet(string skinId)
        {
            int index = Mathf.Max(0, Skins.IndexOf(skinId));
            return Runners[Mathf.Min(index, Runners.Length - 1)];
        }

        public Sprite RunnerSprite(string skinId, Form form, int frame) =>
            Frames(RunnerSheet(skinId), RunnerFrame)[(int)form * FramesPerForm + frame];

        public Texture2D Badge(Form form) => Badges[(int)form];

        public AudioClip ShiftSound(Form form) => Shift[(int)form];
    }

    /// <summary>Frame indices into the generated sheets (see tools/art/build.py).</summary>
    public static class Frame
    {
        public const int RunFrames = 4;
        public const int Air = 4;
        public const int Hurt = 5;

        public const int CoinFrames = 6;
        public const int HeartItem = 6;

        public const int SignJump = 4;

        public const int Dust = 0;
        public const int Sparkle = 4;
        public const int Splash = 8;
        public const int Rock = 12;
        public const int Ring = 14;
        public const int Leaf = 18;

        public const int TileTop = 0;
        public const int TileFill = 1;
        public const int TileWaterTop = 4;
        public const int TileWater = 5;
        public const int TileCeiling = 6;
        public const int TileWall = 7;
        public const int TileWallTop = 8;
    }

    /// <summary>Keeps progress and settings in PlayerPrefs (the registry on Windows, IndexedDB on the web).</summary>
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
