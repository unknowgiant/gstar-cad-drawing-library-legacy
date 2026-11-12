using System;

namespace GstarCadDrawingLibrary
{
    internal sealed class InsertDrawingEventArgs : EventArgs
    {
        public InsertDrawingEventArgs(string filePath, double scale, double rotationDegrees)
        {
            FilePath = filePath;
            Scale = scale;
            RotationDegrees = rotationDegrees;
        }

        public string FilePath { get; private set; }
        public double Scale { get; private set; }
        public double RotationDegrees { get; private set; }
    }
}
