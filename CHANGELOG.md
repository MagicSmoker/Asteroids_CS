# Changelog

Version shown in the bottom-right corner of the attract/options screens
(`GameConstants.Version`). **Policy: bump the MINOR number on every change**
(1.0.0 → 1.1.0 → 1.2.0, ...), reset PATCH to 0, and log what changed here.
MAJOR only changes on explicit request.

## 1.1.0

- Added a copyright/credits line ("COPYRIGHT 1979 ATARI • C# VERSION BY
  JIMMY IPOCK") to the attract-mode start and high-score screens.

## 1.0.0

Initial tracked release. Everything built up to this point:

- Core gameplay matching the original arcade manual (TM-143): frictionless
  ship inertia, splitting asteroids (large → 2 medium → 2 small, 20/50/100
  points), waves of 4/6/8/10 large asteroids, large and small flying saucers
  (200/1000 points, the small one aims accurately), hyperspace with the
  manual's own-risk "may also explode on reentry" chance, and an extra ship
  every 10,000 points.
- 1-player and 2-player alternating-turn modes with independent scores/lives,
  attract mode alternating a drifting-asteroids demo with the 10-highest
  scores table, and the manual's own high-score initials entry (rotate
  through A-Z-blank per character).
- A start page that reproduces the cabinet's actual DIP switch banks from
  Figure 7 of the manual: an 8-toggle switch (language, ships per game,
  center/right coin mechanism multipliers, coinage/free play) and a 4-toggle
  switch (coin door denomination configuration), each rendered as a
  clickable/keyboard-navigable physical rocker switch with the self-test
  screen's own "0 = ON / 1 = OFF" readout row and decoded option text.
- Language switch actually retranslates in-game/attract text (English,
  German, French, Spanish); coinage switch drives a simulated credits system
  (5/6/7 = insert coin in the left/center/right mechanism, honoring each
  mechanism's multiplier and the 1-for-1/1-for-2/2-for-1/free-play setting).
- Procedurally synthesized sound effects (fire, saucer fire, three asteroid
  bang pitches, saucer hums, hyperspace, ship explosion, extra ship, and the
  alternating heartbeat that quickens as the field clears) mixed via NAudio.
- Vector-CRT-style rendering: dim wireframe outlines with bright vertex glow
  for the ship, saucers, and jagged per-instance asteroid polygons; a
  stroked-vector attract-mode title.
- Window resolution options (small/medium/large, fixed 4:3 aspect).
