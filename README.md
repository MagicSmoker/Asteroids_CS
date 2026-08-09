# Asteroids

A vector-wireframe recreation of Atari's 1979 *Asteroids*, built from the
original TM-143 Operation, Maintenance and Service Manual. C# / WinForms,
same style as the [StarCastle](../StarCastle) project in this repo.

## Run it

```bash
dotnet run --project Asteroids.csproj
```

## Controls

- `Left`/`A`, `Right`/`D` — rotate
- `Up`/`W` — thrust
- `Space` — fire
- `Down`/`S`/`Shift` — hyperspace
- `5` / `6` / `7` — insert a coin (left / center / right coin mechanism)
- `1` / `2` — start a 1- or 2-player game
- `O` — open the game options (DIP switch) screen
- `R` — cycle window resolution

## Game options (DIP switches)

The start page's **O** menu reproduces the two toggle-switch banks from
Figure 7 of the manual, exactly as a technician would set them on the real
cabinet: language, ships per game, coin mechanism multipliers and coinage on
the 8-toggle switch, and coin door denomination wiring on the 4-toggle
switch. Use the arrow keys (or click a switch directly) to select and flip
one; `Enter`/`Space` also flips the selected switch. The panel shows the same
"0 = ON / 1 = OFF" readout the real self-test screen prints, plus the plain-
English meaning of the current setting.
