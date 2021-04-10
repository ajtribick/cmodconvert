using System.Collections.Generic;

namespace CmodConvert
{
    internal class CmodData
    {
        public CmodData(IReadOnlyCollection<Material> materials, IReadOnlyCollection<Mesh> meshes)
        {
            Materials = materials;
            Meshes = meshes;
        }

        public IReadOnlyCollection<Material> Materials { get; }
        public IReadOnlyCollection<Mesh> Meshes { get; }
    }
}
