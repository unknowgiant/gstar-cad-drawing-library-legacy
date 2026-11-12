namespace GstarCadDrawingLibrary
{
    internal sealed class DrawingItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }

        public string CategoryDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(Category))
                {
                    return "未分类";
                }

                return Category.Replace("\\", " / ");
            }
        }
    }
}
