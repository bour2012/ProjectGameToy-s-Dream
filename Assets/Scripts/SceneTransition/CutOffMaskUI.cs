using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class CutOffMaskUI : Image
{
   public override Material materialForRendering
    {
        get
        {
            Material mat = new Material(base.materialForRendering);
            mat.SetFloat("_StencilComp", (int)CompareFunction.NotEqual); // Set the cutoff value as needed
            return mat;
        }
    }
}
