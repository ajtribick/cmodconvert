using System;
using System.Collections.Generic;

namespace CmodConvert
{
    internal class Material
    {
        public Color? Diffuse { get; set; }
        public Color? Specular { get; set; }
        public Color? Emissive { get; set; }
        public float? SpecularPower { get; set; }
        public float? Opacity { get; set; }
        public BlendMode? BlendMode { get; set; }
        public List<string?> Textures { get; } = new List<string?>();

        public void AddTexture(int index, string texture)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (index < Textures.Count && Textures[index] != null) throw new CmodException("Multiple entries for texture");
            while (Textures.Count <= index)
            {
                Textures.Add(null);
            }

            Textures[index] = texture;
        }
    }
}
