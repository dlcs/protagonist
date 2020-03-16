namespace IIIF.ImageApi
{
    /// <summary>
    /// Represents the {rotation} parameter of a IIIF image request.
    /// </summary>
    /// <remarks>see https://iiif.io/api/image/3.0/#43-rotation </remarks>
    public class RotationParameter
    {
        public bool Mirror { get; set; }
        public float Angle { get; set; }

        public static RotationParameter Parse(string pathPart)
        {
            var rotation = new RotationParameter();
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
