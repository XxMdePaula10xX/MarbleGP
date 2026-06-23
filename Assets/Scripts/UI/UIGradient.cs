using UnityEngine;
using UnityEngine.UI;

namespace MarbleGP.UI
{
    /// <summary>
    /// Gradiente vertical aplicado aos vertices de um Graphic (Image/Text).
    /// Usado como "sheen" sutil nos botoes e cards para dar profundidade,
    /// sem precisar de TextMeshPro/shaders. Multiplica a cor existente, entao
    /// continua compativel com os estados de ColorTint do Button.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIGradient : BaseMeshEffect
    {
        public Color top = Color.white;
        public Color bottom = new Color(0.84f, 0.84f, 0.84f, 1f);

        public override void ModifyMesh(VertexHelper vh)
        {
            int count = vh.currentVertCount;
            if (!IsActive() || count == 0) return;

            float minY = float.MaxValue, maxY = float.MinValue;
            var vert = new UIVertex();
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                if (vert.position.y < minY) minY = vert.position.y;
                if (vert.position.y > maxY) maxY = vert.position.y;
            }

            float h = Mathf.Max(0.0001f, maxY - minY);
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                float t = (vert.position.y - minY) / h;
                vert.color *= Color.Lerp(bottom, top, t);
                vh.SetUIVertex(vert, i);
            }
        }
    }
}
