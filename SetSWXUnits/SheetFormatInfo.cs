namespace SetSWXUnits
{
    public class SheetFormatInfo
    {
        public string SheetName { get; set; } = string.Empty;
        public string TemplatePath { get; set; } = string.Empty;
        public string TemplateFileName { get; set; } = string.Empty;
        public int PaperSize { get; set; }
        public int TemplateIn { get; set; }
        public double Scale1 { get; set; }
        public double Scale2 { get; set; }
        public bool FirstAngle { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
