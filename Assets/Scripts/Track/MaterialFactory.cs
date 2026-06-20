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
