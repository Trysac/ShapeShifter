"""Original chiptune music for ShapeShifter, on minor pentatonic scales.

Melodies are 'NOTE:SIXTEENTHS' tokens ('R' is a rest), played by a pulse-wave lead. Each bar
names a chord, arpeggiated by a soft triangle on the 'x' steps of the song's arp pattern; a
bass plays on 'R' (root) and 'F' (fifth). Drums: K kick, k soft kick, s snare, h hat, '.' rest.
"""

from synth import Track, note_freq, sine, square, triangle

CHORDS = {
    "Am": ("A", [0, 3, 7]), "C": ("C", [0, 4, 7]), "G": ("G", [0, 4, 7]), "F": ("F", [0, 4, 7]),
    "Dm": ("D", [0, 3, 7]), "Em": ("E", [0, 3, 7]),
}


def parse(melody):
    notes = []
    for token in melody.split():
        name, length = token.split(":")
        notes.append((None if name == "R" else name, int(length)))
    return notes


def kick(track, at, volume):
    track.add_tone(at, 0.12, 120, sine, volume, freq_end=45, release=0.05)


def snare(track, at, seed):
    track.add_noise(at, 0.1, 0.16, smoothing=0.25, decay_power=1.6, seed=seed)
    track.add_tone(at, 0.04, 220, triangle, 0.08, freq_end=160, release=0.02)


def hat(track, at, seed):
    track.add_noise(at, 0.03, 0.05, smoothing=0.0, decay_power=2.0, seed=seed)


def render(song):
    sixteenth = song["sixteenth"]
    steps = song["steps"]
    bars = [bar for section in song["form"] for bar in song["sections"][section]]
    total = len(bars) * steps * sixteenth
    track = Track(total + 0.4)
    duty = song.get("duty", 0.25)

    def lead(p):
        return square(p, duty)

    for bar_index, (melody, chord) in enumerate(bars):
        bar_start = bar_index * steps * sixteenth
        notes = parse(melody)
        assert sum(length for _, length in notes) == steps, f"bar {bar_index} is not {steps} steps: {melody}"
        cursor = 0
        for name, length in notes:
            if name:
                track.add_tone(bar_start + cursor * sixteenth, length * sixteenth * 0.85, note_freq(name),
                               lead, song["lead_volume"], attack=0.005, decay=0.06, sustain=0.6,
                               release=0.05, vibrato=0.008 if length >= 4 else 0.0)
            cursor += length
        root, intervals = CHORDS[chord]
        arp = [note_freq(root + "4") * 2 ** (i / 12) for i in intervals + [12]]
        for step, kind in enumerate(song["arp"]):
            if kind == "x":
                track.add_tone(bar_start + step * sixteenth, sixteenth * 0.8, arp[step % len(arp)], triangle,
                               song["arp_volume"], release=0.03)
        bass_root = note_freq(root + "2")
        for step, kind in enumerate(song["bass"]):
            if kind in "RF":
                freq = bass_root if kind == "R" else bass_root * 2 ** (7 / 12)
                track.add_tone(bar_start + step * sixteenth, sixteenth * 1.7, freq, triangle, 0.34, release=0.03)
        for step, kind in enumerate(song["drums"]):
            at = bar_start + step * sixteenth
            seed = bar_index * 31 + step
            if kind == "K":
                kick(track, at, 0.5)
            elif kind == "k":
                kick(track, at, 0.28)
            elif kind == "s":
                snare(track, at, seed)
            elif kind == "h":
                hat(track, at, seed)
    track.normalize(0.8)
    track.fade(fade_out=0.02)
    return track


TITLE = [
    ("A4:4 C5:4 E5:4 D5:4", "Am"), ("C5:8 A4:4 G4:4", "F"),
    ("A4:4 C5:4 D5:4 E5:4", "C"), ("G5:8 E5:8", "G"),
    ("A5:4 G5:4 E5:4 D5:4", "Am"), ("C5:8 D5:4 E5:4", "F"),
    ("D5:4 C5:4 A4:4 G4:4", "G"), ("A4:12 R:4", "Am"),
]

RUN_A = [
    ("A4:2 A4:1 C5:1 E5:2 A4:2 G5:2 E5:2 D5:2 C5:2", "Am"),
    ("C5:2 C5:1 D5:1 E5:2 C5:2 A4:4 G4:4", "F"),
    ("E5:2 E5:1 G5:1 A5:2 G5:2 E5:2 D5:2 C5:2 D5:2", "C"),
    ("D5:2 C5:2 A4:2 G4:2 A4:8", "G"),
]
RUN_B = [
    ("E5:4 G5:2 A5:2 C6:4 A5:4", "Am"),
    ("G5:2 E5:2 D5:2 C5:2 D5:8", "G"),
    ("C5:2 D5:2 E5:2 G5:2 A5:2 G5:2 E5:2 D5:2", "F"),
    ("E5:2 D5:1 C5:1 A4:2 G4:2 A4:8", "Am"),
]

SONGS = {
    "title_theme.ogg": {
        "sixteenth": 0.14, "steps": 16, "form": "AA", "lead_volume": 0.2, "arp_volume": 0.08, "duty": 0.5,
        "sections": {"A": TITLE},
        "arp": "x.x.x.x.x.x.x.x.", "bass": "R.......F.......", "drums": "k.......k.......",
    },
    "run_theme.ogg": {
        "sixteenth": 0.095, "steps": 16, "form": "AABA", "lead_volume": 0.2, "arp_volume": 0.07,
        "sections": {"A": RUN_A, "B": RUN_B},
        "arp": "xxxxxxxxxxxxxxxx", "bass": "R.R.F.R.R.R.F.R.", "drums": "K.h.s.hhK.hKs.hh",
    },
}
