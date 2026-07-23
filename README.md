# CosmosEdu

A real-time cosmos exhibit built with **Unity 6 + URP** — four connected, fully narrated
educational simulations: a general-relativity black hole, the Milky Way, the solar system, and
the nebulae &amp; clusters gallery, each with a desktop showcase and a Quest passthrough (MR) edition.

*Formerly **BlackHoleEdu** — the project outgrew its first name.*

**▶ Live WebGL demo: [cosmos-edu-783.netlify.app](https://cosmos-edu-783.netlify.app)**

*[한국어 README](README.ko.md)*

## The exhibit at a glance

A title screen (build index 0) lets visitors pick a language and an experience; every scene has a
way back to it (a toolbar button on desktop, a menu button in MR). A headset boots the same build:
`TitleScreen` detects a running HMD and hands off to `MRTitle`, the passthrough picker.

| Exhibit | Desktop scene | MR scene |
|---|---|---|
| Title / picker | `TitleScreen` | `MRTitle` |
| Black hole | `BlackHoleShowcase` | `BlackHoleMR` |
| Milky Way | `MilkyWayShowcase` | `MilkyWayMR` |
| Solar system | `SolarSystemShowcase` | `SolarSystemMR` |
| Nebulae &amp; clusters | `NebulaShowcase` | `NebulaMR` |

The exhibits link to each other in-fiction as well: the black hole scene is Sagittarius A*, the
galaxy scene can dive into its core and land back at the black hole (F9), and the solar-system
tour ends by returning to the galaxy.

- **Four languages** — Korean · English · Japanese · Chinese: every caption, panel, label and
  narration clip (neural TTS per language); switch any time (K, or the on-screen selector)
- **Almost fully procedural** — skyboxes, the galaxy, star surfaces, soundscapes and
  gravitational-wave chirp audio are generated in code; the few external assets are observed
  planet/moon texture maps, DSS2 deep-sky survey photography (nebula backdrops, with
  attribution), and a bundled pan-CJK font
- **Runs on PC (primary), Quest passthrough, and WebGL** (all shaders `target 3.5`; post-processing
  is disabled on web — a known URP/WebGL FSR limitation)

---

## 1 · Black hole (`BlackHoleShowcase` / `BlackHoleMR`)

Everything you see — the shadow, the photon ring, the lensed arcs — comes from **numerically
integrating the geodesic equation per pixel** in a fragment shader. Brightness and color come from
relativistic shifts: Doppler beaming, gravitational redshift, and blackbody radiation.

- **Schwarzschild raymarching** — null geodesics integrated with a leapfrog scheme on a single
  billboard quad; thin accretion disk + volumetric haze, relativistic beaming *I ∝ (δ·g)³*,
  Shakura–Sunyaev thin-disk temperature *T ∝ r^(−3/4)*, Planckian-locus blackbody colors
- **Kerr (spinning) black hole** — Kerr–Schild coordinate Hamiltonian integration: D-shaped shadow,
  frame dragging, disk inner edge tracking the prograde ISCO(a) (key 3, presets 0 → 0.998)
- **Binary black hole merger** — the GW150914 story (F4): the disk disperses to leave **two bare
  black holes lensing the starfield** as they inspiral on a Peters-equation orbit — gas-free, as
  GW150914 itself was — with gravitational-wave chirp audio synced to the actual orbital frequency,
  merger flash, quadrupole-deformed wavefronts, ringdown, and a Kerr remnant with 95% of the total
  mass and spin a ≈ 0.69
- **Experiences** — 11-step narrated guided tour (F1), star-collapse birth intro (F2), fully narrated
  first-person fall-in with a physically honest inside-the-horizon ending (F3)
- **Educational toggles** — photon trajectory launcher (Space), Einstein ring (E), spaghettification
  (T), relativistic jets (J), gravitational-lens magnifier (G), light curve (V), EHT photo comparison
  (4); every toggle shows a card explaining what you are looking at
- **Theory panel** — context-sensitive governing-equation cards (X; auto-shown at the advanced
  difficulty level, C)
- **MR edition** — room-scale hole you can grab and scale, throwable spectral-type star-balls,
  binary-merger haptics (the room swaps for open space so the bare holes have stars to lens),
  palm-summoned mini black hole

| Category | Keys |
|---|---|
| Experiences | **F1** guided tour (N/B) · **F2** birth · **F3** fall in · **F4** merger · **F9** to the Milky Way · **Esc** skip/stop |
| Black hole | **1** disk colors · **2** mass presets · **3** spin · **4** EHT photo comparison |
| Phenomena | **Space** photons · **E** Einstein ring (A/D) · **T** spaghettification · **J** jets · **G** lens magnifier · **V** light curve |
| System | RMB orbit · wheel/W/S zoom · **R** reset · **L** labels · **I** info · **X** theory · **U** immersive · **M** sound · **K** language · **P** perf · **F10** title · **F12** snapshot · **H** help · **C** level |

## 2 · Milky Way (`MilkyWayShowcase` / `MilkyWayMR`)

A hybrid galaxy: a **raymarched volumetric model** (bar, spiral arms as density waves, dust
extinction, HII regions, warm bulge) plus a **baked starfield of ~480k point stars** the dust
genuinely attenuates. Nine narrated experiences:

- **F1 zoom journey** — from the solar system out to the full galaxy, with a "you are here" ring
- **F2 night sky** — lift off from a hillside at night and watch the Milky Way band become the disk
  seen edge-on from inside
- **F3 Andromeda encounter** — framed with the 2025 result (a coin-flip probability of merging, not
  the old certainty); viewed from beside M31 looking home, tidal tails, Milkomeda, then time rewinds
- **F4 galaxy tour** — seven-stop anatomy lesson (bulge & bar, density-wave arms, dust lanes,
  stellar nurseries, the Sun's orbit, halo & dark matter)
- **F5 cosmic zoom-out** — Local Group → cosmic web (52k impostor galaxies along filaments)
- **F6 solar-system tour** · **F7 rotation-curve lab** (the dark-matter evidence, interactive) ·
  **F8 galaxy zoo** (the Hubble sequence as volumetric specimens)
- **F9 Sagittarius A\* crossover** — dive into the core through the S-star swarm and land in the
  black-hole exhibit
- **MR edition** — the galaxy as a ~1.1 m miniature you can grab, spin and two-hand scale, tipped
  toward the viewer so the spiral face reads; feature name tags, a pulsing gold sun ring, and the
  guided tour re-pointed with a highlight ring instead of a camera

## 3 · Solar system (`SolarSystemShowcase` / `SolarSystemMR`)

A detailed orrery using **observed texture maps** (planets, the Moon, Saturn's ring strip), real
axial tilts and retrograde spins, comet-tail orbit lines tinted per planet, and calibrated motion:
spins and atmosphere flows run on an honest shared clock (1 real hour ≈ 1.5 s), while orbits use a
legibility clock (Kepler ratios preserved).

- **Click any planet** to frame it; **F1 planet tour** — nine narrated stops with NASA fact strips
- **F2 the true scale** — the "friendly map" confesses: orbits go linear in AU, bodies shrink to
  real proportions, and the solar system reveals itself as almost perfectly empty space
- **Atmosphere dynamics** — Jupiter's belts shear and the Great Red Spot churns (bounded two-phase
  flow maps over the photo maps), Venus superrotation, ice-giant winds, drifting Earth clouds —
  all speeds derived from real wind data on the exhibit clock
- **MR edition** — a room-scale orrery (Neptune's orbit ≈ 1.2 m) you can grab and rescale; the
  true-scale lesson re-authored for a fixed viewer: the rig itself shrinks so Neptune's orbit stays
  put while the planets vanish into grains

## 4 · Nebulae &amp; clusters (`NebulaShowcase` / `NebulaMR`)

A browsable gallery of six hero deep-sky objects — the Orion Nebula, the Horsehead, the Pleiades,
the Ring Nebula, the Crab, and Omega Centauri — one volumetric specimen per stellar life-stage,
shown one at a time against **real DSS2 survey photography** of each object's true region of sky.
Switching specimens fades to black and back (the swap happens behind the blackout, so no hitch);
the parked view keeps a slow two-axis camera drift, so the volumes parallax against the photos.

- **Form-matched volumetrics** — each nebula type gets its own raymarched model: turbulent Hα/OIII
  emission (Orion), a camera-facing torus with a genuinely dark hole (the Ring), sparse filament
  lace over synchrotron haze (the Crab), striated reflection wisps (the Pleiades), a baked
  point-cloud globular (ω Cen)
- **The Horsehead is a hybrid diorama** — the pillar's silhouette is *traced from the actual
  photograph* (baked into a mask texture) and extruded as an eroded dust volume, standing in front
  of the real inpainted IC 434 curtain; camera drift parallaxes the two
- **"The Life of a Star"** — a narrated tour threading the six specimens into one story: cloud →
  cold dust → cluster → planetary nebula → supernova → the survivors, and the cycle
- **MR edition** — the specimens as a single grabbable miniature (~0.8 m) shown one at a time,
  the museum label following along, the Horsehead as a shadow box floating in your room, and the
  same narrated tour driven by hand-ray cards

## The physics actually implemented (black hole)

The geometry on screen is the numerical solution of the real equations — not an artist's
impression with lensing "painted on":

| Quantity | Equation | Notes |
|---|---|---|
| Light bending | null-geodesic ODE d²**x**/dλ² = −(3/2)h²**x**/r⁵ (GM=c=1) | integrated per pixel, leapfrog KDK; the shadow (b_crit = 3√3 GM/c² ≈ 2.6 Rs), photon ring, Einstein ring and the disk's over/under arcs all *emerge* from this — none are drawn by hand |
| Kerr spacetime | Kerr–Schild Hamiltonian, Φ = H(1+l·q)² | numerical gradient; horizon r₊ = M+√(M²−a²); prograde ISCO from the Bardeen–Press–Teukolsky formula |
| Beaming & redshift | δ = 1/(1−β·cosθ), g = √(1−Rs/r), I ∝ (δg)³, T_obs = T·δg | the bright/dim disk asymmetry is computed, not textured |
| Disk temperature | Shakura–Sunyaev T ∝ r^(−3/4), colors from the Planckian locus | anchored to ~12 MK for a 10 M☉ hole, T ∝ M^(−1/4) across mass presets |
| Time dilation | √(1−Rs/r) for both clocks | the observer clock uses the camera's true distance; the "far away 1 h = X min" numbers are exact for static observers |
| Real scales | Rs = 2.953 km × M/M☉, shadow ∅ = 5.2 Rs | panel numbers for 10 M☉ / Sgr A* / M87* are the real values |
| Binary inspiral | Peters decay a(t) = a_f + (a₀−a_f)(1−t/T)^¼, Kepler ω² = M/a³, f_GW = 2f_orb | remnant mass 0.95 M_tot and spin a = 0.69 are the measured GW150914 values |
| Photon launcher | the same geodesic ODE on the CPU | capture inside b_crit is genuine, not scripted |

And honest numbers elsewhere: the galaxy's proportions (disk radius ~16 kpc, Sun at 8.2 kpc, bar at
~27°), the rotation-curve lab's flat curve vs. the Keplerian prediction, the solar system's fact
strips, tilts, retrograde spins, and the 2025 Andromeda probability are all the real values.

## Simplifications and artistic license

Being honest about what is *not* rigorous:

- **Time and space are compressed everywhere.** The merger squeezes months of inspiral into ~40 s
  (Kepler scaling preserved); disk and planet orbits are sped up on legibility clocks; the Andromeda
  encounter compresses ~10 Gyr; mass presets change the numbers correctly but visual scale ratios
  are stylized so everything stays on screen.
- **Binary lensing is a superposition** of two Schwarzschild deflections. No analytic two-black-hole
  metric exists; the last orbits really require numerical relativity. Ringdown audio is a damped
  sine, not the quasi-normal-mode spectrum. GW "rings" visualize invisible strain.
- **No radiative transfer.** Disk brightness, the galaxy's emission/extinction model, turbulence
  noise and the bright "knots" are procedural art shaped by the physics, not MHD or RT simulation
  output. Bloom and exposure are tuned for legibility.
- **The fall-in and other cinematics are staged**, not proper-time integrations (the *statements*
  in the captions are correct physics).
- **Spaghettification** uses a capped stretch for readability (the real tidal gradient is
  Δa ∝ 1/r³); supernova, jets and the intro are physically-motivated VFX.
- **The galaxy encounter is a choreographed morph**, not an N-body run; tidal tails and phase-mixing
  are shader deformations shaped by the real timeline.
- **Planet atmosphere motion** advects photographs with flow maps at real wind speeds — the pattern
  motion is real-rate, the fluid dynamics are not simulated.
- Kerr mode shows the prograde equatorial thin disk only; jets are decorative (no Blandford–Znajek).
- MR star-ball orbits are Newtonian with a room-scale GM.

Each in-app theory card (X) states whether its topic is computed or stylized.

## Notes for classroom use

- **Trust the numbers, not the stopwatch.** Panel values (Rs, shadow size, temperatures, dilation
  factors, planetary data) are correct; on-screen durations and angular sizes are compressed.
- **The dilation clocks assume *hovering* (static) observers.** Orbiting or falling observers need
  extra velocity terms — that is why the probe is described as "hovering beside the hole".
- **Colors are physical hues, but real images differ:** EHT pictures are radio interferometry in
  false color; an optical view would be blindingly bright. Use the comparison mode (4) to discuss
  this explicitly.
- **Inside-horizon content is an educated illustration.** Nothing can report back from inside; the
  captions say as much and that claim *is* the physics.
- **The Andromeda story teaches uncertainty**: the 2025 reanalysis turned a "certain collision"
  into a coin flip — a good example of science updating itself.
- The narration scripts are the on-screen captions (all four languages) — safe to quote; regenerate
  audio with edge-tts if you edit them.

## Requirements

- **Unity 6000.5.3f1** (Unity 6, URP 17); the XR packages are only needed for the MR scenes
- Narration audio ships in `Assets/*/Resources/Narration/` (regenerate with
  [edge-tts](https://github.com/rany2/edge-tts); transcripts live in each script's `NarrationLines`
  arrays — subtitle == voice is the exhibit-wide convention)
- WebGL builds are gzip-compressed; serve with `python Builds/serve_webgl.py` locally

## Building

All scenes are **menu-driven build artifacts** (`Tools/…/Create … Scene`) and are already
registered in *Build Settings* — `TitleScreen` at index 0 is the boot scene for every platform.
One scene list serves all three targets: the MR scenes are inert outside a headset, and
`TitleScreen` hands off to `MRTitle` automatically when an HMD is running.

### PC (Windows)

1. `File → Build Profiles` → platform **Windows**.
2. Scripting backend: **Mono** works out of the box; switch to IL2CPP if the module is installed
   (`Project Settings → Player → Configuration`).
3. Build to `Builds/Windows/` and run `CosmosEdu.exe`.

### WebGL

1. `File → Build Profiles` → switch platform to **WebGL**.
2. Player settings that must stay as configured (already set in the repo):
   - **WebGL Template: `PROJECT:CosmosEdu`** — the bundled template fixes canvas focus (keyboard
     input) and right-drag orbiting in the browser; the default template breaks both.
   - Compression **gzip** — the hosting server must send `Content-Encoding: gzip`.
   - Every shader stays `#pragma target 3.5` (SM 4.5 features silently break WebGL builds).
3. Build to `Builds/WebGL/`, then serve locally:
   ```
   python Builds/serve_webgl.py    # http://localhost:8123 (handles Content-Encoding)
   ```
   A bare `python -m http.server` will NOT work with gzip builds.
4. Known WebGL limits: post-processing is off (URP FSR shader incompatibility — PC is unaffected),
   and audio streaming is disabled (clips decompress on load).

### MR (Meta Quest, passthrough)

1. `File → Build Profiles` → platform **Android** (install the Android modules + OpenXR support).
2. Scripting backend **IL2CPP** + target architecture **ARM64** (Quest requires both).
3. `Project Settings → XR Plug-in Management → Android`: enable **OpenXR** with the Meta Quest
   feature group (passthrough requires the Meta OpenXR features; the AR camera in each MR scene
   drives it via AR Foundation).
4. Build the APK and install: `adb install -r CosmosEdu.apk`.
5. On device the build boots into `TitleScreen`, detects the HMD, and lands in `MRTitle` — the
   passthrough picker with all four MR exhibits. In-editor, pressing Play in any MR scene spawns
   the **XR Device Simulator** for keyboard/mouse hand-ray testing.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
