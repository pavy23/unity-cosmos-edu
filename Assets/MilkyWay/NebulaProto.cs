using UnityEngine;

namespace MilkyWay
{
    /// <summary>
    /// Phase-0 preset cycler: swaps the NebulaVolume material between the three
    /// looks (emission / reflection / planetary) so we can judge the shader.
    /// Click anywhere or press Space to advance; auto-advances otherwise.
    /// </summary>
    public class NebulaProto : MonoBehaviour
    {
        public Material nebulaMaterial;
        public NebulaStars stars;
        public float autoSeconds = 6f;

        [System.Serializable]
        public struct Preset
        {
            public string name;
            public float type;
            public float brightness;
            public Color color1, color2;
            public float radius, density, noiseScale, filament, threshold, dust;
            public float shellRadius, shellThickness;
        }

        public static readonly Preset[] Presets =
        {
            new Preset { name = "Emission (Orion)", type = 0f, brightness = 1.5f,
                color1 = new Color(2.3f, 0.5f, 0.85f), color2 = new Color(0.35f, 1.7f, 1.25f),
                radius = 8f, density = 0.95f, noiseScale = 0.30f, filament = 1.7f,
                threshold = 0.44f, dust = 1.5f, shellRadius = 0.6f, shellThickness = 0.14f },
            new Preset { name = "Reflection (Pleiades)", type = 1f, brightness = 2.0f,
                color1 = new Color(0.5f, 0.8f, 2.3f), color2 = new Color(0.6f, 0.8f, 1.6f),
                radius = 7f, density = 0.9f, noiseScale = 0.40f, filament = 1.2f,
                threshold = 0.5f, dust = 0.8f, shellRadius = 0.6f, shellThickness = 0.14f },
            new Preset { name = "Planetary (Ring)", type = 2f, brightness = 0.5f,
                color1 = new Color(2.1f, 0.55f, 0.45f), color2 = new Color(0.35f, 1.7f, 1.2f),
                radius = 5f, density = 0.9f, noiseScale = 0.7f, filament = 0.8f,
                threshold = 0.3f, dust = 0.6f, shellRadius = 0.6f, shellThickness = 0.05f },
            new Preset { name = "Supernova Remnant (Crab)", type = 3f, brightness = 1.0f,
                color1 = new Color(2.2f, 0.4f, 0.4f), color2 = new Color(0.4f, 1.6f, 1.1f),
                radius = 7f, density = 1.0f, noiseScale = 0.5f, filament = 1.5f,
                threshold = 0.35f, dust = 0.5f, shellRadius = 0.6f, shellThickness = 0.1f },
            new Preset { name = "Dark Nebula (Horsehead)", type = 4f, brightness = 1.0f,
                color1 = new Color(0.9f, 0.55f, 0.4f), color2 = new Color(0.4f, 0.5f, 0.7f),
                radius = 7f, density = 1.2f, noiseScale = 0.35f, filament = 1.3f,
                threshold = 0.4f, dust = 1.5f, shellRadius = 0.6f, shellThickness = 0.1f },
        };

        int index = -1;
        float timer;

        void Start() { Apply(0); }

        void Update()
        {
            timer += Time.deltaTime;
            bool click = false;
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            var k = UnityEngine.InputSystem.Keyboard.current;
            click = (m != null && m.leftButton.wasPressedThisFrame)
                 || (k != null && k.spaceKey.wasPressedThisFrame);
#else
            click = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#endif
            if (click || timer >= autoSeconds) Apply((index + 1) % Presets.Length);
        }

        public void Apply(int i)
        {
            index = i; timer = 0f;
            if (nebulaMaterial == null) return;
            var p = Presets[i];
            nebulaMaterial.SetFloat("_NebulaType", p.type);
            nebulaMaterial.SetFloat("_Brightness", p.brightness);
            nebulaMaterial.SetColor("_Color1", p.color1);
            nebulaMaterial.SetColor("_Color2", p.color2);
            nebulaMaterial.SetFloat("_Radius", p.radius);
            nebulaMaterial.SetFloat("_Density", p.density);
            nebulaMaterial.SetFloat("_NoiseScale", p.noiseScale);
            nebulaMaterial.SetFloat("_Filament", p.filament);
            nebulaMaterial.SetFloat("_Threshold", p.threshold);
            nebulaMaterial.SetFloat("_DustStrength", p.dust);
            nebulaMaterial.SetFloat("_ShellRadius", p.shellRadius);
            nebulaMaterial.SetFloat("_ShellThickness", p.shellThickness);

            if (stars != null) ConfigureStars(i, p);
        }

        void ConfigureStars(int i, Preset p)
        {
            // Real nebula photos are dominated by embedded stars; give each type
            // the cluster it actually shows.
            if (i == 0) // Orion emission: the tight teal-white Trapezium + field
                stars.Configure(new Color(0.75f, 1.15f, 1.35f), new Color(1.2f, 1.05f, 0.95f),
                                5, 70, p.radius, p.radius * 0.16f);
            else if (i == 1) // Pleiades reflection: bright blue giants spread wide
                stars.Configure(new Color(0.8f, 0.95f, 1.5f), new Color(0.85f, 0.95f, 1.3f),
                                8, 45, p.radius, p.radius * 0.55f);
            else if (i == 3) // SNR: a lone central pulsar + a sparse field
                stars.Configure(new Color(0.7f, 0.85f, 1.3f), new Color(1.1f, 1.0f, 0.95f),
                                1, 40, p.radius, p.radius * 0.06f);
            else if (i == 4) // dark nebula: NO embedded stars — it hides the ones behind
                stars.Configure(Color.white, Color.white, 0, 0, p.radius, p.radius);
            else // planetary: the white dwarf is in the shader; a few faint field stars
                stars.Configure(new Color(0.9f, 0.95f, 1.1f), new Color(0.9f, 0.9f, 1.0f),
                                0, 22, p.radius * 1.6f, p.radius);
        }

        public string CurrentName => index >= 0 ? Presets[index].name : "";
    }
}
