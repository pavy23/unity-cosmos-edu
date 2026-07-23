#ifndef BLACKHOLE_STARS_INCLUDED
#define BLACKHOLE_STARS_INCLUDED

// Shared procedural starfield + noise. Used by both the skybox shader and the
// black hole raymarch shader so lensed and un-lensed background match exactly.

float bh_hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float3 bh_hash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

float bh_vnoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = bh_hash21(i);
    float b = bh_hash21(i + float2(1, 0));
    float c = bh_hash21(i + float2(0, 1));
    float d = bh_hash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float bh_fbm2(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        v += a * bh_vnoise2(p);
        p = p * 2.13 + 17.7;
        a *= 0.5;
    }
    return v;
}

float bh_vnoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = bh_hash33(i).x;
    float n100 = bh_hash33(i + float3(1, 0, 0)).x;
    float n010 = bh_hash33(i + float3(0, 1, 0)).x;
    float n110 = bh_hash33(i + float3(1, 1, 0)).x;
    float n001 = bh_hash33(i + float3(0, 0, 1)).x;
    float n101 = bh_hash33(i + float3(1, 0, 1)).x;
    float n011 = bh_hash33(i + float3(0, 1, 1)).x;
    float n111 = bh_hash33(i + float3(1, 1, 1)).x;
    float nx00 = lerp(n000, n100, f.x);
    float nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x);
    float nx11 = lerp(n011, n111, f.x);
    float nxy0 = lerp(nx00, nx10, f.y);
    float nxy1 = lerp(nx01, nx11, f.y);
    return lerp(nxy0, nxy1, f.z);
}

float bh_fbm3(float3 p)
{
    float v = 0.0;
    float a = 0.5;
    [unroll]
    for (int k = 0; k < 3; k++)
    {
        v += a * bh_vnoise3(p);
        p = p * 2.17 + 9.3;
        a *= 0.5;
    }
    return v;
}

// Star color from a pseudo temperature parameter (0 = cool red, 1 = hot blue).
// Follows the real stellar population: most stars are cool orange, a few are
// blue-white beacons.
float3 bh_starTint(float t)
{
    float3 cool = float3(1.0, 0.52, 0.30);   // M/K orange-red
    float3 sun  = float3(1.0, 0.90, 0.72);   // G yellow-white
    float3 hot  = float3(0.66, 0.78, 1.0);   // B/A blue-white
    return t < 0.5 ? lerp(cool, sun, t * 2.0) : lerp(sun, hot, t * 2.0 - 1.0);
}

