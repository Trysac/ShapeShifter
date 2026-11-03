using System;
using System.Collections.Generic;
using ShapeShifter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShapeShifter.Game
{
    /// <summary>One line of a menu: a label, an optional value, and what choosing or nudging it does.</summary>
    public sealed class MenuItem
    {
        public string Label;
        public Func<string> Value;
        public Action Activate;
        /// <summary>Left/right on the item (-1 or +1); null when it has no value to change.</summary>
        public Action<int> Adjust;
        public bool Enabled = true;
    }

    /// <summary>A menu or card: a title, optional text and custom content, and items chosen with keys, pad or pointer.</summary>
    public sealed class MenuPage
    {
        public string Title;
        public string Subtitle;
        public string Body;
        public string Footer;
        public bool Logo;
        public Action Back;
        /// <summary>Fills the area between the body and the items (cards, tables, name slots).</summary>
        public Action<VisualElement> Extra;
        public readonly List<MenuItem> Items = new List<MenuItem>();
    }

    /// <summary>
    /// The UI Toolkit overlay, built in code and styled by Assets/UI/Hud.uss: hearts, score,
    /// coins and distance as in 2021, the form bar with cooldowns, the warning for the next
    /// obstacle, touch buttons, floating text, banners and every menu. Items never take focus;
    /// the director reads keys and pad and moves the highlight itself.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class Hud : MonoBehaviour
    {
        sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Age;
        }

        GameArt art;
        VisualElement root;
        VisualElement play;
        VisualElement tapLayer;
        readonly List<VisualElement> hearts = new List<VisualElement>();
        Label score;
        Label coins;
        Label distance;
        Label streak;
        VisualElement warning;
        VisualElement warningIcon;
        VisualElement warningFill;
        Label warningText;
        readonly List<VisualElement> slots = new List<VisualElement>();
        readonly List<VisualElement> cooldowns = new List<VisualElement>();
        readonly List<Label> keys = new List<Label>();
        VisualElement touch;
        Label banner;
        Label bannerSub;
        VisualElement bannerBox;
        VisualElement hurt;
        VisualElement floaters;
        VisualElement menuLayer;
        VisualElement menuPanel;
        Label menuTitle;
        Label menuSubtitle;
        Label menuBody;
        VisualElement logo;
        VisualElement menuExtra;
        VisualElement menuItems;
        Label menuFooter;
        readonly List<Label> itemLabels = new List<Label>();
        readonly List<Floater> active = new List<Floater>();
        MenuPage page;
        float bannerTime;
        float hurtLevel;
        int shownHearts = -1;
        bool gamepadPrompts;

        public int Selected { get; private set; }
        public MenuPage Page => page;
        public bool MenuOpen => page != null;
        public string BannerText => bannerBox != null && !bannerBox.ClassListContains("hidden") ? banner.text : "";
        public VisualElement Root => root;

        public event Action<int> ItemClicked;
        public event Action<Form> FormClicked;
        public event Action JumpClicked;
        public event Action PauseClicked;

        public void Build(GameArt gameArt)
        {
            art = gameArt;
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("hud");
            root.pickingMode = PickingMode.Ignore;

            tapLayer = Layer("tap hidden");
            tapLayer.pickingMode = PickingMode.Position;
            tapLayer.RegisterCallback<PointerDownEvent>(_ => JumpClicked?.Invoke());
            hurt = Layer("hurt");
            floaters = Layer("floaters");

            play = Layer("play");
            var left = Box(play, "corner corner--left");
            var heartRow = Box(left, "hearts");
            for (int i = 0; i < Tuning.MaxHearts; i++) hearts.Add(Box(heartRow, "heart"));
            var right = Box(play, "corner corner--right");
            var scoreRow = Box(right, "row");
            Text(scoreRow, "score-label", "SCORE");
            score = Text(scoreRow, "score", "0");
            var stats = Box(right, "row row--end");
            Image(stats, "coin-icon", art.Coin);
            coins = Text(stats, "stat", "0");
            distance = Text(stats, "stat stat--distance", "0 m");
            streak = Text(play, "streak hidden", "");

            warning = Box(play, "warning hidden");
            Text(warning, "warning__title", "AHEAD");
            warningIcon = Box(warning, "warning__icon");
            var track = Box(warning, "warning__track");
            warningFill = Box(track, "warning__fill");
            warningText = Text(warning, "warning__text", "");

            var bar = Box(play, "formbar");
            foreach (Form form in FormRules.All)
            {
                Form captured = form;
                var slot = Box(bar, "slot");
                slot.pickingMode = PickingMode.Position;
                slot.RegisterCallback<PointerDownEvent>(e =>
                {
                    e.StopPropagation();
                    FormClicked?.Invoke(captured);
                });
                var badge = Box(slot, "slot__badge");
                badge.style.backgroundImage = new StyleBackground(art.Badge(form));
                cooldowns.Add(Box(badge, "slot__cooldown"));
                keys.Add(Text(slot, "slot__key", ""));
                Text(slot, "slot__name", form.ToString().ToUpperInvariant());
                slots.Add(slot);
            }

            touch = Box(play, "touch hidden");
            var jump = Text(touch, "touch__jump", "JUMP");
            jump.pickingMode = PickingMode.Position;
            jump.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                JumpClicked?.Invoke();
            });
            var pause = Text(play, "pause-button", "II");
            pause.pickingMode = PickingMode.Position;
            pause.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                PauseClicked?.Invoke();
            });

            bannerBox = Box(root, "banner hidden");
            banner = Text(bannerBox, "banner__title", "");
            bannerSub = Text(bannerBox, "banner__sub", "");

            menuLayer = Layer("menu-layer hidden");
            menuLayer.pickingMode = PickingMode.Position;
            menuPanel = Box(menuLayer, "menu");
            logo = Box(menuPanel, "logo");
            var emblem = Box(logo, "logo__emblem");
            emblem.style.backgroundImage = new StyleBackground(art.Icon);
            var words = Box(logo, "logo__words");
            Text(words, "logo__title", "SHAPESHIFTER");
            Text(words, "logo__tag", "fox  -  bird  -  golem  -  fish");
            menuTitle = Text(menuPanel, "menu__title", "");
            menuSubtitle = Text(menuPanel, "menu__subtitle", "");
            menuBody = Text(menuPanel, "menu__body", "");
            menuExtra = Box(menuPanel, "menu__extra");
            menuItems = Box(menuPanel, "menu__items");
            menuFooter = Text(menuPanel, "menu__footer", "");
        }

        // ---- Play HUD ----

        public void ShowPlay(bool visible)
        {
            play.EnableInClassList("hidden", !visible);
            tapLayer.EnableInClassList("hidden", !visible);
        }

        public void SetTouch(bool visible)
        {
            touch.EnableInClassList("hidden", !visible);
            root.EnableInClassList("touch-on", visible);
        }

        public void SetPrompts(bool gamepad)
        {
            if (gamepadPrompts == gamepad && keys[0].text.Length > 0) return;
            gamepadPrompts = gamepad;
            string[] labels = gamepad ? new[] { "A", "Y", "B", "X" } : new[] { "1 / K", "2 / I", "3 / L", "4 / J" };
            for (int i = 0; i < keys.Count; i++) keys[i].text = labels[i];
        }

        public void UpdatePlay(Run run)
        {
            int h = run.Health.Hearts;
            if (shownHearts != h)
            {
                shownHearts = h;
                for (int i = 0; i < hearts.Count; i++)
                {
                    hearts[i].style.backgroundImage = new StyleBackground(i < h ? art.Heart : art.HeartEmpty);
                }
            }
            score.text = run.Score.Total.ToString("D6");
            coins.text = run.Score.Coins.ToString();
            distance.text = ((int)run.Distance) + " m";
            int multiplier = run.Score.Multiplier;
            streak.EnableInClassList("hidden", multiplier <= 1);
            streak.text = $"x{multiplier}   STREAK {run.Score.Streak}";

            for (int i = 0; i < slots.Count; i++)
            {
                bool current = (int)run.Form == i;
                slots[i].EnableInClassList("slot--active", current);
                float fraction = current ? 0f : run.Shifter.CooldownFraction;
                cooldowns[i].style.height = Length.Percent(100f * Mathf.Clamp01(fraction));
            }

            Obstacle next = run.NextObstacle;
            float seconds = next != null ? (next.Start - run.X) / Mathf.Max(1f, run.Speed) : 99f;
            bool show = next != null && seconds > 0f && seconds < Tuning.TelegraphSeconds + 0.6f && !run.Over;
            warning.EnableInClassList("hidden", !show);
            if (show)
            {
                Form? needed = next.RequiredForm;
                warningIcon.style.backgroundImage = needed.HasValue
                    ? new StyleBackground(art.Badge(needed.Value))
                    : new StyleBackground(art.Frames(art.Signs, 16)[Frame.SignJump]);
                warningText.text = needed.HasValue ? needed.Value.ToString().ToUpperInvariant() : "JUMP";
                warningFill.style.width = Length.Percent(100f * Mathf.Clamp01(1f - seconds / (Tuning.TelegraphSeconds + 0.6f)));
                bool ready = !needed.HasValue || run.Form == needed.Value || (next.Kind == ObstacleKind.Boulder);
                warning.EnableInClassList("warning--ready", needed.HasValue && run.Form == needed.Value);
                warning.EnableInClassList("warning--urgent", !ready && seconds < 0.8f);
            }
        }

        public void Hurt() => hurtLevel = 1f;

        public void Banner(string title, string sub, float seconds = 1.8f)
        {
            banner.text = title;
            bannerSub.text = sub ?? "";
            bannerSub.EnableInClassList("hidden", string.IsNullOrEmpty(sub));
            bannerBox.RemoveFromClassList("hidden");
            bannerTime = seconds;
        }

        public void HideBanner()
        {
            bannerTime = 0f;
            bannerBox.AddToClassList("hidden");
        }

        public void Float(Vector3 world, string text, string style)
        {
            var label = new Label(text);
            label.AddToClassList("floater");
            if (!string.IsNullOrEmpty(style)) label.AddToClassList(style);
            label.pickingMode = PickingMode.Ignore;
            floaters.Add(label);
            active.Add(new Floater { Label = label, World = world });
        }

        public void ClearFloaters()
        {
            foreach (Floater f in active) f.Label.RemoveFromHierarchy();
            active.Clear();
        }

        public void Tick(float dt, Camera camera)
        {
            if (root == null) return;
            if (bannerTime > 0f)
            {
                bannerTime -= dt;
                if (bannerTime <= 0f) bannerBox.AddToClassList("hidden");
            }
            hurtLevel = Mathf.Max(0f, hurtLevel - dt * 2.5f);
            hurt.style.opacity = hurtLevel * 0.8f;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Floater f = active[i];
                f.Age += dt;
                if (f.Age > 0.9f || camera == null || root.panel == null)
                {
                    f.Label.RemoveFromHierarchy();
                    active.RemoveAt(i);
                    continue;
                }
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, f.World + new Vector3(0f, f.Age * 1.4f, 0f), camera);
                f.Label.style.left = p.x;
                f.Label.style.top = p.y;
                f.Label.style.opacity = 1f - Mathf.Max(0f, f.Age - 0.5f) / 0.4f;
            }
        }

        // ---- Menus ----

        public void ShowMenu(MenuPage newPage, int selected = 0)
        {
            page = newPage;
            menuLayer.RemoveFromClassList("hidden");
            menuTitle.text = page.Title ?? "";
            menuTitle.EnableInClassList("hidden", string.IsNullOrEmpty(page.Title));
            menuSubtitle.text = page.Subtitle ?? "";
            menuSubtitle.EnableInClassList("hidden", string.IsNullOrEmpty(page.Subtitle));
            menuBody.text = page.Body ?? "";
            menuBody.EnableInClassList("hidden", string.IsNullOrEmpty(page.Body));
            menuFooter.text = page.Footer ?? "";
            menuFooter.EnableInClassList("hidden", string.IsNullOrEmpty(page.Footer));
            logo.EnableInClassList("hidden", !page.Logo);
            menuPanel.EnableInClassList("menu--title", page.Logo);
            menuExtra.Clear();
            page.Extra?.Invoke(menuExtra);
            menuExtra.EnableInClassList("hidden", page.Extra == null);
            menuItems.Clear();
            itemLabels.Clear();
            for (int i = 0; i < page.Items.Count; i++)
            {
                int index = i;
                var label = new Label();
                label.AddToClassList("menu__item");
                label.EnableInClassList("menu__item--disabled", !page.Items[i].Enabled);
                label.RegisterCallback<ClickEvent>(_ => Click(index));
                label.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (page != null && index < page.Items.Count && page.Items[index].Enabled) Select(index);
                });
                menuItems.Add(label);
                itemLabels.Add(label);
            }
            Selected = Mathf.Clamp(selected, 0, Mathf.Max(0, page.Items.Count - 1));
            if (page.Items.Count > 0 && !page.Items[Selected].Enabled) Move(1);
            Refresh();
        }

        /// <summary>Rebuilds the custom content of the open page (after a purchase, a letter change...).</summary>
        public void RefreshExtra()
        {
            if (page?.Extra == null) return;
            menuExtra.Clear();
            page.Extra(menuExtra);
            Refresh();
        }

        public void SetBody(string text)
        {
            if (page == null) return;
            page.Body = text;
            menuBody.text = text ?? "";
            menuBody.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        }

        public void HideMenu()
        {
            page = null;
            menuLayer.AddToClassList("hidden");
        }

        /// <summary>Moves the highlight up (-1) or down (+1), skipping disabled items.</summary>
        public void Move(int step)
        {
            if (page == null || page.Items.Count == 0) return;
            int next = Selected;
            for (int i = 0; i < page.Items.Count; i++)
            {
                next = (next + step + page.Items.Count) % page.Items.Count;
                if (page.Items[next].Enabled) break;
            }
            Select(next);
        }

        public void Select(int index)
        {
            Selected = index;
            Refresh();
        }

        public void Refresh()
        {
            if (page == null) return;
            for (int i = 0; i < itemLabels.Count; i++)
            {
                MenuItem item = page.Items[i];
                string value = item.Value?.Invoke();
                string text = item.Label + (string.IsNullOrEmpty(value) ? "" : "   " + (item.Adjust != null ? "< " + value + " >" : value));
                itemLabels[i].text = text;
                itemLabels[i].EnableInClassList("menu__item--selected", i == Selected);
            }
        }

        void Click(int index)
        {
            if (page == null || index >= page.Items.Count || !page.Items[index].Enabled) return;
            Select(index);
            ItemClicked?.Invoke(index);
        }

        // ---- Helpers (also used by the director to fill MenuPage.Extra) ----

        VisualElement Layer(string classes)
        {
            var layer = Box(root, "layer " + classes);
            layer.pickingMode = PickingMode.Ignore;
            return layer;
        }

        public static VisualElement Box(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) element.AddToClassList(c);
            parent.Add(element);
            return element;
        }

        public static Label Text(VisualElement parent, string classes, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) label.AddToClassList(c);
            parent.Add(label);
            return label;
        }

        public static VisualElement Image(VisualElement parent, string classes, Texture2D texture)
        {
            VisualElement image = Box(parent, classes);
            image.style.backgroundImage = new StyleBackground(texture);
            return image;
        }

        public static VisualElement Image(VisualElement parent, string classes, Sprite sprite)
        {
            VisualElement image = Box(parent, classes);
            image.style.backgroundImage = new StyleBackground(sprite);
            return image;
        }
    }
}
