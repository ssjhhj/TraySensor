using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TraySensor.IconBuilder
{
    public static class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: IconBuilder <input image> <output .ico>");
                return 1;
            }

            string src = args[0];
            string dst = args[1];

            try
            {
                if (!File.Exists(src))
                {
                    Console.WriteLine("Source not found: " + src);
                    return 2;
                }

                int[] sizes = { 16, 32, 48, 64, 128, 256 };

                using var fs = new FileStream(dst, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                bw.Write((short)0);    // Reserved
                bw.Write((short)1);    // Type: 1=ICON
                bw.Write((short)sizes.Length);  // Count

                long entriesPos = bw.BaseStream.Position;
                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write((byte)0); // width (0 means >= 256)
                    bw.Write((byte)0); // height
                    bw.Write((byte)0); // color count
                    bw.Write((byte)0); // reserved
                    bw.Write((short)1); // planes
                    bw.Write((short)32); // bitcount
                    bw.Write(0); // dwBytesInRes (backpatch)
                    bw.Write(0); // dwImageOffset (backpatch)
                }

                long[] offsets = new long[sizes.Length];
                int[] byteLens = new int[sizes.Length];

                using var srcBmp = new Bitmap(src);

                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    offsets[i] = bw.BaseStream.Position;
                    byte[] pngBytes = RenderAndEncodePng(srcBmp, size);
                    bw.Write(pngBytes);
                    byteLens[i] = pngBytes.Length;
                }

                long cur = bw.BaseStream.Position;
                bw.BaseStream.Position = entriesPos;
                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.Write((byte)(size >= 256 ? 0 : size));
                    bw.BaseStream.Position += 10;
                    bw.Write(byteLens[i]);
                    bw.Write((int)offsets[i]);
                }
                bw.BaseStream.Position = cur;

                Console.WriteLine($"OK -> {dst}, {sizes.Length} layers");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERR: " + ex);
                return 3;
            }
        }

        private static byte[] RenderAndEncodePng(Bitmap src, int size)
        {
            using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);

                float scale = Math.Min((float)size / src.Width, (float)size / src.Height);
                int w = (int)Math.Round(src.Width * scale);
                int h = (int)Math.Round(src.Height * scale);
                int x = (size - w) / 2;
                int y = (size - h) / 2;
                g.DrawImage(src, new Rectangle(x, y, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }
}