// Long-exposure telescope sky: three star layers with realistic color
// temperatures, gaussian PSF cores + wide faint halos, diffraction spikes on
// the brightest stars, a Milky-Way band with dust lanes, and two-tone nebula.
float3 bh_starField(float3 rd, float density, float nebula, float bandStrength)
{
    float3 col = float3(0.0, 0.0, 0.0);

    // Domain warp for every low-frequency haze term. Trilinear value noise
    // alone produces axis-aligned boxy blobs that read as "square textures"
    // when zoomed out; warping the sample direction makes them organic.
    float3 warp;
    warp.x = bh_fbm3(rd * 3.1 + 5.0);
    warp.y = bh_fbm3(rd * 3.3 - 2.0);
    warp.z = bh_fbm3(rd * 2.9 + 9.0);
    float3 rdw = normalize(rd + (warp - 0.5) * 0.55);

    // --- Milky-Way band: a tilted great circle of dense unresolved stars ---
    const float3 bandN = normalize(float3(0.28, 0.87, 0.40));
    float bandDist = dot(rdw, bandN); // warped → ragged, natural band edges
    // bandStrength lets a scene hide the Milky-Way band while keeping the
    // plain starfield — the solar-system exhibit turns it off so the galaxy
    // isn't seen from inside it.
    float band = exp(-bandDist * bandDist * 34.0) * bandStrength;
    if (band > 0.003)
    {
        // Unresolved star glow, broken by dark dust lanes.
        float glowN = bh_fbm3(rdw * 5.0 + 3.7);
        float dust = smoothstep(0.42, 0.72, bh_fbm3(rdw * 7.5 - 11.0));
        float3 glow = lerp(float3(0.055, 0.045, 0.038), float3(0.030, 0.033, 0.048), glowN)
                    * (0.35 + 0.65 * glowN);
        col += band * glow * (1.0 - 0.82 * dust) * (0.4 + 0.6 * nebula + 0.5 * density);
    }

    // --- Interstellar dust: warm brown lanes + cold blue reflection haze ---
    float n1 = pow(saturate(bh_fbm3(rdw * 2.6 + 7.0)), 3.0);
    float n2 = pow(saturate(bh_fbm3(rdw * 4.3 - 3.0)), 3.6);
    col += (float3(0.05, 0.032, 0.045) * n1 + float3(0.018, 0.028, 0.055) * n2) * nebula;

    // --- Three star layers at different angular scales. Density controls how
    // many cells actually contain a star; the Milky-Way band gets extra stars.
    [unroll]
    for (int l = 0; l < 3; l++)
    {
        float grid = (l == 0) ? 21.0 : ((l == 1) ? 46.0 : 92.0);
        float p = ((l == 0) ? 0.10 : ((l == 1) ? 0.18 : 0.26)) * saturate(density);
        if (l == 2) p *= 0.4 + 1.6 * band;   // fine dust of stars along the band
        float3 id = floor(rd * grid);
        float3 h = bh_hash33(id);
        if (h.z < 1.0 - p) continue;
        float3 starDir = normalize((id + 0.15 + 0.7 * h) / grid);
        float3 off = rd - starDir;
        float d = length(off);
        float size = (0.0014 + 0.002 * h.x) * ((l == 2) ? 0.55 : 1.0);
        float bright = lerp(0.2, 1.8, pow(h.y, 6.0)) * ((l == 2) ? 0.45 : 1.0);

        // Population-correct color: mostly cool, occasionally blue and bright.
        float temp = pow(h.x, 1.6);
        float3 tint = bh_starTint(temp);
        bright *= lerp(0.8, 1.6, temp); // hot stars really are brighter

        // Gentle scintillation: every star breathes on its own phase and rate,
        // so the sky shimmers without ever pulsing in sync. Subtle by design —
        // ±15% reads as atmosphere, more reads as fairy lights. Shared code, so
        // the lensed background twinkles in step with the sky around it.
        bright *= 1.0 + 0.15 * sin(_Time.y * (1.2 + 2.4 * h.x) + h.y * 6.2832 + h.z * 3.1);

        // Gaussian PSF core + a modest halo so bright stars glow. The halo
        // must die out well inside the grid cell, otherwise it gets truncated
        // at the cell border and shows up as square patches.
        float star = exp(-(d * d) / (size * size * 0.28));
        star += exp(-(d * d) / (size * size * 2.2)) * 0.05 * bright;

        // Diffraction spikes on the brightest few stars only.
        if (h.y > 0.93 && l < 2)
        {
            float3 axisA = normalize(cross(starDir, float3(0.21, 0.95, 0.05)));
            float3 axisB = cross(starDir, axisA);
            float sx = dot(off, axisA);
            float sy = dot(off, axisB);
            float spikes = exp(-abs(sx) * 260.0) * exp(-abs(sy) * 2600.0)
                         + exp(-abs(sy) * 260.0) * exp(-abs(sx) * 2600.0);
            star += spikes * 0.35;
        }

        col += star * bright * tint;
    }
    return col;
}

// Back-compat overload: full-strength Milky-Way band (the black-hole scenes).
float3 bh_starField(float3 rd, float density, float nebula)
{
    return bh_starField(rd, density, nebula, 1.0);
}

#endif
