using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;

namespace FACS01_Bundle_Comp_Decomp
{
    class Program
    {
        public static bool noConsole = false;
        static void Main(string[] args)
        {
            if (args.Length >= 4) { noConsole = args[3] == "no-console"; }
            if (!noConsole)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("FACS01 AssetBundle Compression/Decompression Tool");
                Console.ForegroundColor = ConsoleColor.White;
            }
            if (args.Length < 3)
            {
                if (!noConsole) ErrorMSG("Usage: {method} {bundlePath} {savePath}\nmethod: c (compression) , d (decompression)");
                return;
            }
            
            string bundlePath = args[1].Replace(@"/",@"\");
            if (!File.Exists(bundlePath))
            {
                if (!noConsole) ErrorMSG($"File at \"{bundlePath}\" doesn't exist.");
                return;
            }
            string savePath = args[2].Replace(@"/", @"\");
            string saveFileName = savePath.Split(@"\").Last();
            if (!saveFileName.Contains('.'))
            {
                if (!noConsole) ErrorMSG($"Save path \"{savePath}\" doesn't end with file extension.");
                return;
            }
            
            string work = args[0];
            if (work == "d")
            {
                if (!noConsole) Console.WriteLine("Decompression routine started!");
                DecompressFile(bundlePath, savePath);
            }
            else if (work == "c")
            {
                if (!noConsole) Console.WriteLine("Compression routine launched!");
                CompressBundle(bundlePath, savePath);
                if  (!noConsole)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("File compressed!");
                }       
            }
        }

        public static void DecompressFile(string compressed_path, string decompressed_path_result)
        {
            using (var input_stream = File.OpenRead(compressed_path))
            {
                var compressed_file = new AssetBundleFile();
                compressed_file.Read(new AssetsFileReader(input_stream), true);
                if (compressed_file.bundleHeader6 != null && compressed_file.bundleHeader6.GetCompressionType() != 0)
                {
                    using (FileStream output_stream = File.Open(decompressed_path_result, FileMode.Create, FileAccess.ReadWrite))
                    {
                        if (!noConsole) Console.ForegroundColor = ConsoleColor.Blue;
                        var progressBar = new SZProgress();
                        compressed_file.reader.Position = 0;
                        compressed_file.Unpack(compressed_file.reader, new AssetsFileWriter(output_stream), progressBar);
                        output_stream.Position = 0;
                    }
                    if (!noConsole)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine("File decompressed!");
                    }
                }
            }
        }

        public static void CompressBundle(string decompressed_path, string compressed_path_result)
        {
            var am = new AssetsManager();
            var bun = am.LoadBundleFile(decompressed_path);
            using (var stream = File.OpenWrite(compressed_path_result))
            {
                using (var writer = new AssetsFileWriter(stream))
                {
                    if (!noConsole) Console.ForegroundColor = ConsoleColor.Blue;
                    var progressBar = new SZProgress();
                    bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZMA, progressBar);
                }
            }
        }

        public static void ErrorMSG(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }
    }
    public class SZProgress : SevenZip.ICodeProgress
    {
        public ulong maxSize;
        public float prog;

        public SZProgress()
        {
            maxSize = 1;
            prog = 0.0f;
        }
        public void SetProgress(ulong inSize)
        {
            float pgs = (float)inSize / maxSize;
            if (pgs > prog + 0.005f)
            {
                prog = pgs;
                string progstr = (prog * 100).ToString("n4");
                if (Program.noConsole) Console.Out.WriteLine(progstr);
                else Console.Write($"\rProgress: {progstr}%");
            }
        }

        public void SetMaxSize(ulong maxSize)
        {
            this.maxSize = maxSize;
        }

        public void Clear()
        {
            maxSize = 0;
            prog = 0.0f;
            if (!Program.noConsole)
            {
                Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
            }
        }
    }
}