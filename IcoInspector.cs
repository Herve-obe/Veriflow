using System;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string path = @"d:\ELEMENT\VERIFLOW\src\Veriflow.Desktop\Assets\veriflow.ico";
        if (!File.Exists(path))
        {
            Console.WriteLine("File not found.");
            return;
        }

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        using (var br = new BinaryReader(fs))
        {
            // Read Header
            short reserved = br.ReadInt16();
            short type = br.ReadInt16();
            short count = br.ReadInt16();

            Console.WriteLine($"ICO File: {Path.GetFileName(path)}");
            Console.WriteLine($"Image Count: {count}");

            for (int i = 0; i < count; i++)
            {
                byte width = br.ReadByte();
                byte height = br.ReadByte();
                byte colors = br.ReadByte();
                byte reserved1 = br.ReadByte();
                short planes = br.ReadInt16();
                short bpp = br.ReadInt16();
                int size = br.ReadInt32();
                int offset = br.ReadInt32();

                int w = width == 0 ? 256 : width;
                int h = height == 0 ? 256 : height;

                Console.WriteLine($"  #{i + 1}: {w}x{h} - {bpp} bits - {size} bytes");
            }
        }
    }
}
