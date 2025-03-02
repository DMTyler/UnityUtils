using UnityEngine;

namespace DM.Utils
{
    public static class RenderUtils
    {
        public static void RenderRT(RenderTexture rt)
        {
            var shader = Shader.Find("Hidden/RenderRT");
        }
    }
}

