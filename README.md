# BlackHoleEdu

A real-time, general-relativity-based black hole educational simulation built with **Unity 6 + URP**.

*[한국어 README](README.ko.md)*

Everything you see in the image — the shadow, the photon ring, the lensed arcs — comes from
**numerically integrating the geodesic equation per pixel** in a fragment shader. Brightness and
color come from relativistic shifts: Doppler beaming, gravitational redshift, and blackbody radiation.

## Features

- **Schwarzschild raymarching** — null geodesics integrated with a leapfrog scheme on a single
  billboard quad; thin accretion disk + volumetric haze, relativistic beaming *I ∝ (δ·g)³*,
  Shakura–Sunyaev thin-disk temperature *T ∝ r^(−3/4)*, Planckian-locus blackbody colors
- **Kerr (spinning) black hole** — Kerr–Schild coordinate Hamiltonian integration: D-shaped shadow,
  frame dragging, disk inner edge tracking the prograde ISCO(a) (F4 spin presets 0 → 0.998)
- **Binary black hole merger** — the GW150914 story (F7): two lensing centers inspiraling on a
  Peters-equation orbit, gravitational-wave chirp audio synced to the actual orbital frequency,
  merger flash, ringdown, and a Kerr remnant with 95% of the total mass and spin a ≈ 0.69
- **Guided tour** — 11 narrated steps (G), star-collapse intro (F5), first-person fall-in (F6),
  gravitational-lens magnifier (F3)
- **Educational tools** — photon trajectory launcher (Space), spaghettification (T), Einstein ring
  (E), time-dilation clocks linked to the actual camera distance, light curve (V), EHT photo
  comparison (O)
- **Theory panel** — context-sensitive governing-equation cards (X; auto-shown at the advanced
  difficulty level, F2)
- **Korean / English toggle** — every caption, panel and narration clip is bilingual (K), with
  neural-TTS voice-over in both languages
- **Fully procedural assets** — starfield/Milky-Way skybox, star surface shader (convection
  granulation + corona), ambient soundscape and GW chirp are all generated in code; zero external
  art or audio assets
- **MR scene** — Quest passthrough (`BlackHoleMR`): room-scale hole you can grab and scale,
  throwable spectral-type star-balls, binary-merger haptics, palm-summoned mini black hole

## Controls (BlackHoleShowcase scene)

| Key | Action | Key | Action |
|---|---|---|---|
| RMB drag / wheel | Orbit / zoom camera | G · N · B | Guided tour |
| 1·2·3 | Disk color presets | 4·5·6 | Mass presets |
| Space / C | Fire photons / clear | E (A/D) | Einstein ring |
| T / J | Spaghettification / jets | V / O | Light curve / EHT compare |
| X / F2 | Theory panel / difficulty | K | Korean / English |
| F3 / F4 | Lens magnifier / spin | F5 / F6 / F7 | Intro / fall-in / merger |
| L / I / U / M / H | Labels / panel / immersive / sound / help | R | Reset camera |

## Physical honesty

The lensing geometry, shadow size, time-dilation numbers and Doppler asymmetry are numerical
solutions of the real equations. The disk's turbulence texture, the supernova/jet effects, and the
close-range lensing of the binary merger (a superposed-deflection approximation — no analytic
two-body metric exists) are physically motivated visualizations. Each in-app theory card (X) states
which is which.

## Requirements

- Unity 6 (URP 17); the XR packages are only needed for the MR scene
- Narration audio ships in `Assets/BlackHoleEffect/Resources/Narration/` (regenerate with
  [edge-tts](https://github.com/rany2/edge-tts); transcripts live in each script's `Lines` arrays)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
