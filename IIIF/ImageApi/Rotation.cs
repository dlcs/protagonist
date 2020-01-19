namespace IIIF.ImageApi
{
    public class Rotation
    {
        public bool Mirror { get; set; }
        public float Angle { get; set; }

        public static Rotation Parse(string pathPart)
        {
            var rotation = new Rotation();
            if (pathPart[0] == '!')
            {
                rotation.Mirror = true;
                pathPart = pathPart.Substring(1);
            }
            rotation.Angle = float.Parse(pathPart);
            return rotation;
        }
    }
}
