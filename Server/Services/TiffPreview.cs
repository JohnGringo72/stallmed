using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace StallmedManager.Server.Services
{
    // Οι browsers δεν δείχνουν TIFF (συνήθως φαξ, συχνά πολλών σελίδων). Για ΠΡΟΒΟΛΗ
    // το μετατρέπουμε σε ένα PNG με τις σελίδες τη μία κάτω από την άλλη. Το αρχικό
    // αρχείο μένει ανέγγιχτο στη βάση και κατεβαίνει όπως είναι.
    // System.Drawing = μόνο Windows (ο server τρέχει σε IIS).
    public static class TiffPreview
    {
        private const int MaxWidth = 1100;   // αρκετό για ανάγνωση, χωρίς τεράστια εικόνα
        public const int MaxPages = 10;      // όριο μνήμης: ~40 MB στη χειρότερη περίπτωση
        private const int PageGap = 12;

        public static bool IsTiff(string? fileName, string? contentType)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            return ext is ".tif" or ".tiff"
                || string.Equals(contentType, "image/tiff", StringComparison.OrdinalIgnoreCase);
        }

        [SupportedOSPlatform("windows")]
        public static byte[] ToPng(byte[] tiff)
        {
            using var input = new MemoryStream(tiff);
            using var image = Image.FromStream(input);
            var dimension = new FrameDimension(image.FrameDimensionsList[0]);
            var pageCount = Math.Min(image.GetFrameCount(dimension), MaxPages);

            // Τα φαξ έχουν συχνά διαφορετική οριζόντια/κάθετη ανάλυση (π.χ. 204x98 dpi):
            // χωρίς διόρθωση, η σελίδα φαίνεται «πατημένη» στο μισό ύψος.
            var pages = new List<(int Width, int Height)>();
            for (var i = 0; i < pageCount; i++)
            {
                image.SelectActiveFrame(dimension, i);
                var aspect = image.VerticalResolution > 0 && image.HorizontalResolution > 0
                    ? image.HorizontalResolution / image.VerticalResolution
                    : 1f;
                var scale = Math.Min(1.0, (double)MaxWidth / image.Width);
                pages.Add(((int)Math.Round(image.Width * scale), (int)Math.Round(image.Height * aspect * scale)));
            }

            var width = pages.Max(p => p.Width);
            var height = pages.Sum(p => p.Height) + PageGap * (pages.Count - 1);

            using var canvas = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.FromArgb(0x40, 0x44, 0x4b));   // ίδιο φόντο με το παράθυρο προβολής
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                var y = 0;
                for (var i = 0; i < pages.Count; i++)
                {
                    image.SelectActiveFrame(dimension, i);
                    g.DrawImage(image, new Rectangle(0, y, pages[i].Width, pages[i].Height));
                    y += pages[i].Height + PageGap;
                }
            }

            using var output = new MemoryStream();
            canvas.Save(output, ImageFormat.Png);
            return output.ToArray();
        }
    }
}
