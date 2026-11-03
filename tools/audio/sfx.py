"""Sound effect recipes for ShapeShifter. Each function returns a rendered Track."""

from synth import Track, note_freq, saw, sine, square, triangle


def pulse(duty):
    return lambda p: square(p, duty)


def jump():
    """A light hop: a quick upward chirp."""
    t = Track(0.16)
    t.add_tone(0, 0.09, 330, pulse(0.25), 0.18, freq_end=640, release=0.03)
    return t


def flap():
    """The bird beats its wings: two soft air puffs."""
    t = Track(0.22)
    t.add_noise(0, 0.06, 0.25, smoothing=0.75, decay_power=1.6, seed=1)
    t.add_noise(0.09, 0.06, 0.2, smoothing=0.75, decay_power=1.6, seed=2)
    t.add_tone(0, 0.08, 520, triangle, 0.06, freq_end=760, release=0.03)
    return t


def land():
    t = Track(0.1)
    t.add_noise(0, 0.05, 0.3, smoothing=0.85, decay_power=2.0, seed=3)
    t.add_tone(0, 0.04, 150, triangle, 0.22, freq_end=80, release=0.02)
    return t


def shift(base):
    """A magic whoosh that ends on the form's own note."""
    def recipe():
        t = Track(0.32)
        t.add_noise(0, 0.18, 0.12, smoothing=0.5, decay_power=1.0, seed=4)
        t.add_tone(0, 0.12, base * 0.5, sine, 0.18, freq_end=base, release=0.04)
        t.add_tone(0.08, 0.12, base, pulse(0.5), 0.1, release=0.12, vibrato=0.02)
        t.add_tone(0.08, 0.12, base * 1.5, triangle, 0.08, release=0.12)
        return t
    return recipe


def blocked():
    """Still cooling down: a short low buzz."""
    t = Track(0.12)
    t.add_tone(0, 0.08, 110, pulse(0.3), 0.14, release=0.02)
    return t


def coin():
    t = Track(0.25)
    t.add_tone(0, 0.05, note_freq("B5"), pulse(0.5), 0.13, release=0.02)
    t.add_tone(0.05, 0.12, note_freq("E6"), pulse(0.5), 0.13, release=0.06)
    return t


def heart():
    t = Track(0.45)
    for i, n in enumerate(["C5", "E5", "G5", "C6"]):
        t.add_tone(i * 0.06, 0.12, note_freq(n), triangle, 0.22, release=0.1)
    return t


def hurt():
    t = Track(0.35)
    t.add_tone(0, 0.22, 420, saw, 0.2, freq_end=120, release=0.06)
    t.add_noise(0, 0.12, 0.2, smoothing=0.4, decay_power=1.5, seed=5)
    return t


def fall():
    """Dropping into a chasm: a long falling whistle."""
    t = Track(0.6)
    t.add_tone(0, 0.5, 900, triangle, 0.18, freq_end=140, release=0.08)
    return t


def smash():
    """The golem breaks stone: a crunch and a deep thud."""
    t = Track(0.45)
    t.add_noise(0, 0.25, 0.45, smoothing=0.55, decay_power=1.8, seed=6)
    t.add_tone(0, 0.18, 110, sine, 0.45, freq_end=45, release=0.1)
    t.add_noise(0.05, 0.15, 0.2, smoothing=0.2, decay_power=2.5, seed=7)
    return t


def splash():
    t = Track(0.4)
    t.add_noise(0, 0.3, 0.3, smoothing=0.65, decay_power=1.4, seed=8)
    t.add_tone(0.02, 0.1, 700, sine, 0.08, freq_end=1300, release=0.05)
    return t


def rustle():
    """The fox slips through branches."""
    t = Track(0.3)
    for i in range(4):
        t.add_noise(i * 0.06, 0.05, 0.12, smoothing=0.3, decay_power=1.5, seed=20 + i)
    return t


def passed():
    t = Track(0.15)
    t.add_tone(0, 0.06, note_freq("A5"), triangle, 0.12, release=0.06)
    return t


def streak():
    """The multiplier rises."""
    t = Track(0.5)
    for i, n in enumerate(["E5", "A5", "C#6", "E6"]):
        t.add_tone(i * 0.05, 0.1, note_freq(n), pulse(0.25), 0.1, release=0.12)
    return t


def game_over():
    t = Track(1.6)
    for i, n in enumerate(["E5", "C5", "A4", "E4"]):
        t.add_tone(i * 0.28, 0.3, note_freq(n), pulse(0.5), 0.14, release=0.2, vibrato=0.01)
        t.add_tone(i * 0.28, 0.3, note_freq(n) / 2, triangle, 0.18, release=0.2)
    return t


def high_score():
    t = Track(1.2)
    for i, n in enumerate(["C5", "E5", "G5", "C6", "G5", "C6"]):
        t.add_tone(i * 0.11, 0.12 if i < 5 else 0.4, note_freq(n), pulse(0.25), 0.14, release=0.15)
    return t


def click():
    t = Track(0.06)
    t.add_tone(0, 0.02, 1400, square, 0.08, release=0.01)
    return t


def confirm():
    t = Track(0.2)
    t.add_tone(0, 0.05, note_freq("E5"), pulse(0.5), 0.12, release=0.02)
    t.add_tone(0.05, 0.08, note_freq("A5"), pulse(0.5), 0.12, release=0.06)
    return t


def buy():
    t = Track(0.6)
    t.add_tone(0, 0.05, note_freq("E6"), pulse(0.5), 0.12, release=0.02)
    t.add_tone(0.06, 0.05, note_freq("B6"), pulse(0.5), 0.12, release=0.02)
    for i, n in enumerate(["E5", "G#5", "B5", "E6"]):
        t.add_tone(0.14 + i * 0.05, 0.1, note_freq(n), triangle, 0.15, release=0.15)
    return t


EFFECTS = {
    "jump.ogg": jump, "flap.ogg": flap, "land.ogg": land,
    "shift_fox.ogg": shift(note_freq("A4")), "shift_bird.ogg": shift(note_freq("E5")),
    "shift_golem.ogg": shift(note_freq("A3")), "shift_fish.ogg": shift(note_freq("C#5")),
    "blocked.ogg": blocked, "coin.ogg": coin, "heart.ogg": heart, "hurt.ogg": hurt,
    "fall.ogg": fall, "smash.ogg": smash, "splash.ogg": splash, "rustle.ogg": rustle,
    "pass.ogg": passed, "streak.ogg": streak, "game_over.ogg": game_over,
    "high_score.ogg": high_score, "click.ogg": click, "confirm.ogg": confirm, "buy.ogg": buy,
}
