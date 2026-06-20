using UnityEngine;

namespace MarbleGP.Track
{
    /// <summary>
    /// Cria materiais simples em runtime de forma agnostica ao render pipeline
    /// (Built-in / URP). Usado para placeholders visuais (PRD 39.9).
    /// </summary>
    public static class MaterialFactory
    {
        private static Shader _litShader;
        private static Shader _unlitShader;

        private static Shader LitShader
        {
            get
            {
                if (_litShader == null)
                {
                    _litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_litShader == null) _litShader = Shader.Find("Standard");
                    if (_litShader == null) _litShader = Shader.Find("Diffuse");
                }
                return _litShader;
            }
        }

        private static Shader UnlitShader
        {
            get
            {
                if (_unlitShader == null)
                {
                    _unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (_unlitShader == null) _unlitShader = Shader.Find("Unlit/Color");
                    if (_unlitShader == null) _unlitShader = Shader.Find("Sprites/Default");
                }
                return _unlitShader;
            }
        }

        private static Font _legacyFont;
        /// <summary>Fonte built-in para TextMesh 3D (Arial foi removido em versoes novas).</summary>
        public static Font LegacyFont
        {
            get
            {
                if (_legacyFont == null)
                {
                    _legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_legacyFont == null) _legacyFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _legacyFont;
            }
        }

        /// <summary>Aplica fonte e material a um TextMesh 3D para garantir renderizacao.</summary>
        public static void ApplyFont(TextMesh tm)
        {
            var f = LegacyFont;
            if (f == null) return;
            tm.font = f;
            var mr = tm.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = f.material;
        }

        /// <summary>Material unlit (cor chapada) - bom para faixas/zebras/setas que
        /// precisam aparecer independente da iluminacao.</summary>
        public static Material CreateUnlit(Color color)
        {
            var mat = new Material(UnlitShader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }

        public static Material Create(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            var mat = new Material(LitShader);
            // Funciona tanto para Standard (_Color) quanto URP (_BaseColor).
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            return mat;
        }
    }
}
