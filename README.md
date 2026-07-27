# CosmosEdu

A real-time cosmos exhibit built with **Unity 6 + URP** — four connected, fully narrated
educational simulations: a general-relativity black hole, the Milky Way, the solar system, and
the nebulae &amp; clusters gallery, each with a desktop showcase and a Quest passthrough (MR) edition.

*Formerly **BlackHoleEdu** — the project outgrew its first name.*

**▶ Live WebGL demo: [cosmos-edu-783.netlify.app](https://cosmos-edu-783.netlify.app)**

*[한국어 README](README.ko.md)*

## The exhibit at a glance

A title screen (build index 0) lets visitors pick a language and an experience; every scene has a
way back to it (a toolbar button on desktop, a menu button in MR). The Quest APK boots straight into
`MRTitle`, the passthrough picker; a PC build with a headset attached starts at `TitleScreen`, detects
the HMD, and hands off to it.

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
- **Runs on PC (primary), Quest passthrough, and WebGL — phone browsers included**: touch controls,
  a 16:9 letterbox, a UI laid out against a 1280-wide reference so captions stay legible, and
  adaptive resolution (all shaders `target 3.5`; post-processing is disabled on web — a known
  URP/WebGL FSR limitation)

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
- **MR edition** — a room-scale hole you grab with one hand and scale with two, the guided tour,
  the birth intro, disk colors, spin, the EHT comparison and the difficulty level, all on a
  hand-ray button menu. Deliberately *not* ported: the fall-in (forced translation with a
  stationary head is the textbook sickness trigger, and the real floor stays put), the merger
  (it swaps the room for a starfield, turning an MR exhibit into a VR one mid-session), the mass
  cycle (it grows the horizon past the viewer, who is one arm away), the Einstein ring and lens
  magnifier (both need a bright source *behind* the hole — in passthrough the background is your
  living room), and the light curve and theory sheet (desktop-density line art). The tour skips
  its Einstein-ring step here rather than talking for twenty seconds over nothing, and renumbers
  its footer to what the scene actually offers.

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
  guided tour re-pointed with a highlight ring instead of a camera. Menu: galaxy tour · labels ·
  ambient spin · reset (it returns to its home pose and scale) · title. The ambient spin pauses
  while a hand is holding it.

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
- **MR edition** — a room-scale orrery (Neptune's orbit ≈ 1.2 m) at chest height, built around
  **tapping a planet**: aim, pull the trigger, and the orrery fades out while that one body rises
  to 25 cm across at reading distance, turning on its axis with the same NASA fact strip the
  desktop tour uses. Menu: planet tour · time speed · labels · title. The rig-shrinking true-scale
  sequence is gone — it and the focus card fought over the rig's scale, and the tap is the verb
  this exhibit is built around now.

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
- **MR edition** — a vitrine rather than an instrument: the six specimens live under one root
  (~0.8 m) and are shown one at a time, with the museum label following along and the Horsehead
  as a shadow box floating in your room. You step through them (◀ previous / next ▶) or hand the
  room to the "life of a star" tour and let it drive; a dwell timer only fills the gaps, so an
  unattended case still cycles. Not grabbable — six volumetric specimens have no reason to be
  carried around, and a grab with no reset strands the case wherever it lands.

## Controls in the headset

There is no keyboard in MR, so every exhibit carries the same three things: a **hand ray** for
buttons and tap targets, **grab** for whatever the exhibit lets you hold, and a **button menu**
along the bottom of the world-space frame. The menu calls straight into `DesktopControls` — the
cycles, the toasts and the four languages live there, so the two editions cannot drift apart.

| Gesture | What it does |
|---|---|
| Point + trigger | Press a menu button; tap a planet in the solar orrery; drive the tour cards |
| Grab (one hand) | Move the black hole, the galaxy miniature, a focused planet |
| Grab (two hands) | Scale it — the general grab transformer allows two-handed scaling |
| Poke the docent orb | Pause / resume the line it is speaking |

The frame itself is placed **against your head pose when the scene opens**, not against the scene
origin: on a Quest the world origin is wherever the room was set up, so a fixed anchor puts the
menu behind whoever did not start on that exact spot. Once placed it is world-locked — a menu that
chases the head reads as a helmet HUD — but if it leaves reach (past 3.2 m, closer than 0.9 m, or
more than 75° off your gaze) for a second and a half, it glides back in front of you.

Reading text is **1.4× its desktop size** in MR. The frame is 1920 px hung 2.6 m wide about 1.6 m
away, which puts a 20 px line near one degree — fine for Latin, not fine for Korean, where a
syllable block packs two or three strokes into the space a Latin letter uses and fills in to grey
texture. Only heights grow: x becomes an angle on the canvas cylinder, and the widest button row
already spends the 70° comfort budget.

| Scene | Menu |
|---|---|
| `MRTitle` | four exhibit cards + the language row; a docent orb greets you and, after 45 s of hesitation, points at the first card |
| `BlackHoleMR` | guided tour · birth · disk colors · spin · EHT photo · level ⁄ spaghettify · jets ⁄ title |
| `MilkyWayMR` | galaxy tour · labels · spin · reset ⁄ title |
| `SolarSystemMR` | planet tour · time speed · labels ⁄ title — plus tap any planet, then **Back** |
| `NebulaMR` | life-of-a-star tour · ◀ previous · next ▶ ⁄ title |

Six buttons is the widest row that still lands inside the comfort budget at 1.6 m, which is why
the rows are short and the only way out of every exhibit is the title screen — hopping straight
between exhibits put three destinations in every menu and made the way home the fourth thing to
find.

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
- **The MR solar focus card shows every body at the same 25 cm.** It is about what a world looks
  like; true relative scale is a different lesson, and mixing them teaches neither.

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
- The MR docent's own lines are baked to `Assets/MilkyWay/Audio/NarrationMR/`, deliberately **not**
  under `Resources/`: anything in `Resources` ships with every platform including the web build,
  and these clips are headset-only (`Tools/generate_mr_solar_narration.py`)
- Narration imports at **22 kHz mono Vorbis**. It is one voice reading a caption; speech energy
  dies well below 11 kHz, and the web build charges the visitor for every byte before the first
  frame
- The shipped CJK font is a **subset**, not the 15.7 MB original. `Tools/subset_font.py` cuts
  Noto Sans KR down to the ~2,000 characters the project's own sources contain (0.73 MB) and
  verifies its output. **Re-run it after adding text with new characters** — a glyph missing from
  a dynamic font renders as nothing at all, with no tofu box to catch in a screenshot. The full
  font stays in `Assets/BlackHoleEffect/Fonts~/`, which Unity ignores by convention
- WebGL builds are gzip-compressed; serve with `python Builds/serve_webgl.py` locally

## Building

All scenes are **menu-driven build artifacts** (`Tools/…/Create … Scene`) and are already
registered in *Build Settings* — `TitleScreen` at index 0 is the boot scene on desktop and web.
Android and Windows both ship the whole scene list: the MR scenes are inert outside a headset, and
`TitleScreen` hands off to `MRTitle` when an HMD is running. The Quest APK does not rely on that
handoff — `BuildAndroid` moves `MRTitle` to index 0, so the headset build has only the MR front
door. The WebGL site build is
the exception — it takes the five desktop scenes only, because the MR scenes would drag the XR
sample assets (hand recordings, demo textures, ~14 MB) into the data file.

Prefer the `Tools/Cosmos/` menu items over *Build Profiles*: they pin the scene list and the output
path per platform.

| Menu item | Method | Output |
|---|---|---|
| Build WebGL (Site, desktop scenes) | `MilkyWay.WebGLSiteBuild.Build` | `Builds/WebGL/` |
| Build Android (Quest APK) | `MilkyWay.WebGLSiteBuild.BuildAndroid` | `Builds/Android/CosmosEdu.apk` |
| Build Windows (full exhibit) | `MilkyWay.WebGLSiteBuild.BuildWindows` | `Builds/Windows/CosmosEdu.exe` |

The scenes themselves are regenerated the same way. `Tools/Cosmos/Rebuild All MR Scenes` runs all
five MR builders in order (the four exhibits plus `MRTitle`) — the scenes are code, so a change to
a builder is not real until they are rebuilt, and doing them one menu item at a time is how one
gets forgotten.

Each one **refuses to build a platform that is not already the active target** — it switches and
asks you to re-run. Player scripts compile with the defines of whatever target is active, so
building Android straight from the Web target would bake `UNITY_WEBGL` branches into the APK, and
the switch itself queues a domain reload that would strand the build mid-call.

### PC (Windows)

1. `File → Build Profiles` → platform **Windows**.
2. Scripting backend: **Mono** works out of the box; switch to IL2CPP if the module is installed
   (`Project Settings → Player → Configuration`).
3. `Tools/Cosmos/Build Windows (full exhibit)`, then run `Builds/Windows/CosmosEdu.exe`.

### WebGL

1. `File → Build Profiles` → switch platform to **WebGL**.
2. Player settings that must stay as configured (already set in the repo):
   - **WebGL Template: `PROJECT:CosmosEdu`** — the bundled template fixes canvas focus (keyboard
     input) and right-drag orbiting in the browser; the default template breaks both.
   - Compression **gzip** — the hosting server must send `Content-Encoding: gzip`.
   - Every shader stays `#pragma target 3.5` (SM 4.5 features silently break WebGL builds).
3. `Tools/Cosmos/Build WebGL (Site, desktop scenes)`, then serve locally:
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
4. `Tools/Cosmos/Build Android (Quest APK)`, then install: `adb install -r Builds/Android/CosmosEdu.apk`.
5. On device the build boots straight into `MRTitle` — the passthrough picker with all four MR
   exhibits, hung in front of wherever the visitor happens to be standing. In-editor, pressing Play
   in any MR scene spawns the **XR Device Simulator** for keyboard/mouse hand-ray testing.
6. Bringing up Quest Link, where it is genuinely hard to tell whether a headset is driving the
   scene or a simulator is: `MRDiagnostics` dumps the display subsystems, the registered input
   devices, what drives the camera and whether it moves — grep the editor log for `[MRDIAG]`. It
   is scaffolding, not a feature; delete it once the bring-up is settled.

### Headless, and driving an editor that is already open

The same three methods run from the command line, one target per invocation:

```
Unity.exe -batchmode -quit -projectPath <repo> -buildTarget Android \
          -executeMethod MilkyWay.WebGLSiteBuild.BuildAndroid -logFile android.log
```

`-buildTarget` makes the target active before `-executeMethod` runs, which is what satisfies the
guard above. **Close the editor first** — two Unity instances cannot open one project, and batchmode
aborts on the spot while the lock (`Temp/UnityLockfile`) is held. Two traps if you script this:

- `Unity.exe` is a GUI-subsystem binary, so a shell that launches it with `&` or `start` returns the
  instant it spawns rather than when the build ends (PowerShell: `Start-Process -Wait`). Fire three
  builds that way and they collide on the project lock instead of queueing.
- Judge a build by the `[…Build] Succeeded` line in its log, not by the artifact's timestamp. The
  Windows player's `CosmosEdu.exe` and `UnityPlayer.dll` are copied from the editor install with
  their original mtimes, and an incremental WebGL build leaves `WebGL.wasm.gz` untouched when the
  IL2CPP output is unchanged — both look stale while being perfectly current.

To build without closing a running editor, write the target into `Temp/cosmos-autobuild.txt`
(`webgl`, `android`, or `windows`). `AutoBuildTrigger` reads it on the next domain reload, switches
target if needed, and builds on the reload that follows — the request survives the first pass and is
consumed before `BuildPlayer`, so a build that takes down the editor cannot loop on restart.

Also switching targets is not free in the working tree: Unity rewrites `preloadedAssets` in
`ProjectSettings.asset` and deletes the XR Simulation assets under `Assets/XR/`. Revert that churn
rather than committing it.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
