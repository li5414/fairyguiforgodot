// Ported from FairyGUI-unity: Assets/Scripts/Core/Mesh/PlaneMesh.cs
using Godot;

namespace FairyGUI
{
    // [Godot] Name collides with Godot.PlaneMesh. Inside namespace FairyGUI the simple name
    // "PlaneMesh" resolves to this class; user code that imports both namespaces needs an alias.
    /// <summary>
    ///
    /// </summary>
    public class PlaneMesh : IMeshFactory
    {
        public int gridSize = 30;

        public void OnPopulateMesh(VertexBuffer vb)
        {
            float w = vb.contentRect.width;
            float h = vb.contentRect.height;
            float xMax = vb.contentRect.xMax;
            float yMax = vb.contentRect.yMax;
            int hc = (int)UMath.Min(UMath.CeilToInt(w / gridSize), 9);
            int vc = (int)UMath.Min(UMath.CeilToInt(h / gridSize), 9);
            int eachPartX = UMath.FloorToInt(w / hc);
            int eachPartY = UMath.FloorToInt(h / vc);
            float x, y;
            for (int i = 0; i <= vc; i++)
            {
                if (i == vc)
                    y = yMax;
                else
                    y = vb.contentRect.y + i * eachPartY;
                for (int j = 0; j <= hc; j++)
                {
                    if (j == hc)
                        x = xMax;
                    else
                        x = vb.contentRect.x + j * eachPartX;
                    vb.AddVert(new Vector3(x, y, 0));
                }
            }

            for (int i = 0; i < vc; i++)
            {
                int k = i * (hc + 1);
                for (int j = 1; j <= hc; j++)
                {
                    int m = k + j;
                    vb.AddTriangle(m - 1, m, m + hc);
                    vb.AddTriangle(m, m + hc + 1, m + hc);
                }
            }
        }
    }
}
